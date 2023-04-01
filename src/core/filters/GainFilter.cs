
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class GainFilter : Filter
    {
        private double m_Gain;

        public GainFilter(double gain)
        {
            m_Gain = gain;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = value * m_Gain;
        }

        public override double[] Insert(double[] values)
        {
            if (values.Length != 0)
            {
                m_LastInput = values[^1];
                var result = values.ElementProduct(m_Gain);
                m_LastOutput = result[^1];
                return result;
            }
            else
            {
                return new double[0];
            }
        }
    }
}
