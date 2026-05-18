// =============================================================================
// ATAS.Indicators.Stubs.cs — Compile-time type stubs for the ATAS SDK
//
// PURPOSE
//   These stub declarations reproduce just enough of the ATAS.Indicators public
//   surface to let SpeedOfTape.cs compile locally without a real ATAS installation.
//
// IMPORTANT — DO NOT deploy this DLL to ATAS
//   The stubs-compiled SpeedOfTape.dll will NOT load inside ATAS because the
//   CLR will resolve ATAS.Indicators types from the real ATAS.Indicators.dll
//   (different assembly version / strong-name).
//
//   To produce a deployable DLL you must:
//     1. Copy the real DLLs listed in SpeedOfTape.csproj into the libs\ folder.
//     2. Run:  dotnet build SpeedOfTape.csproj -c Release
//
// SDK DLLs to copy from your ATAS installation:
//   • ATAS.Indicators.dll
//   • OFT.Rendering.dll       (may be needed for CrossColor / rendering helpers)
//
// ATAS installation is typically at:
//   C:\Program Files\ATAS Platform\   (check Settings → About in the app for the path)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Windows.Media;

#pragma warning disable CS1591 // Missing XML comment — stubs only

namespace ATAS.Indicators
{
    // ------------------------------------------------------------------ //
    //  IDataSeries                                                         //
    // ------------------------------------------------------------------ //

    public interface IDataSeries
    {
        string Name { get; }
        void   Clear();
    }

    // ------------------------------------------------------------------ //
    //  DataSeriesList — exposes an indexer so DataSeries[0] = x compiles  //
    // ------------------------------------------------------------------ //

    public class DataSeriesList : List<IDataSeries>
    {
        public new IDataSeries this[int index]
        {
            get => base[index];
            set
            {
                while (Count <= index) Add(null);
                base[index] = value;
            }
        }
    }

    // ------------------------------------------------------------------ //
    //  VisualMode                                                          //
    // ------------------------------------------------------------------ //

    public enum VisualMode
    {
        Line,
        Histogram,
        Dots,
        Cross,
        UpArrow,
        DownArrow,
        Square,
        Circle,
        Triangle,
        Hash,
        Bars,
        Block,
        Candles,
        OnlyNumbers,
        LineWithDots,
        LastDot,
        DottedLine,
    }

    // ------------------------------------------------------------------ //
    //  ValueDataSeries                                                     //
    // ------------------------------------------------------------------ //

    public class ValueDataSeries : IDataSeries
    {
        private readonly List<decimal> _values = new List<decimal>();

        public string     Name          { get; }
        public VisualMode VisualType    { get; set; } = VisualMode.Line;
        public Color      Color         { get; set; } = Colors.White;
        public bool       ShowZeroValue { get; set; } = true;
        public int        Width         { get; set; } = 1;

        public ValueDataSeries(string name) { Name = name; }

        public decimal this[int index]
        {
            get
            {
                if (index < 0 || index >= _values.Count) return 0m;
                return _values[index];
            }
            set
            {
                while (_values.Count <= index) _values.Add(0m);
                _values[index] = value;
            }
        }

        public void Clear() => _values.Clear();
    }

    // ------------------------------------------------------------------ //
    //  IndicatorCandle — bar data including order-flow fields              //
    // ------------------------------------------------------------------ //

    public class IndicatorCandle
    {
        public decimal  Open     { get; set; }
        public decimal  High     { get; set; }
        public decimal  Low      { get; set; }
        public decimal  Close    { get; set; }
        public decimal  Volume   { get; set; }

        /// <summary>Volume of buyer-initiated trades (filled at the ask).</summary>
        public decimal  Ask      { get; set; }

        /// <summary>Volume of seller-initiated trades (filled at the bid).</summary>
        public decimal  Bid      { get; set; }

        public decimal  Delta    { get; set; }   // Ask - Bid
        public int      Ticks    { get; set; }   // trade count

        /// <summary>Timestamp of the first trade in this bar.</summary>
        public DateTime Time     { get; set; }

        /// <summary>Timestamp of the last trade in this bar.</summary>
        public DateTime LastTime { get; set; }
    }

    // ------------------------------------------------------------------ //
    //  Panel / IndicatorDataProvider                                       //
    // ------------------------------------------------------------------ //

    public class Panel { }

    public static class IndicatorDataProvider
    {
        public static Panel NewPanel { get; } = new Panel();
    }

    // ------------------------------------------------------------------ //
    //  Indicator — abstract base class                                     //
    // ------------------------------------------------------------------ //

    public abstract class Indicator
    {
        protected DataSeriesList DataSeries { get; } = new DataSeriesList { null };

        public Panel Panel { get; set; }

        /// <summary>Called by the engine for each bar recalculation.</summary>
        protected abstract void OnCalculate(int bar, decimal value);

        /// <summary>Returns the candle data for the given bar index.</summary>
        protected IndicatorCandle GetCandle(int bar) => new IndicatorCandle();

        /// <summary>Signals the engine to rerun OnCalculate for all bars.</summary>
        protected void RecalculateValues() { }
    }
}
