namespace SehensWerte.Maths
{
    public class Statistics
    {
        public double LastInput;
        public double Sum;
        public double SumSquares;
        public double SumCodeviates;
        public int Count;
        public double Min;
        public double Max;

        public double Range => Max - Min;
        public double Average => Count <= 0 ? 0.0 : Sum / Count;
        public double Rms => Math.Sqrt(SumSquares / Count);
        public double StdDev => Math.Sqrt(Variance);

        public double Variance
        {
            get
            {
                double result = 0.0;
                if (Count > 0 && Range != 0.0 && SumSquares > 0.0)
                {
                    double mean = Sum / (double)Count;
                    result = SumSquares / (double)Count - mean * mean;
                }
                return result;
            }
        }

        public Statistics() { }

        public Statistics(double[] input)
        {
            Insert(input);
        }

        public void Insert(double[] input)
        {
            foreach (double value in input)
            {
                Insert(value);
            }
        }

        public double Insert(double input)
        {
            LastInput = input;
            if (Count > 0)
            {
                Min = (input < Min) ? input : Min;
                Max = (input > Max) ? input : Max;
            }
            else
            {
                Min = (Max = input);
            }
            Sum += input;
            SumSquares += input * input;
            SumCodeviates += input * (double)Count;
            Count++;
            return LastInput;
        }

        public double Insert(Statistics stat)
        {
            LastInput = stat.LastInput;
            if (Count > 0)
            {
                Min = ((stat.Min < Min) ? stat.Min : Min);
                Max = ((stat.Max > Max) ? stat.Max : Max);
            }
            else
            {
                Min = stat.Min;
                Max = stat.Max;
            }
            Sum += stat.Sum;
            SumSquares += stat.SumSquares;
            SumCodeviates = stat.SumCodeviates;
            Count += stat.Count;
            return LastInput;
        }

        public override string ToString()
        {
            return $"Min={Min.ToStringRound(5, 3)}, Max={Max.ToStringRound(5, 3)}, Avg={Average.ToStringRound(5, 3)}, Range={Range.ToStringRound(5, 3)}, " +
                   $"Std={StdDev.ToStringRound(5, 3)}, RMS={Rms.ToStringRound(5, 3)}, " +
                   $"Sum={Sum.ToStringRound(5, 3)}, Count={Count}, LastInput={LastInput.ToStringRound(5, 3)}";
        }

        public class LinearResult
        {
            public double RSquared;
            public double Intercept;
            public double Slope;
            public override string ToString()
            {
                return $"r^2={RSquared.ToStringRound(5, 3)}, I={Intercept.ToStringRound(5, 3)}, slope={Slope.ToStringRound(5, 3)}";
            }
        }

        public LinearResult LinearRegression
        {
            get
            {
                double count = Count;
                double countm1 = Count - 1;
                double sumofx = count / 2.0 * countm1;
                double sumofx2 = countm1 * (1.0 + countm1) * (1.0 + 2.0 * countm1) / 6.0;
                double ssx = sumofx2 - sumofx * sumofx / (double)Count;
                double rnumerator = (double)Count * SumCodeviates - sumofx * Sum;
                double rdenominator = ((double)Count * sumofx2 - sumofx * sumofx) * ((double)Count * SumSquares - Sum * Sum);
                double sco = SumCodeviates - sumofx * Sum / (double)Count;
                double meanx = sumofx / (double)Count;
                double meany = Sum / (double)Count;
                double dblr = rnumerator / Math.Sqrt(rdenominator);

                return new LinearResult
                {
                    RSquared = dblr * dblr,
                    Intercept = meany - sco / ssx * meanx,
                    Slope = sco / ssx
                };
            }
        }
    }
}
