using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    //function [errors,weights,results,P]=rls(lambda,taps,x,d,delta)
    //  weights=zeros(taps,1);
    //  P=eye(taps)/delta;
    //  x=x(:); d=d(:);
    //  width=length(x);
    //  results=zeros(width,1);
    //  errors=d;
    //  for loop=taps:width
    //    xvec=x(loop:-1:loop-taps+1);
    //    results(loop)=weights'*xvec;
    //    errors(loop)=d(loop)-results(loop);
    //    g=(1/lambda) * P * xvec / (1 + (1 / lambda) * xvec' * P * xvec);
    //    P=(1/lambda) * P - (1/lambda) * g * xvec' * P;
    //    weights=weights + g * errors(loop);
    //  end
    //endfunction

    public class RlsFilter : MultiplyAccumulateAdaptiveFilter
    {
        private double m_Lambda;
        private double[,] m_Matrix;

        public RlsFilter(int length) : this(length, 1.0, 0.005) { }

        //length: scales at o(n^2+n^3)
        //lambda: forgetting factor, e.g. 1.0
        //delta: regularization factor, e.g. 0.02
        public RlsFilter(int length, double lambda, double delta) : base(length)
        {
            m_Lambda = lambda;
            m_Matrix = new double[length, length].Eye().Divide(delta);
        }

        public override double Insert(double value, double desired)
        {
            m_LastInput = value;
            int length = History.Length;
            double error = desired - base.Insert(value);
            if (!m_Hold)
            {
                // g=(1/lambda) * P * xvec / (1 + (1 / lambda) * xvec' * P * xvec);
                double rlambda = 1 / m_Lambda;
                double[,] xvecC = History.Reversed().AsColumn();
                double[,] xvecR = xvecC.Transpose();
                double[,] Pxvec = m_Matrix.Product(xvecC);
                double[,] g = Pxvec.ElementProduct(rlambda).Divide(1 + rlambda * (xvecR.Product(Pxvec))[0, 0]);

                // P=(1/lambda) * P - (1/lambda) * g * xvec' * P;
                double[,] result = m_Matrix.ElementProduct(rlambda).Subtract(g.ElementProduct(rlambda).Product(xvecR).Product(m_Matrix));
                m_Matrix = result;

                // weights=weights + g * errors(loop);
                for (int loop = 0; loop < length; loop++)
                {
                    m_Coefficients[length - loop - 1] = m_Coefficients[length - loop - 1] + g[loop, 0] * error;
                }
                CheckLimiter();
            }
            return LastOutput;
        }
    }

    [TestClass]
    public class RLSFilterTests
    {
        [TestMethod]
        public void TestRls()
        {
            RlsFilter rls = new RlsFilter(4, 2, 0.02);
            var output = rls.Insert(new double[] { 4, 3, 2, 1 }, new double[] { 1, 2, 3, 4 });

            Action<double, double> test = (a, b) => Assert.IsTrue(Math.Abs(a - b) < 0.001);
            test(output[0], 0);
            test(output[1], 0.936914);
            test(output[2], 2.385421);
            test(output[3], 3.243571);
        }
    }

}
