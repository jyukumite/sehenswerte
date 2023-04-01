namespace SehensWerte.Filters
{
    // nlms (https://www.keil.com/pack/doc/CMSIS_Dev/DSP/html/group__LMS__NORM.html)
    //    E = x[n]^2 + x[n-1]^2 + ... + x[n-numTaps+1]^2.    
    //    b[k] = b[k] + e[n] * (mu/E) * x[n-k],  for k=0, 1, ..., numTaps-1    
    // leaking
    //    b(k+1)=b(k)+2*mu*e*x(k)/((1+l)*sig)
    //    leak by alpha*(x(k)**2)+(1-alpha)*sig(k-1)
    //    w=w+mu/(a+uvec'*uvec)*uvec*e(n)

    public class NlmsFilter : MultiplyAccumulateAdaptiveFilter
    {
        public double Mu;

        private double m_Epsilon = 1E-07; // small non-0
        private double m_Alpha; // leak
        private double m_Sigma = 2.0; // for normalisation
        private double m_MuError;

        // alpha: 1.0 = traditional, 0.001 = slower convergeance but more reliable (leaking)
        public NlmsFilter(int length) : this(length, mu: 5E-05 * (double)length, alpha: 1.0)
        {
        }

        public NlmsFilter(int length, double mu, double alpha) : base(length)
        {
            Mu = mu;
            m_Alpha = alpha;
        }

        public new double Insert(double value, double desired)
        {
            double error = desired - Insert(value);
            if (!m_Hold)
            {
                double historySumSquare = HistorySumSquare;
                m_Sigma = (m_Alpha * historySumSquare) + ((1.0 - m_Alpha) * m_Sigma) + m_Epsilon;
                m_MuError = ClearCoefficientsOnOverflow(Mu * error / m_Sigma);
                double[] history = base.History;

                int length = History.Length;
                for (int loop = 0; loop < length; loop++)
                {
                    m_Coefficients[loop] += m_MuError * history[loop];
                }
            }
            CheckLimiter();
            return LastOutput;
        }
    }
}
