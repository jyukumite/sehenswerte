namespace SehensWerte.Maths
{
    public class RollingAverage
    {
        private double m_Distance;
        private double m_LastOutput;
        public double LastOutput => m_LastOutput;
        public double Distance => m_Distance;

        public RollingAverage(double distance)
        {
            m_Distance = distance;
        }

        public double Insert(double value)
        {
            m_LastOutput = (m_LastOutput * (m_Distance - 1.0) + value) / m_Distance;
            return m_LastOutput;
        }
    }
}
