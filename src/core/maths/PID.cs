using Core.filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Filters;

namespace SehensWerte.Maths
{
    public class PID
    {
        public bool Inverted = false;
        public bool ScaleByP = true;
        public double P = 1.0;
        public double I = 0.0;
        public double D = 0.0;
        public double PrevError;
        public double PrevPartP = 0.0;
        public double PrevPartI = 0.0;
        public double PrevPartD = 0.0;
        public double PrevSensor = 0.0;
        public double PrevTarget = 0.0;
        public double PrevOutput = 0.0;

        public double MaxI = double.MaxValue;
        public double MinI = double.MinValue;
        public double MaxOut = double.MaxValue;
        public double MinOut = double.MinValue;

        public double DFilter = 1.0;
        public double TargetDeltaMax = double.MaxValue;
        public double DiffFiltered = 0.0;

        public IFilter? InputFilter = null; // new SehensWerte.Filters.DelayFilter()


        public PID(double initialSensor = 0, double initialTarget = 0, double initialOutput = 0)
        {
            Set(initialSensor, initialTarget, initialOutput);
        }

        public void Set(double initialSensor, double initialTarget, double initialOutput)
        {
            PrevSensor = initialSensor;
            PrevTarget = initialTarget;
            PrevOutput = initialOutput;
            DiffFiltered = 0.0;
            PrevPartP = 0.0;
            PrevPartI = 0.0;
            PrevPartD = 0.0;
        }

        public double Calc(double sensor, double target)
        {
            sensor = InputFilter?.Insert(sensor) ?? sensor;

            double deltaTarget = target - PrevTarget;
            double limitedTarget = Math.Clamp(deltaTarget, -TargetDeltaMax, TargetDeltaMax) + PrevTarget;
            PrevTarget = limitedTarget;

            double error = Inverted ? sensor - limitedTarget : limitedTarget - sensor;
            PrevError = error;

            PrevPartP = P * error;

            double scaleID = ScaleByP ? P : 1.0;
            double newI = PrevPartI + scaleID * I * error;
            PrevPartI = Math.Clamp(newI, MinI, MaxI);

            double diff = Inverted ? (sensor - PrevSensor) : (PrevSensor - sensor);
            PrevSensor = sensor;

            DiffFiltered = DiffFiltered * (1.0 - DFilter) + (diff * DFilter);
            PrevPartD = scaleID * D * DiffFiltered;

            double result = (double)(PrevPartP + PrevPartI + PrevPartD);
            PrevOutput = Math.Clamp(result, MinOut, MaxOut);
            return PrevOutput;
        }

        public override string ToString()
        {
            return $"Sensor:{PrevSensor} Target:{PrevTarget} PartP:{PrevPartP} PartI:{PrevPartI} Output:{PrevOutput}";
        }
    }

    [TestClass]
    public class PIDTests
    {
        [TestMethod]
        public void ResponseTracksTarget()
        {
            var pid = new PID()
            {
                P = 0.5,
                I = 0.05,
                D = 0.1,
                MaxOut = 1.0,
                MinOut = -1.0,
                MaxI = 0.5,
                MinI = -0.5,
                TargetDeltaMax = 0.1
            };

            double sensor = 0;
            double target = 1.0;
            double dt = 0.1;
            double output = 0;
            for (int loop = 0; loop < 100; loop++)
            {
                output = pid.Calc(sensor, target);
                sensor += output * dt;
            }

            Assert.IsTrue(Math.Abs(sensor - target) < 0.05);
        }

        [TestMethod]
        public void IntegralWindupClamp()
        {
            var pid = new PID()
            {
                ScaleByP = false,
                P = 0.0,
                I = 0.1,
                MaxI = 5,
                MinI = -5
            };
            for (int loop = 0; loop < 100; loop++)
            {
                pid.Calc(0, 1.0);
            }

            Assert.IsTrue(Math.Abs(pid.PrevPartI - pid.MaxI) < 1e-6);
        }
    }

    [TestClass]
    public class RingUnitTests
    {
        [TestMethod]
        public void Test3()
        {
            var ring = new Ring<int>(3);
            ring.Insert(1);
            ring.Insert(2);
            ring.Insert(3);
            var outVal = ring.Insert(4);

            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(2, ring[0]);
            Assert.AreEqual(3, ring[1]);
            Assert.AreEqual(4, ring[2]);
            Assert.AreEqual(1, outVal);
        }

        [TestMethod]
        public void Test0()
        {
            var ring = new Ring<int>(0);
            var outVal = ring.Insert(42);
            Assert.AreEqual(0, ring.Count);
            Assert.AreEqual(42, outVal);
        }

        [TestMethod]
        public void Test1()
        {
            var ring = new Ring<int>(1);
            ring.Insert(100);
            var outVal = ring.Insert(200);
            Assert.AreEqual(1, ring.Count);
            Assert.AreEqual(200, ring[0]);
            Assert.AreEqual(100, outVal);
        }
    }
}
