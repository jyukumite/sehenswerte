using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace SehensWerte.Maths
{
    public class SimpleLQR
    { // essentially a PID by another name
        public double[] K;
        public double A = 1.0;
        public double B = 0.1;
        public double Q = 1.0;
        public double R = 0.1;
        public double MaxOut = 1.0;
        public double MinOut = -1.0;

        public double PrevState = 0.0;
        public double PrevInput = 0.0;
        public double PrevTarget = 0.0;
        public double IntegratedError = 0.0;
        public double PrevOutput = 0.0;

        private double m_SumXX = 1e-6;
        private double m_SumXU = 0.0;
        private double m_SumUU = 1e-6;
        private double m_SumX1X = 0.0;
        private double m_SumX1U = 0.0;
        private int m_Count = 0;

        public int GainUpdatePeriod = 5;
        public double GainSmoothing = 1.0;
        private double[] m_PrevK = new double[2];

        public SimpleLQR(double initialState = 0.0)
        {
            K = new double[2];
            m_PrevK = new double[2];
            Set(initialState);
        }

        public void Set(double initialState)
        {
            PrevState = initialState;
            PrevInput = 0.0;
            IntegratedError = 0.0;
            PrevOutput = 0.0;
            m_Count = 0;
        }

        private void UpdateGains()
        {
            if (Math.Abs(B) < 1e-6) return;

            double P = Q;
            for (int loop = 0; loop < 10; loop++)
            {
                double denom = R + B * B * P;
                double Pnew = Q + A * A * P - (A * B * P) * (A * B * P) / denom;
                P = Pnew;
            }

            double newK0 = (B * P) / (R + B * B * P);
            double newK1 = 0.1 * newK0;

            K[0] = (1 - GainSmoothing) * m_PrevK[0] + GainSmoothing * newK0;
            K[1] = (1 - GainSmoothing) * m_PrevK[1] + GainSmoothing * newK1;

            m_PrevK[0] = K[0];
            m_PrevK[1] = K[1];
        }

        public double Calc(double sensor, double target)
        {
            m_SumXX += PrevState * PrevState;
            m_SumXU += PrevState * PrevInput;
            m_SumUU += PrevInput * PrevInput;
            m_SumX1X += sensor * PrevState;
            m_SumX1U += sensor * PrevInput;
            m_Count++;

            if (m_Count >= 10)
            {
                double denom = (m_SumXX * m_SumUU - m_SumXU * m_SumXU);
                if (Math.Abs(denom) > 1e-6)
                {
                    A = (m_SumX1X * m_SumUU - m_SumX1U * m_SumXU) / denom;
                    B = (m_SumX1U * m_SumXX - m_SumX1X * m_SumXU) / denom;
                }
            }

            PrevState = sensor;
            PrevInput = PrevOutput;

            if (GainUpdatePeriod <= 1 || m_Count % GainUpdatePeriod == 0)
            {
                UpdateGains();
            }

            double error = target - sensor;
            IntegratedError += error;

            double u = K[0] * error + K[1] * IntegratedError;
            PrevOutput = Math.Clamp(u, MinOut, MaxOut);
            return PrevOutput;
        }
    }

    [TestClass]
    public class SimpleLQRTests
    {
        [TestMethod]
        public void SimplePlant()
        {
            var lqr = new SimpleLQR()
            {
                Q = 1.0,
                R = 0.1
            };

            double sensor = 0.0;
            double target = 1.0;
            double dt = 0.1;
            double throttle = 0.0;

            for (int loop = 0; loop < 100; loop++)
            {
                throttle = lqr.Calc(sensor, target);
                sensor = 0.9 * sensor + 0.2 * throttle;
            }

            Assert.IsTrue(Math.Abs(sensor - target) < 0.05);
        }

        [TestMethod]
        public void OutputClampedToBounds()
        {
            var lqr = new SimpleLQR()
            {
                K = new double[] { 10.0, 0 },
                MaxOut = 1.0,
                MinOut = -1.0,
            };

            double u = lqr.Calc(sensor: 0.0, target: 100.0);
            Assert.IsTrue(Math.Abs(u - 1.0) < 1e-6);
        }

        [TestMethod]
        public void NoCrashOnZeroB()
        {
            var lqr = new SimpleLQR() { B = 0.0 };
            lqr.Calc(0.0, 1.0);
            Assert.IsTrue(true);
        }
    }



    public class LQR
    {
        public double[,] A = new double[,] { { 1.0 } };
        public double[,] B = new double[,] { { 1.0 } };
        public double[,] Q = new double[,] { { 1.0 } };
        public double[,] R = new double[,] { { 1.0 } };
        public double[] K = new double[] { 0.0 };

        public double MaxOut = double.MaxValue;
        public double MinOut = double.MinValue;
        public double GainSmoothing = 1.0; // 1=all of the future, 0=all of the past
        public int GainUpdatePeriod = 1;

        private int m_Count = 0;
        private double[,]? P = null;

        public static (double[,] K, double[,] P) SolveLQR(double[,] A, double[,] B, double[,] Q, double[,] R, double tolerance = 1e-9, int maxIterations = 10)
        {
            int n = A.GetLength(0);
            double[,] P = Q.Copy();
            double[,]? K = null;

            for (int loop = 0; loop < maxIterations; loop++)
            {
                double[,] BT_P = B.Transpose().Product(P);
                double[,] BT_P_B = BT_P.Product(B);
                double[,] invTerm = (R.Add(BT_P_B)).Invert();
                double[,] BT_P_A = BT_P.Product(A);
                K = invTerm.Product(BT_P_A);  // K = (R + B^T P B)^-1 B^T P A

                double[,] A_minus_BK = A.Subtract(B.Product(K));
                double[,] P_next = A_minus_BK.Transpose().Product(P).Product(A_minus_BK).Add(Q);

                double diff = MatrixNorm(P_next.Subtract(P));
                P = P_next;

                if (diff < tolerance)
                {
                    break;
                }
            }

            if (K == null) throw new ArgumentException("Failed to find LQR gain K");
            return (K, P);
        }

        private static double MatrixNorm(double[,] M)
        {
            double norm = 0;
            for (int loop1 = 0; loop1 < M.RowCount(); loop1++)
            {
                for (int loop2 = 0; loop2 < M.ColumnCount(); loop2++)
                {
                    norm += M[loop1, loop2] * M[loop1, loop2];
                }
            }
            return Math.Sqrt(norm);
        }


        public LQR()
        {
            UpdateGain();  // initial gain
        }

        public double Calc(double[] sensor, double[] target)
        {
            if (sensor.Length != target.Length || sensor.Length != K.Length)
            {
                throw new ArgumentException("Sensor, target, and gain dimensions must match");
            }
            if (m_Count++ % GainUpdatePeriod == 0)
            {
                UpdateGain();
            }
            // u = -K * x, where x = (sensor - target)
            double u = 0.0;
            for (int i = 0; i < K.Length; i++)
            {
                double error = sensor[i] - target[i];
                u -= K[i] * error;
            }
            return Math.Clamp(u, MinOut, MaxOut);
        }

        private void UpdateGain()
        {
            var (newK, newP) = SolveLQR(A, B, Q, R);
            for (int i = 0; i < newK.ColumnCount(); i++)
            {
                if (i >= K.Length)
                {
                    Array.Resize(ref K, i + 1);
                }

                K[i] = (1 - GainSmoothing) * K[i] + GainSmoothing * newK[0, i];
            }

            P = newP;
        }
    }
}
