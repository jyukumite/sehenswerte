using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    // second differential (e.g. acceleration) Kalman filter (see KalmanFilterV for simpler version)
    public class KalmanFilterA
    {
        private double[] m_PosFactor;
        private double[,] m_MeasurementNoise;
        private double[,] m_Estimate;
        private double[,] m_Eye;
        private double[,] m_ErrorCovariance;
        public double[] PredictedState;
        private double[,] m_H;
        private double[,] m_Ht;

        public KalmanFilterA(double[] processNoise, double[] measurementNoise)
        {
            if (processNoise.Length != measurementNoise.Length)
            {
                throw new Exception("Different lengths");
            }
            if (processNoise.Length < 1 || processNoise.Length > 3)
            {
                throw new Exception("1–3 dimensions only");
            }

            int dim = processNoise.Length;
            m_PosFactor = processNoise;

            int stateSize = dim * 3;
            m_Estimate = new double[stateSize, 1];
            m_Eye = new double[stateSize, stateSize].Eye();
            m_ErrorCovariance = new double[stateSize, stateSize];

            m_H = new double[dim, stateSize];
            for (int loop = 0; loop < dim; loop++)
            {
                m_H[loop, loop * 3] = 1.0;
            }

            m_Ht = m_H.Transpose();

            m_MeasurementNoise = new double[dim, dim];
            for (int loop = 0; loop < dim; loop++)
            {
                m_MeasurementNoise[loop, loop] = measurementNoise[loop];
            }

            PredictedState = new double[dim];
        }

        public double[] Insert(double[] newState, double timeStep = 1.0)
        {
            int dim = m_PosFactor.Length;
            int stateSize = dim * 3;
            if (newState.Length != dim)
            {
                throw new ArgumentOutOfRangeException("New state length mismatch");
            }

            // transition matrix
            double[,] phi = Phi(timeStep, dim);

            // process noise matrix Q
            var Q = new double[stateSize, stateSize];
            for (int loop = 0; loop < dim; loop++)
            {
                int x = loop * 3;
                Q[x, x] = m_PosFactor[loop] * Math.Pow(timeStep, 5) / 20.0;
                Q[x, x + 1] = m_PosFactor[loop] * Math.Pow(timeStep, 4) / 8.0;
                Q[x, x + 2] = m_PosFactor[loop] * Math.Pow(timeStep, 3) / 6.0;
                Q[x + 1, x] = m_PosFactor[loop] * Math.Pow(timeStep, 4) / 8.0;
                Q[x + 1, x + 1] = m_PosFactor[loop] * Math.Pow(timeStep, 3) / 3.0;
                Q[x + 1, x + 2] = m_PosFactor[loop] * Math.Pow(timeStep, 2) / 2.0;
                Q[x + 2, x] = m_PosFactor[loop] * Math.Pow(timeStep, 3) / 6.0;
                Q[x + 2, x + 1] = m_PosFactor[loop] * Math.Pow(timeStep, 2) / 2.0;
                Q[x + 2, x + 2] = m_PosFactor[loop] * Math.Pow(timeStep, 1);
            }

            // Predict
            m_Estimate = phi.Product(m_Estimate);
            m_ErrorCovariance = phi.Product(m_ErrorCovariance).Product(phi.Transpose()).Add(Q);

            // Update
            var y = newState.AsColumn().Subtract(m_H.Product(m_Estimate));
            var S = m_H.Product(m_ErrorCovariance).Product(m_Ht).Add(m_MeasurementNoise);
            var K = m_ErrorCovariance.Product(m_Ht).Product(S.Invert());

            m_Estimate = m_Estimate.Add(K.Product(y));

            var KH = K.Product(m_H);
            var I_KH = m_Eye.Subtract(KH);
            m_ErrorCovariance = I_KH.Product(m_ErrorCovariance).Product(I_KH.Transpose()).Add(K.Product(m_MeasurementNoise).Product(K.Transpose()));

            for (int loop = 0; loop < dim; loop++)
            {
                PredictedState[loop] = m_Estimate[loop * 3, 0];
            }

            return PredictedState;
        }

        private double[,] Phi(double timeStep, int dim)
        {
            var phi = m_Eye.Copy();
            for (int loop = 0; loop < dim; loop++)
            {
                int x = loop * 3;
                phi[x, x + 1] = Math.Pow(timeStep, 1);
                phi[x, x + 2] = 0.5 * Math.Pow(timeStep, 2);
                phi[x + 1, x + 2] = Math.Pow(timeStep, 1);
            }

            return phi;
        }


        public double[] PredictFuture1d(int count, double timeStep = 1.0)
        {
            int dim = m_PosFactor.Length;
            int stateSize = dim * 3;
            var predictions = new double[count];
            var current = m_Estimate.Copy();

            double[,] phi = Phi(timeStep, dim);
            for (int loop = 0; loop < count; loop++)
            {
                current = phi.Product(current);
                predictions[loop] = current[0, 0];
            }

            return predictions;
        }
    }

    [TestClass]
    public class KalmanFilterATests
    {
        [TestMethod]
        public void TestKalman()
        {
            var kf = new KalmanFilterA(processNoise: new double[3].Add(0.01), measurementNoise: new double[3].Add(0.01));

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
            test(error.Rms, 0.018);

            //fixme: test PredictFuture1d
        }
    }

}
