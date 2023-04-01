namespace SehensWerte.Filters
{
    public class DrcFilter : RcFilter
    {
        public DrcFilter(double sampleRate, double r, double c) : base(sampleRate, r, c) { }
        public DrcFilter(double sampleRate, double tau) : base(sampleRate, tau) { }
        public DrcFilter(double coefficient) : base(coefficient) { }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = m_Charge = Math.Max(value, base.Insert(value));
        }
    }
}
