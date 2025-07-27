using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class KalmanFilter
    {
        private double[] m_PosFactor;
        private double[,] m_VelFactor;

        private double[,] m_Estimate;
        private double[,] m_Eye;
        private double[,] m_ErrorCovariance;
        public double[] PredictedState;
        private double[,] m_H;
        private double[,] m_Ht;

        private double m_FauxTime;

        public KalmanFilter(double[] velocityFactor, double[] positionFactor)
        {
            m_PosFactor = positionFactor;
            if (velocityFactor.Length != positionFactor.Length)
            {
                throw new Exception("Different lengths");
            }
            if (velocityFactor.Length < 1 && velocityFactor.Length > 3)
            {
                throw new Exception("1-3 dimensions only");
            }

            int length = velocityFactor.Length;

            m_Estimate = new double[length * 2, 1];
            m_Eye = new double[length * 2, length * 2].Eye();
            m_ErrorCovariance = new double[length * 2, length * 2];
            m_VelFactor = new double[length, length];
            for (int loop = 0; loop < length; loop++)
            {
                m_VelFactor[loop, loop] = velocityFactor[loop];
            }
            m_H = new double[length, length * 2];
            for (int loop = 0; loop < length; loop++)
            {
                m_H[loop, loop * 2] = 1.0;
            }
            m_Ht = m_H.Transpose();

            PredictedState = new double[length];
        }

        public double[] Insert(double[] new_state, double timeStep = 1.0)
        {
            int length = m_PosFactor.Length;
            if (new_state.Length != length)
            {
                throw new Exception("State vector incorrect length");
            }

            var gain = m_ErrorCovariance.Product(m_Ht).Product(m_H.Product(m_ErrorCovariance).Product(m_Ht).Add(m_VelFactor).Invert());
            m_Estimate = m_Estimate.Add(gain.Product(new_state.AsColumn().Subtract(m_H.Product(m_Estimate))));
            m_ErrorCovariance = m_Eye.Subtract(gain.Product(m_H)).Product(m_ErrorCovariance);

            for (int loop = 0; loop < new_state.Length; loop++)
            {
                PredictedState[loop] = m_Estimate[loop * 2, 0];
            }

            var phi = m_Eye.Copy();
            for (int loop = 0; loop < length; loop++)
            {
                phi[loop * 2, loop * 2 + 1] = timeStep;
            }
            m_Estimate = phi.Product(m_Estimate);

            double[,] projection = new double[length * 2, length * 2];
            for (int loop = 0; loop < length; loop++)
            {
                int row = loop * 2;
                int col = loop * 2 + 1;
                projection[row, row] = m_PosFactor[loop] * Math.Pow(timeStep, 4.0) / 4.0;
                projection[row, col] = m_PosFactor[loop] * Math.Pow(timeStep, 3.0) / 2.0;
                projection[col, row] = m_PosFactor[loop] * Math.Pow(timeStep, 3.0) / 2.0;
                projection[col, col] = m_PosFactor[loop] * Math.Pow(timeStep, 2.0) / 2.0;
            }

            m_ErrorCovariance = phi.Product(m_ErrorCovariance).Product(phi.Transpose()).Add(projection);
            return PredictedState;
        }

        public double[] PredictFuture(int N, double timeStep = 1.0)
        {
            int length = m_PosFactor.Length;
            var predictions = new double[N];

            var current = m_Estimate.Copy();
            var phi = m_Eye.Copy();
            for (int loop = 0; loop < length; loop++)
            {
                phi[loop * 2, loop * 2 + 1] = timeStep;
            }
            for (int loop = 0; loop < N; loop++)
            {
                current = phi.Product(current);
                predictions[loop] = current[0, 0];
            }
            return predictions;
        }
    }

    [TestClass]
    public class KalmanFilterTests
    {
        [TestMethod]
        public void TestKalman()
        {
            KalmanFilter kf = new KalmanFilter(velocityFactor: new double[3].Add(0.01), positionFactor: new double[3].Add(0.01));

            double[,] rot = new double[] { 0, .8, .4 }.RotationMatrix(Math.PI / 100);
            double[] vec = new double[] { 1, 0, 0 }.RotateAroundBy(new double[] { 45, 10, 45 }.DegreesToRadians());
            double[] pos = new double[] { 10, 50, 20 };

            Statistics error = new Statistics();
            for (int loop = 0; loop < 100; loop++)
            {
                double[] val = kf.Insert(pos);
                if (loop > 10)
                {
                    error.Insert(pos.Subtract(val).Magnitude());
                }
                pos = pos.Add(vec);
                vec = rot.Product(vec);
            }

            Action<double, double> test = (a, b) => Assert.IsTrue(Math.Abs(a - b) < 0.001);
            test(error.Rms, 0.028);

            //fixme: test PredictFuture
        }
    }
}

