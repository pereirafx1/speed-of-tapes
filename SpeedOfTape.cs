// =============================================================================
// SpeedOfTape.cs — ATAS Custom Indicator  v2.0
//
// Both buy and sell tape speeds are displayed as POSITIVE histogram bars in
// the same panel, making buy vs sell directly comparable by height.
//
//   Green  bars — buyer-initiated speed (contracts/sec at the ask)
//   Purple bars — seller-initiated speed (contracts/sec at the bid)
//   Yellow bars — buy bar that passes the active filter (signal)
//   Cyan   bars — sell bar that passes the active filter (signal)
//   Gray line   — rolling average total tape speed (reference)
//
// Build:   dotnet build SpeedOfTape.csproj -c Release
// Deploy:  copy bin\Release\net48\SpeedOfTape.dll
//          → %APPDATA%\ATAS Platform\CustomIndicators\
// =============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace SpeedOfTapeIndicator
{
    /// <summary>
    /// Speed of Tape v2 — order-flow histogram for ATAS.
    ///
    /// Renders buyer and seller tape speeds as independent positive histogram bars
    /// so their magnitudes can be compared directly.  An optional filter system
    /// (Auto or Custom) highlights bars that represent statistically significant
    /// or volume/speed-elevated tape activity.
    /// </summary>
    [DisplayName("Speed of Tape")]
    [Description("Buyer and seller tape speeds shown as positive bars. Filter system highlights significant activity.")]
    [Category("Order Flow")]
    public class SpeedOfTape : Indicator
    {
        // ================================================================== //
        //  Private fields                                                      //
        // ================================================================== //

        private int     _period              = 10;
        private bool    _useAutoFilter       = true;
        private bool    _useCustomFilters    = false;
        private decimal _minVolumeMultiplier = 1.5m;
        private int     _volumeLookbackBars  = 10;
        private decimal _minSpeedMultiplier  = 1.5m;
        private int     _speedLookbackBars   = 10;
        private decimal _deltaThreshold      = 0.6m;
        private int     _minBarHeight        = 5;
        private bool    _showOnlyFiltered    = false;

        private Color _buyColor          = Colors.Green;
        private Color _sellColor         = Color.FromRgb(160, 32, 240);  // purple/violet
        private Color _filteredBuyColor  = Colors.Yellow;
        private Color _filteredSellColor = Colors.Cyan;

        // Five data series registered with ATAS
        private readonly ValueDataSeries _buyNormalSeries;
        private readonly ValueDataSeries _sellNormalSeries;
        private readonly ValueDataSeries _buyFilteredSeries;
        private readonly ValueDataSeries _sellFilteredSeries;
        private readonly ValueDataSeries _avgSpeedSeries;

        // ================================================================== //
        //  Properties — Settings                                              //
        // ================================================================== //

        /// <summary>Rolling window size (bars) for the speed calculation.</summary>
        [Display(Name = "Period", GroupName = "Settings", Order = 10,
            Description = "Number of bars in the rolling window used to compute tape speed.")]
        [Range(1, 500)]
        public int Period
        {
            get => _period;
            set
            {
                if (value < 1) return;
                _period = value;
                RecalculateValues();
            }
        }

        // ================================================================== //
        //  Properties — Colors                                                //
        // ================================================================== //

        /// <summary>Colour for buyer-speed bars that have not triggered a filter signal.</summary>
        [Display(Name = "Buy Color", GroupName = "Colors", Order = 20)]
        public Color BuyColor
        {
            get => _buyColor;
            set { _buyColor = value; _buyNormalSeries.Color = value; RecalculateValues(); }
        }

        /// <summary>Colour for seller-speed bars that have not triggered a filter signal.</summary>
        [Display(Name = "Sell Color", GroupName = "Colors", Order = 21)]
        public Color SellColor
        {
            get => _sellColor;
            set { _sellColor = value; _sellNormalSeries.Color = value; RecalculateValues(); }
        }

        /// <summary>Colour for a buy bar that passes the active filter (signal bar).</summary>
        [Display(Name = "Filtered Buy Color", GroupName = "Colors", Order = 22,
            Description = "Buy bar colour when the bar passes the active filter.")]
        public Color FilteredBuyColor
        {
            get => _filteredBuyColor;
            set { _filteredBuyColor = value; _buyFilteredSeries.Color = value; RecalculateValues(); }
        }

        /// <summary>Colour for a sell bar that passes the active filter (signal bar).</summary>
        [Display(Name = "Filtered Sell Color", GroupName = "Colors", Order = 23,
            Description = "Sell bar colour when the bar passes the active filter.")]
        public Color FilteredSellColor
        {
            get => _filteredSellColor;
            set { _filteredSellColor = value; _sellFilteredSeries.Color = value; RecalculateValues(); }
        }

        // ================================================================== //
        //  Properties — Filter Toggles                                        //
        // ================================================================== //

        /// <summary>
        /// Automatically highlights bars whose speed exceeds mean + 1.5 × std dev
        /// over the last 50 bars.  Takes priority over Custom Filters when both are on.
        /// </summary>
        [Display(Name = "Use Auto Filter", GroupName = "Filters", Order = 30,
            Description = "Highlights bars > 1.5 std dev above the 50-bar mean speed. Overrides custom filters.")]
        public bool UseAutoFilter
        {
            get => _useAutoFilter;
            set { _useAutoFilter = value; RecalculateValues(); }
        }

        /// <summary>
        /// Activates the manual volume-and-speed multiplier filters defined below.
        /// Ignored when <see cref="UseAutoFilter"/> is also enabled.
        /// </summary>
        [Display(Name = "Use Custom Filters", GroupName = "Filters", Order = 31,
            Description = "Enable volume and speed multiplier thresholds. Ignored when Auto Filter is on.")]
        public bool UseCustomFilters
        {
            get => _useCustomFilters;
            set { _useCustomFilters = value; RecalculateValues(); }
        }

        // ================================================================== //
        //  Properties — Volume Filter                                         //
        // ================================================================== //

        /// <summary>
        /// Current bar's total volume must be at least this multiple of the
        /// lookback average for the volume condition to pass.
        /// </summary>
        [Display(Name = "Min Volume Multiplier", GroupName = "Volume Filter", Order = 40,
            Description = "Bar volume must be ≥ this × average volume of the last N bars.")]
        [Range(0.1, 20.0)]
        public decimal MinVolumeMultiplier
        {
            get => _minVolumeMultiplier;
            set { _minVolumeMultiplier = value; RecalculateValues(); }
        }

        /// <summary>Number of prior bars used to build the reference average volume.</summary>
        [Display(Name = "Volume Lookback Bars", GroupName = "Volume Filter", Order = 41,
            Description = "How many prior bars are averaged to produce the volume baseline.")]
        [Range(1, 500)]
        public int VolumeLookbackBars
        {
            get => _volumeLookbackBars;
            set { if (value >= 1) { _volumeLookbackBars = value; RecalculateValues(); } }
        }

        // ================================================================== //
        //  Properties — Speed Filter                                          //
        // ================================================================== //

        /// <summary>
        /// Buy or sell speed must be at least this multiple of its own lookback
        /// average for the speed condition to pass.
        /// </summary>
        [Display(Name = "Min Speed Multiplier", GroupName = "Speed Filter", Order = 50,
            Description = "Buy/sell speed must be ≥ this × the average speed of the last N bars.")]
        [Range(0.1, 20.0)]
        public decimal MinSpeedMultiplier
        {
            get => _minSpeedMultiplier;
            set { _minSpeedMultiplier = value; RecalculateValues(); }
        }

        /// <summary>Number of prior bars used to build the reference average speed.</summary>
        [Display(Name = "Speed Lookback Bars", GroupName = "Speed Filter", Order = 51,
            Description = "How many prior bars are averaged to produce the speed baseline.")]
        [Range(1, 500)]
        public int SpeedLookbackBars
        {
            get => _speedLookbackBars;
            set { if (value >= 1) { _speedLookbackBars = value; RecalculateValues(); } }
        }

        // ================================================================== //
        //  Properties — Additional Filters                                    //
        // ================================================================== //

        /// <summary>
        /// Minimum fraction of total bar volume that must be on one side for a bar
        /// to qualify as a filter signal.  Set to 0 to disable.
        /// Example: 0.6 means at least 60 % must be buy or sell to signal.
        /// </summary>
        [Display(Name = "Delta Threshold", GroupName = "Additional Filters", Order = 60,
            Description = "Buy% or sell% of total volume must exceed this fraction. 0 = disabled.")]
        [Range(0.0, 1.0)]
        public decimal DeltaThreshold
        {
            get => _deltaThreshold;
            set { _deltaThreshold = value; RecalculateValues(); }
        }

        /// <summary>
        /// Bars whose computed speed falls below this value are treated as noise
        /// and suppressed regardless of filter state.
        /// </summary>
        [Display(Name = "Min Bar Height", GroupName = "Additional Filters", Order = 61,
            Description = "Bars with speed below this threshold are hidden (noise gate).")]
        [Range(0, 10000)]
        public int MinBarHeight
        {
            get => _minBarHeight;
            set { _minBarHeight = value; RecalculateValues(); }
        }

        /// <summary>
        /// When true, only bars that pass the active filter are rendered;
        /// all others are suppressed entirely.
        /// </summary>
        [Display(Name = "Show Only Filtered", GroupName = "Additional Filters", Order = 62,
            Description = "Hide non-signal bars. Only filtered (signal) bars are drawn.")]
        public bool ShowOnlyFiltered
        {
            get => _showOnlyFiltered;
            set { _showOnlyFiltered = value; RecalculateValues(); }
        }

        // ================================================================== //
        //  Constructor                                                         //
        // ================================================================== //

        /// <summary>
        /// Registers all five data series and places the indicator in its own panel.
        /// </summary>
        public SpeedOfTape()
        {
            Panel = IndicatorDataProvider.NewPanel;

            _buyNormalSeries = new ValueDataSeries("Buy")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _buyColor,
                ShowZeroValue = false,
            };

            _sellNormalSeries = new ValueDataSeries("Sell")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _sellColor,
                ShowZeroValue = false,
            };

            _buyFilteredSeries = new ValueDataSeries("Buy Signal")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _filteredBuyColor,
                ShowZeroValue = false,
            };

            _sellFilteredSeries = new ValueDataSeries("Sell Signal")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _filteredSellColor,
                ShowZeroValue = false,
            };

            // Thin line drawn at the rolling average total tape speed
            _avgSpeedSeries = new ValueDataSeries("Avg Speed")
            {
                VisualType    = VisualMode.Line,
                Color         = Colors.Gray,
                ShowZeroValue = false,
            };

            DataSeries[0] = _buyNormalSeries;
            DataSeries.Add(_sellNormalSeries);
            DataSeries.Add(_buyFilteredSeries);
            DataSeries.Add(_sellFilteredSeries);
            DataSeries.Add(_avgSpeedSeries);
        }

        // ================================================================== //
        //  OnCalculate                                                         //
        // ================================================================== //

        /// <summary>
        /// Called by ATAS for every bar (and on each real-time tick for the live bar).
        /// Computes buy and sell tape speeds, applies the active filter, and routes
        /// values to the appropriate colour series.
        /// </summary>
        /// <param name="bar">Zero-based bar index being (re)calculated.</param>
        /// <param name="value">Close price — unused; we read order-flow fields directly.</param>
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                _buyNormalSeries.Clear();
                _sellNormalSeries.Clear();
                _buyFilteredSeries.Clear();
                _sellFilteredSeries.Clear();
                _avgSpeedSeries.Clear();
                return;
            }

            // ---- 1. Current bar speeds (both positive) ------------------- //
            var (buySpeed, sellSpeed) = CalculateBarSpeed(bar);
            decimal totalSpeed = buySpeed + sellSpeed;

            // Noise gate: treat bars below MinBarHeight as zero
            decimal buyValue  = buySpeed  >= _minBarHeight ? buySpeed  : 0m;
            decimal sellValue = sellSpeed >= _minBarHeight ? sellSpeed : 0m;

            // ---- 2. Filter pass / fail ------------------------------------ //
            bool filterActive = _useAutoFilter || _useCustomFilters;
            bool buyFiltered  = false;
            bool sellFiltered = false;

            if (filterActive)
            {
                if (_useAutoFilter)
                {
                    // Thresholds = mean + 1.5 × stdDev over last 50 bars
                    var (buyThresh, sellThresh) = CalculateAutoThresholds(bar);
                    buyFiltered  = buyValue  > 0m && buySpeed  >= buyThresh;
                    sellFiltered = sellValue > 0m && sellSpeed >= sellThresh;
                }
                else // _useCustomFilters
                {
                    IndicatorCandle current = GetCandle(bar);
                    decimal currentVol = current?.Volume ?? 0m;

                    decimal avgVol = CalculateAvgBarVolume(bar, _volumeLookbackBars);
                    bool    volOk  = avgVol <= 0m || currentVol >= avgVol * _minVolumeMultiplier;

                    var (avgBuySpd, avgSellSpd) = CalculateAvgSpeeds(bar, _speedLookbackBars);
                    bool buySpeedOk  = avgBuySpd  <= 0m || buySpeed  >= avgBuySpd  * _minSpeedMultiplier;
                    bool sellSpeedOk = avgSellSpd <= 0m || sellSpeed >= avgSellSpd * _minSpeedMultiplier;

                    buyFiltered  = buyValue  > 0m && volOk && buySpeedOk;
                    sellFiltered = sellValue > 0m && volOk && sellSpeedOk;
                }

                // Delta threshold: one side must represent at least X% of total volume
                if (_deltaThreshold > 0m && totalSpeed > 0m)
                {
                    if (buySpeed  / totalSpeed < _deltaThreshold) buyFiltered  = false;
                    if (sellSpeed / totalSpeed < _deltaThreshold) sellFiltered = false;
                }
            }

            // ---- 3. Route values to the correct colour series ------------- //
            if (!filterActive)
            {
                // No filtering — plain green / purple bars
                _buyNormalSeries[bar]    = buyValue;
                _sellNormalSeries[bar]   = sellValue;
                _buyFilteredSeries[bar]  = 0m;
                _sellFilteredSeries[bar] = 0m;
            }
            else if (_showOnlyFiltered)
            {
                // Only signal bars rendered; everything else suppressed
                _buyNormalSeries[bar]    = 0m;
                _sellNormalSeries[bar]   = 0m;
                _buyFilteredSeries[bar]  = buyFiltered  ? buyValue  : 0m;
                _sellFilteredSeries[bar] = sellFiltered ? sellValue : 0m;
            }
            else
            {
                // All bars shown; signal bars drawn in highlight colour,
                // non-signal bars remain in the base colour
                _buyNormalSeries[bar]    = buyFiltered  ? 0m : buyValue;
                _sellNormalSeries[bar]   = sellFiltered ? 0m : sellValue;
                _buyFilteredSeries[bar]  = buyFiltered  ? buyValue  : 0m;
                _sellFilteredSeries[bar] = sellFiltered ? sellValue : 0m;
            }

            // ---- 4. Average-speed reference line -------------------------- //
            _avgSpeedSeries[bar] = CalculateAvgTotalSpeed(bar, _period);
        }

        // ================================================================== //
        //  Private helpers                                                     //
        // ================================================================== //

        /// <summary>
        /// Computes buy and sell tape speed (contracts/second) for the <see cref="Period"/>
        /// rolling window ending at <paramref name="bar"/>.
        /// </summary>
        private (decimal buySpeed, decimal sellSpeed) CalculateBarSpeed(int bar)
        {
            int     start        = Math.Max(0, bar - _period + 1);
            decimal totalBuy     = 0m;
            decimal totalSell    = 0m;
            double  totalSeconds = 0d;

            for (int i = start; i <= bar; i++)
            {
                IndicatorCandle c = GetCandle(i);
                if (c is null) continue;

                totalBuy  += c.Ask;
                totalSell += c.Bid;

                double secs = (c.LastTime - c.Time).TotalSeconds;
                if (secs > 0d) totalSeconds += secs;
            }

            if (totalSeconds <= 0d) totalSeconds = 1d;
            return (totalBuy / (decimal)totalSeconds, totalSell / (decimal)totalSeconds);
        }

        /// <summary>
        /// Computes separate buy and sell thresholds as mean + 1.5 × std dev
        /// calculated over the 50 bars immediately before <paramref name="bar"/>.
        /// </summary>
        private (decimal buyThreshold, decimal sellThreshold) CalculateAutoThresholds(int bar)
        {
            const int     historyBars = 50;
            const decimal stdDevMul   = 1.5m;

            int start = Math.Max(0, bar - historyBars);
            var buyValues  = new List<decimal>(historyBars);
            var sellValues = new List<decimal>(historyBars);

            for (int i = start; i < bar; i++)
            {
                var (b, s) = CalculateBarSpeed(i);
                buyValues.Add(b);
                sellValues.Add(s);
            }

            return (
                MeanPlusStdDev(buyValues,  stdDevMul),
                MeanPlusStdDev(sellValues, stdDevMul)
            );
        }

        /// <summary>
        /// Returns mean + <paramref name="multiplier"/> × population standard deviation
        /// for <paramref name="values"/>, or zero when fewer than two samples exist.
        /// </summary>
        private static decimal MeanPlusStdDev(List<decimal> values, decimal multiplier)
        {
            if (values.Count < 2) return 0m;

            decimal mean = 0m;
            foreach (decimal v in values) mean += v;
            mean /= values.Count;

            decimal variance = 0m;
            foreach (decimal v in values) variance += (v - mean) * (v - mean);
            variance /= values.Count;

            return mean + multiplier * (decimal)Math.Sqrt((double)variance);
        }

        /// <summary>
        /// Average single-bar total volume over the <paramref name="lookback"/> bars
        /// immediately before <paramref name="bar"/>.
        /// </summary>
        private decimal CalculateAvgBarVolume(int bar, int lookback)
        {
            int     start = Math.Max(0, bar - lookback);
            decimal total = 0m;
            int     count = 0;

            for (int i = start; i < bar; i++)
            {
                IndicatorCandle c = GetCandle(i);
                if (c is null) continue;
                total += c.Volume;
                count++;
            }

            return count > 0 ? total / count : 0m;
        }

        /// <summary>
        /// Average buy and sell tape speeds over the <paramref name="lookback"/> bars
        /// immediately before <paramref name="bar"/>.
        /// </summary>
        private (decimal avgBuy, decimal avgSell) CalculateAvgSpeeds(int bar, int lookback)
        {
            int     start   = Math.Max(0, bar - lookback);
            decimal sumBuy  = 0m;
            decimal sumSell = 0m;
            int     count   = 0;

            for (int i = start; i < bar; i++)
            {
                var (b, s) = CalculateBarSpeed(i);
                sumBuy  += b;
                sumSell += s;
                count++;
            }

            if (count == 0) return (0m, 0m);
            return (sumBuy / count, sumSell / count);
        }

        /// <summary>
        /// Rolling average of combined tape speed (buy + sell) over the last
        /// <paramref name="lookback"/> bars, used for the reference line.
        /// </summary>
        private decimal CalculateAvgTotalSpeed(int bar, int lookback)
        {
            int     start = Math.Max(0, bar - lookback);
            decimal sum   = 0m;
            int     count = 0;

            for (int i = start; i < bar; i++)
            {
                var (b, s) = CalculateBarSpeed(i);
                sum += b + s;
                count++;
            }

            return count > 0 ? sum / count : 0m;
        }
    }
}
