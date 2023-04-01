namespace SehensWerte.Filters
{
    public class NoiseAddFilter : Filter
    {
        private double m_Magnitude;
        private Random m_Source = new Random();
        public NoiseAddFilter(double magnitude) { m_Magnitude = magnitude; }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = value * (1.0 - m_Magnitude) + (m_Source.NextDouble() * 2.0 - 1.0) * m_Magnitude;
        }
    }
}
