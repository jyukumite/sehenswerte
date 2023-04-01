namespace SehensWerte.Filters
{
    public class CrFilter : RcFilter
    {
        public CrFilter(double sampleRate, double r, double c) : base(sampleRate, r, c) { }
        public CrFilter(double sampleRate, double tau) : base(sampleRate, tau) { }
        public CrFilter(double coefficient) : base(coefficient) { }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = value - base.Insert(value);
        }
    }
}
