// =============================================================================
// SpeedOfTape.cs — ATAS Custom Indicator  v3.0
//
// For every bar:
//   buySpeed  = bar's Ask volume  ÷ bar duration (seconds)   → positive green bar
//   sellSpeed = bar's Bid volume  ÷ bar duration (seconds)   → positive purple bar
//
// Period controls a simple moving average applied to the raw per-bar speeds
// so the histogram is less spiky on noisy instruments.
// Set Period = 1 for raw (un-smoothed) single-bar speed.
//
// Build:   dotnet build SpeedOfTape.csproj -c Release
// Deploy:  copy bin\Release\net48\SpeedOfTape.dll
//          → %APPDATA%\ATAS Platform\CustomIndicators\
// =============================================================================

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace SpeedOfTapeIndicator
{
    /// <summary>
    /// Speed of Tape — displays buyer and seller tape speeds as independent positive
    /// histogram bars so their magnitudes can be compared directly.
    ///
    /// Each bar's speed is:  volume-at-ask-or-bid  ÷  bar-duration-in-seconds.
    /// The <see cref="Period"/> parameter applies an N-bar simple moving average to
    /// smooth the result without changing the per-bar calculation logic.
    /// </summary>
    [DisplayName("Speed of Tape")]
    [Description("Buyer and seller tape speed (contracts/sec) as positive bars. Green = buy, Purple = sell.")]
    [Category("Order Flow")]
    public class SpeedOfTape : Indicator
    {
        // ------------------------------------------------------------------ //
        //  Fields                                                              //
        // ------------------------------------------------------------------ //

        private int   _period   = 10;
        private Color _buyColor  = Colors.Green;
        private Color _sellColor = Color.FromRgb(160, 32, 240);   // purple/violet

        private readonly ValueDataSeries _buySpeedSeries;
        private readonly ValueDataSeries _sellSpeedSeries;

        // ------------------------------------------------------------------ //
        //  Properties                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Number of bars averaged together.  Period = 1 shows raw per-bar speed.
        /// Larger values smooth noise at the cost of lag.
        /// </summary>
        [Display(Name = "Period",
                 GroupName = "Settings",
                 Order = 1,
                 Description = "SMA smoothing window. 1 = raw per-bar speed, higher = smoother.")]
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

        /// <summary>Histogram colour for buyer-aggressed tape speed.</summary>
        [Display(Name = "Buy Color", GroupName = "Settings", Order = 2)]
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

        /// <summary>Histogram colour for seller-aggressed tape speed.</summary>
        [Display(Name = "Sell Color", GroupName = "Settings", Order = 3)]
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
        /// Registers both histogram series and places the indicator in a new panel.
        /// </summary>
        public SpeedOfTape()
        {
            Panel = IndicatorDataProvider.NewPanel;

            _buySpeedSeries = new ValueDataSeries("Buy Speed")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _buyColor,
                ShowZeroValue = false,
            };

            _sellSpeedSeries = new ValueDataSeries("Sell Speed")
            {
                VisualType    = VisualMode.Histogram,
                Color         = _sellColor,
                ShowZeroValue = false,
            };

            DataSeries[0] = _buySpeedSeries;
            DataSeries.Add(_sellSpeedSeries);
        }

        // ------------------------------------------------------------------ //
        //  Calculation                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called by ATAS for every bar and on each real-time tick for the live bar.
        ///
        /// Algorithm:
        ///   1. For each of the last <see cref="Period"/> bars (including <paramref name="bar"/>),
        ///      compute the individual bar's buy and sell speed from its own Ask/Bid volume
        ///      and its own wall-clock duration.
        ///   2. Average those per-bar speeds (simple moving average) and write the
        ///      result as a positive value to the appropriate series.
        /// </summary>
        /// <param name="bar">Zero-based bar index being (re)calculated.</param>
        /// <param name="value">Close price — unused; we read Ask/Bid directly.</param>
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                _buySpeedSeries.Clear();
                _sellSpeedSeries.Clear();
                return;
            }

            int     startBar  = Math.Max(0, bar - _period + 1);
            decimal sumBuy    = 0m;
            decimal sumSell   = 0m;
            int     count     = 0;

            for (int i = startBar; i <= bar; i++)
            {
                IndicatorCandle c = GetCandle(i);
                if (c is null) continue;

                // Duration of this individual bar.
                // Falls back to 1 s on tick charts or bars with no elapsed time.
                double secs = (c.LastTime - c.Time).TotalSeconds;
                if (secs <= 0d) secs = 1d;

                // Per-bar speeds:
                //   c.Ask = total volume executed at the ask in this bar (buyer-initiated)
                //   c.Bid = total volume executed at the bid in this bar (seller-initiated)
                sumBuy  += c.Ask / (decimal)secs;
                sumSell += c.Bid / (decimal)secs;
                count++;
            }

            if (count == 0)
            {
                _buySpeedSeries[bar]  = 0m;
                _sellSpeedSeries[bar] = 0m;
                return;
            }

            // Show only the dominant side; the other series is zeroed out
            decimal avgBuy  = sumBuy  / count;
            decimal avgSell = sumSell / count;

            if (avgBuy >= avgSell)
            {
                _buySpeedSeries[bar]  = avgBuy;
                _sellSpeedSeries[bar] = 0m;
            }
            else
            {
                _buySpeedSeries[bar]  = 0m;
                _sellSpeedSeries[bar] = avgSell;
            }
        }
    }
}
