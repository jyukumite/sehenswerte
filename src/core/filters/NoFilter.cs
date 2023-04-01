namespace SehensWerte.Filters
{
    public class NoFilter : Filter
    {
        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = value;
        }

        public override double[] Insert(double[] values)
        {
            if (values.Length != 0)
            {
                m_LastOutput = values[^1];
            }
            return values;
        }
    }
}
