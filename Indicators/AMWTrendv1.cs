using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Regular ATR stops for long position; volatility stops for short position
    /// 
    ///
    /// TrueRange is defined as the maximum of the following:
    ///   High - Low
    ///   ABS(High - PreviousClose)
    ///   ABS(Low - PreviousClose)
    ///   AMWTrendv1
    /// </summary>
    public class AMWTrendv1 : BarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private bool _isLong;
        private IBaseDataBar _previousBar;

        private decimal min1;
        //private decimal atrLong;
        //private decimal atrShort;
        private decimal atrProxy;
        private decimal _amwTrend;

        ///private readonly int _periodL; 
        private readonly decimal _multipleL;

        ///private readonly int _periodS;
        private readonly decimal _multipleS;

        /// <summary>This indicator is used to smooth the TrueRange computation</summary>
        /// <remarks>This is not exposed publicly since it is the same value as this indicator, meaning
        /// that this '_smoother' computers the ATR directly, so exposing it publicly would be duplication</remarks>
        ///private readonly IndicatorBase<IndicatorDataPoint> _smoother;

        private IndicatorBase<IndicatorDataPoint> _smootherL;
        private IndicatorBase<IndicatorDataPoint> _smootherS;
        //private readonly IndicatorBase<IndicatorDataPoint> _amwTrend;

        /// <summary>
        /// Gets the true range which is the more volatile calculation to be smoothed by this indicator
        /// </summary>
        public IndicatorBase<IBaseDataBar> TrueRange { get; } //doesn't necessarily need to be public

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _smootherL.IsReady && _smootherS.IsReady;
        ///public override bool IsReady => _smootherS.IsReady;
        ///public override bool IsReady => Samples >= 2;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Creates a new amwTrend indicator using the specified periods, multiples, and moving average types
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="periodL">The smoothing period used to smooth the true range values for long position</param>
        /// <param name="multipleL">The the multiple for ATR of long position</param>
        /// <param name="movingAverageTypeL">The type of smoothing used to smooth the true range values for long positions</param>
        /// <param name="periodS">The smoothing period used to smooth the true range values for short position</param>
        /// <param name="multipleS">The the multiple for volatility measure of short position</param>
        /// <param name="movingAverageTypeS">The type of smoothing used to smooth the true range values for short positions</param>
        public AMWTrendv1(string name, int periodL = 20, decimal multipleL = 2.6m, MovingAverageType movingAverageTypeL = MovingAverageType.Wilders,
                            int periodS = 20, decimal multipleS = 2.9m, MovingAverageType movingAverageTypeS = MovingAverageType.Wilders)
            : base(name)
        {
            WarmUpPeriod = Math.Max(periodL, periodS);

            _multipleL = multipleL;
            _multipleS = multipleS;

            _smootherL = movingAverageTypeL.AsIndicator($"{name}_{movingAverageTypeL}", periodL);
            _smootherS = movingAverageTypeS.AsIndicator($"{name}_{movingAverageTypeS}", periodS);

            TrueRange = new FunctionalIndicator<IBaseDataBar>(name + "_TrueRange", currentBar =>
            {
                // in our ComputeNextValue function we'll just call the ComputeTrueRange
                var nextValue = ComputeTrueRange(_previousBar, currentBar);
                _previousBar = currentBar;
                return nextValue;
            }   // in our IsReady function we just need at least one sample
            , trueRangeIndicator => trueRangeIndicator.Samples >= 1
            );


        }

        /// <summary>
        /// Creates a new amwTrend indicator using the specified periods, multiples, and moving average types
        /// </summary>
        /// <param name="periodL">The smoothing period used to smooth the true range values for long position</param>
        /// <param name="multipleL">The the multiple for ATR of long position</param>
        /// <param name="movingAverageTypeL">The type of smoothing used to smooth the true range values for long positions</param>
        /// <param name="periodS">The smoothing period used to smooth the true range values for short position</param>
        /// <param name="multipleS">The the multiple for volatility measure of short position</param>
        /// <param name="movingAverageTypeS">The type of smoothing used to smooth the true range values for short positions</param>
        public AMWTrendv1(int periodL = 20, decimal multipleL = 2.6m, MovingAverageType movingAverageTypeL = MovingAverageType.Wilders,
                            int periodS = 20, decimal multipleS = 2.9m, MovingAverageType movingAverageTypeS = MovingAverageType.Wilders)
            : this($"amwTrend({periodL},{multipleL},{movingAverageTypeL},{periodS},{multipleS},{movingAverageTypeS})",
                        periodL, multipleL, movingAverageTypeL, periodS, multipleS, movingAverageTypeS)
        {
        }

        /// <summary>
        /// Computes the TrueRange from the current and previous trade bars
        ///
        /// TrueRange is defined as the maximum of the following:
        ///   High - Low
        ///   ABS(High - PreviousClose)
        ///   ABS(Low - PreviousClose)
        /// </summary>
        /// <param name="previous">The previous trade bar</param>
        /// <param name="current">The current trade bar</param>
        /// <returns>The true range</returns>
        public static decimal ComputeTrueRange(IBaseDataBar previous, IBaseDataBar current)
        {
            var range1 = current.High - current.Low;
            if (previous == null)
            {
                return range1;
            }

            var range2 = Math.Abs(current.High - previous.Close);
            var range3 = Math.Abs(current.Low - previous.Close);

            return Math.Max(range1, Math.Max(range2, range3));
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            TrueRange.Update(input);
            _smootherL.Update(input.Time, TrueRange);
            _smootherS.Update(input.Time, TrueRange);

            if (Samples == 1)
            {
                //_previousBar = input;
                _amwTrend = input.Close;
            }

            if (Samples == 2)
            {
                Init(input);
                //_previousBar = input;
                _amwTrend = atrProxy;
            }

            if (Samples >= 3)
            {
                if (_isLong)
                {
                    if (input.Close < _amwTrend)
                    {
                        _amwTrend = input.Close + (_multipleS * _smootherS);
                        _isLong = false;
                        min1 = input.Close;
                    }
                    else
                    {
                        atrProxy = input.Close - (_multipleL * _smootherL);
                        _amwTrend = Math.Max(_amwTrend, atrProxy);
                    }
                }
                else
                {
                    if (input.Close > _amwTrend)
                    {
                        _amwTrend = input.Close - (_multipleL * _smootherL);
                        _isLong = true;
                        min1 = input.Close;
                    }
                    else
                    {
                        min1 = Math.Min(input.Close, min1);
                        atrProxy = min1 + (_multipleS * _smootherS);
                        _amwTrend = Math.Min(_amwTrend, atrProxy);
                    }
                }
            }

            return _amwTrend;
        }

        /// <summary>
        /// Initialize the indicator values 
        /// </summary>
        private void Init(IBaseDataBar currentBar)
        {
            // init position
            _isLong = currentBar.Close >= _previousBar.Close;

            // init amwTrend
            if (_isLong)
            {
                atrProxy = currentBar.Close - (_multipleL * _smootherL);
            }
            else
            {
                atrProxy = currentBar.Close + (_multipleS * _smootherS);
            }
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _previousBar = null;
            _smootherL.Reset();
            _smootherS.Reset();
            TrueRange.Reset();
            //_amwTrend.Reset();
            base.Reset();
        }
    }
}