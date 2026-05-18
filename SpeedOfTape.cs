// =============================================================================
// SpeedOfTape.cs — ATAS Custom Indicator
//
// Measures the velocity of buyer- vs seller-aggressed trades (tape speed)
// and renders a histogram in a separate panel:
//   • GREEN bars  — buy aggression dominates (trades filled at the ask)
//   • PURPLE bars — sell aggression dominates (trades filled at the bid)
//   • Bar height  — magnitude of the net speed difference (contracts / second)
//
// Build (run from the directory containing SpeedOfTape.csproj):
//   dotnet build SpeedOfTape.csproj -c Release
//
// Deploy:
//   Copy bin\Release\net48\SpeedOfTape.dll to:
//     %APPDATA%\ATAS Platform\CustomIndicators\
// =============================================================================

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace SpeedOfTapeIndicator
{
    /// <summary>
    /// Speed of Tape — ATAS order-flow histogram indicator.
    ///
    /// For each bar the indicator looks back over the last <see cref="Period"/> bars,
    /// accumulates the buyer-initiated volume (trades at the ask) and the
    /// seller-initiated volume (trades at the bid), then divides each total by
    /// the elapsed wall-clock time to produce a per-second rate.
    ///
    /// A positive net rate is drawn as a green bar; a negative net rate (sellers
    /// faster) is drawn as a purple bar below the zero line.
    /// </summary>
    [DisplayName("Speed of Tape")]
    [Description("Histogram: green = buy tape faster, purple = sell tape faster. Height = contracts/sec delta.")]
    [Category("Order Flow")]
    public class SpeedOfTape : Indicator
    {
        // ------------------------------------------------------------------ //
        //  Private state                                                       //
        // ------------------------------------------------------------------ //

        private int    _period    = 10;
        private Color  _buyColor  = Colors.Green;
        private Color  _sellColor = Color.FromRgb(128, 0, 128); // purple/violet

        private readonly ValueDataSeries _buySpeedSeries;
        private readonly ValueDataSeries _sellSpeedSeries;

        // ------------------------------------------------------------------ //
        //  User-configurable parameters                                        //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Number of bars in the rolling lookback window used for speed calculation.
        /// Smaller values = more reactive; larger values = smoother signal.
        /// </summary>
        [Display(Name = "Period",
                 GroupName = "Settings",
                 Order = 1,
                 Description = "Rolling lookback window (bars) for speed calculation.")]
        [Range(1, 500)]
        public int Period
        {
            get => _period;
            set
            {
                if (value < 1)
                    return;
                _period = value;
                RecalculateValues();
            }
        }

        /// <summary>
        /// Histogram bar colour when buyer-initiated volume rate exceeds seller rate.
        /// </summary>
        [Display(Name = "Buy Color",
                 GroupName = "Visual",
                 Order = 2,
                 Description = "Bar colour when buyers dominate the tape.")]
        public Color BuyColor
        {
            get => _buyColor;
            set
            {
                _buyColor = value;
                _buySpeedSeries.Color = value;
                RecalculateValues();
            }
        }

        /// <summary>
        /// Histogram bar colour when seller-initiated volume rate exceeds buyer rate.
        /// </summary>
        [Display(Name = "Sell Color",
                 GroupName = "Visual",
                 Order = 3,
                 Description = "Bar colour when sellers dominate the tape.")]
        public Color SellColor
        {
            get => _sellColor;
            set
            {
                _sellColor = value;
                _sellSpeedSeries.Color = value;
                RecalculateValues();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initialises the indicator and registers the two histogram data series
        /// (buy-speed and sell-speed) in a dedicated chart panel.
        /// </summary>
        public SpeedOfTape()
        {
            // Place this indicator in a new panel below the main chart.
            Panel = IndicatorDataProvider.NewPanel;

            // Buy-side series — positive values, rendered green.
            _buySpeedSeries = new ValueDataSeries("Buy Speed")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _buyColor,
                ShowZeroValue = false,
            };

            // Sell-side series — negative values, rendered purple.
            _sellSpeedSeries = new ValueDataSeries("Sell Speed")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _sellColor,
                ShowZeroValue = false,
            };

            // DataSeries[0] is the primary (default) series created by the base class.
            // Replace it with our buy series; then append the sell series.
            DataSeries[0] = _buySpeedSeries;
            DataSeries.Add(_sellSpeedSeries);
        }

        // ------------------------------------------------------------------ //
        //  Core calculation                                                    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called by the ATAS engine once per bar (and on real-time ticks for the
        /// current bar).  Computes the net tape speed over the rolling window and
        /// writes the result to the appropriate histogram series.
        /// </summary>
        /// <param name="bar">
        /// Zero-based index of the bar being (re)calculated.
        /// </param>
        /// <param name="value">
        /// Close price of the bar; not used here — we read order-flow fields
        /// directly from <see cref="GetCandle"/>.
        /// </param>
        protected override void OnCalculate(int bar, decimal value)
        {
            // On the very first bar reset both series so stale data is flushed
            // when the indicator is applied to a new chart or the period changes.
            if (bar == 0)
            {
                _buySpeedSeries.Clear();
                _sellSpeedSeries.Clear();
                return;
            }

            // Determine the start of the rolling window (clamped to bar 0).
            int startBar = Math.Max(0, bar - _period + 1);

            decimal totalBuyVolume  = 0m;
            decimal totalSellVolume = 0m;
            double  totalSeconds    = 0d;

            for (int i = startBar; i <= bar; i++)
            {
                IndicatorCandle candle = GetCandle(i);
                if (candle is null)
                    continue;

                // candle.Ask = volume traded at the ask (buyer-initiated / lift)
                // candle.Bid = volume traded at the bid (seller-initiated / hit)
                totalBuyVolume  += candle.Ask;
                totalSellVolume += candle.Bid;

                // Measure this bar's duration so we can normalise to per-second rate.
                double barSeconds = (candle.LastTime - candle.Time).TotalSeconds;
                if (barSeconds > 0d)
                    totalSeconds += barSeconds;
            }

            // Guard: tick charts or very first bars may have zero elapsed time;
            // fall back to treating the whole window as 1 second to avoid division
            // by zero while still returning a meaningful relative magnitude.
            if (totalSeconds <= 0d)
                totalSeconds = 1d;

            decimal buySpeed  = totalBuyVolume  / (decimal)totalSeconds; // contracts/sec
            decimal sellSpeed = totalSellVolume / (decimal)totalSeconds; // contracts/sec
            decimal netSpeed  = buySpeed - sellSpeed;                    // signed delta

            if (netSpeed >= 0m)
            {
                // Buyers faster → positive green bar.
                _buySpeedSeries[bar]  = netSpeed;
                _sellSpeedSeries[bar] = 0m;
            }
            else
            {
                // Sellers faster → negative purple bar.
                _buySpeedSeries[bar]  = 0m;
                _sellSpeedSeries[bar] = netSpeed; // already negative
            }
        }
    }
}
