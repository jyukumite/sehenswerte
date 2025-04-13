using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Generators;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SehensWerte.Maths
{
    public class Fftw : IDisposable
    {
        private float[] m_TemporalComplexFloat;
        private float[] m_SpectralComplexFloat;

        private IntPtr m_TemporalComplexFloatPointer;
        private IntPtr m_SpectralComplexFloatPointer;
        private GCHandle m_TemporalComplexFloatHandle;
        private GCHandle m_SpectralComplexFloatHandle;

        private static object GeneratePlanLock = new object();
        private static Dictionary<int, IntPtr> m_ForwardPlans = new Dictionary<int, IntPtr>();
        private static Dictionary<int, IntPtr> m_BackwardPlans = new Dictionary<int, IntPtr>();

        private bool m_Disposed;

        private double[]? m_CacheSpectralReal;
        private double[]? m_CacheSpectralMagnitude;
        private double[]? m_CacheSpectralPhase;
        private double[]? m_CacheTemporalReal;
        private double[]? m_CacheTemporalMagnitude;

        private static IntPtr GetPlan(int width, Dictionary<int, IntPtr> dict, Import.Direction direction)
        {
            lock (GeneratePlanLock)
            {
                Import.fftw_plan_with_nthreads(Environment.ProcessorCount);
                IntPtr plan;
                if (!dict.TryGetValue(width, out plan))
                {
                    int complex = 2;
                    IntPtr inFloatPointer = Import.malloc(width * complex * sizeof(float));
                    IntPtr outFloatPointer = Import.malloc(width * complex * sizeof(float));
                    plan = Import.plan_dft_1d(width, inFloatPointer, outFloatPointer, direction, Import.Flags.Estimate);
                    Import.free(outFloatPointer);
                    Import.free(inFloatPointer);
                    dict[width] = plan;
                }
                return plan;
            }
        }

        private static IntPtr ForwardPlan(int width)
        {
            return GetPlan(width, m_ForwardPlans, Import.Direction.Forward);
        }

        private static IntPtr BackwardPlan(int width)
        {
            return GetPlan(width, m_BackwardPlans, Import.Direction.Backward);
        }

        public int Bins => SampleCountToBinCount(m_Width);

        private int m_Width;
        public int Width
        {
            get => m_Width;
            set { m_Width = value; ClearCache(); }
        }

        public float[] TemporalComplex
        {
            get => m_TemporalComplexFloat;
            set
            {
                if (value.Length != m_Width * 2) throw new NotSupportedException();
                ClearCache();
                for (int loop = 0; loop < m_Width * 2; loop++)
                {
                    m_TemporalComplexFloat[loop] = value[loop];
                }
            }
        }

        public float[] SpectralComplex
        {
            get => m_SpectralComplexFloat;
            set
            {
                if (value.Length != m_Width * 2 / 2) throw new NotSupportedException();
                ClearCache();
                for (int loop = 0; loop < m_Width * 2 / 2; loop++)
                {
                    m_SpectralComplexFloat[loop] = value[loop];
                }
            }
        }

        public double[] TemporalReal
        {
            get
            {
                if (m_CacheTemporalReal == null)
                {
                    m_CacheTemporalReal = new double[m_Width];
                    int realIndex = 0;
                    int complexIndex = 0;
                    while (realIndex < m_Width)
                    {
                        m_CacheTemporalReal[realIndex] = m_TemporalComplexFloat[complexIndex];
                        realIndex++;
                        complexIndex += 2;
                    }
                }
                return m_CacheTemporalReal;
            }
            set
            {
                if (value.Length != m_Width) throw new Exception("Length mismatch");
                ClearCache();
                int realIndex = 0;
                int complexIndex = 0;
                while (realIndex < m_Width)
                {
                    m_TemporalComplexFloat[complexIndex] = (float)value[realIndex];
                    m_TemporalComplexFloat[complexIndex + 1] = 0f;
                    realIndex++;
                    complexIndex += 2;
                }
            }
        }

        public double[] TemporalMagnitude
        {
            get
            {
                if (m_CacheTemporalMagnitude == null)
                {
                    m_CacheTemporalMagnitude = new double[m_Width];
                    int realIndex = 0;
                    int complexIndex = 0;
                    while (realIndex < m_Width)
                    {
                        double r = m_TemporalComplexFloat[complexIndex];
                        double i = m_TemporalComplexFloat[complexIndex + 1];
                        m_CacheTemporalMagnitude[realIndex] = Math.Sqrt(r * r + i * i);
                        realIndex++;
                        complexIndex += 2;
                    }
                }
                return m_CacheTemporalMagnitude;
            }
        }


        public double[] SpectralMagnitude
        {
            get
            {
                if (m_CacheSpectralMagnitude == null)
                {
                    int bins = Bins;
                    m_CacheSpectralMagnitude = new double[bins];
                    int realIndex = 0;
                    int complexIndex = 0;
                    while (realIndex < bins)
                    {
                        double r = m_SpectralComplexFloat[complexIndex];
                        double i = m_SpectralComplexFloat[complexIndex + 1];
                        m_CacheSpectralMagnitude[realIndex] = Math.Sqrt(r * r + i * i);
                        realIndex++;
                        complexIndex += 2;
                    }
                }
                return m_CacheSpectralMagnitude;
            }
        }

        public double[] SpectralPhase
        {
            get
            {
                if (m_CacheSpectralPhase == null)
                {
                    int bins = Bins;
                    m_CacheSpectralPhase = new double[bins];
                    int realIndex = 0;
                    int complexIndex = 0;
                    while (realIndex < bins)
                    {
                        double r = m_SpectralComplexFloat[complexIndex];
                        double i = m_SpectralComplexFloat[complexIndex + 1];
                        m_CacheSpectralPhase[realIndex] = Math.Atan2(y: i, x: r);
                        realIndex++;
                        complexIndex += 2;
                    }
                }
                return m_CacheSpectralPhase;
            }
        }

        public double[] SpectralReal
        {
            get
            {
                if (m_CacheSpectralReal == null)
                {
                    int bins = Bins;
                    m_CacheSpectralReal = new double[bins];
                    int realIndex = 0;
                    int complexIndex = 0;
                    while (realIndex < bins)
                    {
                        m_CacheSpectralReal[realIndex] = (double)m_SpectralComplexFloat[complexIndex];
                        realIndex++;
                        complexIndex += 2;
                    }
                }
                return m_CacheSpectralReal;
            }
            set
            {
                int bins = Bins;
                if (value.Length != bins) throw new Exception("Length mismatch");
                int loop = 0;
                int lhs = 0;
                int count = m_Width * 2;
                int rhs = count;

                while (loop < bins)
                {
                    m_SpectralComplexFloat[lhs] = (float)value[loop];
                    m_SpectralComplexFloat[lhs + 1] = 0f;
                    if (rhs != count)
                    {
                        m_SpectralComplexFloat[rhs] = (float)value[loop];
                        m_SpectralComplexFloat[rhs + 1] = 0f;
                    }
                    loop++;
                    lhs += 2;
                    rhs -= 2;
                }
            }
        }

        public void SetSpectral(double[] mag, double[] phase)
        {
            int count = m_Width * 2;
            int bins = Bins;
            if (mag.Length != phase.Length || phase.Length != bins) throw new Exception("Length mismatch");

            int lhs = 0;
            int rhs = count;
            for (int loop = 0; loop < bins; loop++)
            {
                float real = (float)(mag[loop] * Math.Cos(phase[loop]));
                float imag = (float)(mag[loop] * Math.Sin(phase[loop]));

                if (rhs != count && rhs != lhs)
                {
                    m_SpectralComplexFloat[rhs] = real;
                    m_SpectralComplexFloat[rhs + 1] = -imag; // complex conjugate
                }
                m_SpectralComplexFloat[lhs] = real;
                m_SpectralComplexFloat[lhs + 1] = imag;

                lhs += 2;
                rhs -= 2;
            }
        }

        public Fftw(double[] real) : this(real.Length)
        {
            ExecuteForward(real);
        }

        public Fftw(int width = 256)
        {
            int complex = 2;
            m_Width = width;
            m_TemporalComplexFloatPointer = Import.malloc(m_Width * complex * sizeof(float));
            m_SpectralComplexFloatPointer = Import.malloc(m_Width * complex * sizeof(float));
            m_TemporalComplexFloat = new float[m_Width * complex];
            m_SpectralComplexFloat = new float[m_Width * complex];
            m_TemporalComplexFloatHandle = GCHandle.Alloc(m_TemporalComplexFloat, GCHandleType.Pinned);
            m_SpectralComplexFloatHandle = GCHandle.Alloc(m_SpectralComplexFloat, GCHandleType.Pinned);
        }

        ~Fftw()
        {
            Dispose(disposing: false);
        }

        public static int SampleCountToBinCount(int samples)
        {
            return (samples + 1) / 2 + ((samples % 2 == 0) ? 1 : 0);
        }

        public double HzToBin(double hz, double samplesPerSecond)
        {
            return hz / (samplesPerSecond / Width);
        }

        public double HighestFrequency(double samplesPerSecond)
        { // aka nyquist
            return samplesPerSecond / Width * (Bins - 1);
        }

        public double HzPerBin(double samplesPerSecond)
        {
            double bins = Bins;
            if ((Bins & 1) == 0)
            {
                return samplesPerSecond / ((bins * 2) - 1);
            }
            else
            {
                return samplesPerSecond / ((bins - 1) * 2);
            }
        }

        private void ClearCache()
        {
            m_CacheSpectralReal = null;
            m_CacheSpectralMagnitude = null;
            m_CacheSpectralPhase = null;
            m_CacheTemporalReal = null;
            m_CacheTemporalMagnitude = null;
        }

        public void ApplySpectralGain(double[] coefficients)
        {
            ClearCache();
            int length = m_SpectralComplexFloat.Length;
            int bins = Bins;
            int rhs = length; // skip DC
            int lhs = 0;
            for (int loop = 0; loop < bins; loop++, lhs += 2, rhs -= 2)
            {
                float amplitude = ((loop < coefficients.Length) ? (float)coefficients[loop] : 0);
                m_SpectralComplexFloat[lhs] *= amplitude;
                m_SpectralComplexFloat[lhs + 1] *= amplitude;
                if (rhs > lhs && rhs < length)
                {
                    m_SpectralComplexFloat[rhs] *= amplitude; // reverse side
                    m_SpectralComplexFloat[rhs + 1] *= amplitude; // reverse side
                }
            }
        }

        public void ExecuteForward(double[] samples)
        {
            TemporalReal = samples;
            ExecuteForward();
        }

        public void ExecuteForward()
        {
            ClearCache();
            int complex = 2;
            Marshal.Copy(m_TemporalComplexFloat, 0, m_TemporalComplexFloatPointer, m_Width * complex);
            Import.fftw_execute_dft(ForwardPlan(m_Width), m_TemporalComplexFloatPointer, m_SpectralComplexFloatPointer);
            Marshal.Copy(m_SpectralComplexFloatPointer, m_SpectralComplexFloat, 0, m_Width * complex);
            for (int loop = 0; loop < m_Width * 2; loop++)
            {
                m_SpectralComplexFloat[loop] /= m_Width;
            }
        }

        public void ExecuteReverse()
        {
            ClearCache();
            int complex = 2;
            Marshal.Copy(m_SpectralComplexFloat, 0, m_SpectralComplexFloatPointer, m_Width * complex);
            Import.fftw_execute_dft(BackwardPlan(m_Width), m_SpectralComplexFloatPointer, m_TemporalComplexFloatPointer);
            Marshal.Copy(m_TemporalComplexFloatPointer, m_TemporalComplexFloat, 0, m_Width * complex);
        }

        public void SpectralPhaseShift(double radians)
        {
            if (radians == 0.0) return;
            ClearCache();

            int complex = 2;
            float sint = (float)Math.Sin(0.0 - radians);
            float cost = (float)Math.Cos(0.0 - radians);
            for (int loop = 0; loop < m_Width * complex; loop += complex)
            {
                float r = m_SpectralComplexFloat[loop];
                float i = m_SpectralComplexFloat[loop + 1];
                m_SpectralComplexFloat[loop] = r * cost - i * sint;
                m_SpectralComplexFloat[loop + 1] = r * sint + i * cost;
            }
        }

        public double[] GenerateFir(double samplesPerSecond, double[] fftCoefficients)
        {
            if (samplesPerSecond == 0) throw new NotImplementedException();
            float one = 0.5f;// (float)(1 / Math.Sqrt(2));// 1f;// 1f/ (float)Width;
            for (int loop = 0; loop < m_SpectralComplexFloat.Length; loop += 2)
            {
                m_SpectralComplexFloat[loop] = one;
                m_SpectralComplexFloat[loop + 1] = 0f;
            }
            ApplySpectralGain(fftCoefficients);
            ExecuteReverse();
            double[] temporalReal = TemporalReal;
            double[] array = new double[temporalReal.Length / 2];
            for (int loop = 0; loop < array.Length; loop++) // move so the center is at the middle of the FIR
            {
                array[loop] = temporalReal[(loop + temporalReal.Length * 3 / 4) % temporalReal.Length];
            }
            return array;
        }

        public double[] ExecuteFilter(double[] input, double[] coefficients)
        {
            TemporalReal = input;
            ExecuteForward();
            ApplySpectralGain(coefficients);
            ExecuteReverse();
            return TemporalReal;
        }

        public class Import
        {
            public enum Direction
            {
                Forward = -1,
                Backward = 1
            }

            [Flags]
            public enum Flags : uint
            {
                Measure = 0x0u,
                DestroyInput = 0x1u,
                Unaligned = 0x2u,
                ConserveMemory = 0x4u,
                Exhaustive = 0x8u,
                PreserveInput = 0x10u,
                Patient = 0x20u,
                Estimate = 0x40u
            }

            public enum Kind : uint
            {
                R2HC,
                HC2R,
                DHT,
                REDFT00,
                REDFT01,
                REDFT10,
                REDFT11,
                RODFT00,
                RODFT01,
                RODFT10,
                RODFT11
            }


            [DllImport("kernel32", SetLastError = true)]
            public static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_malloc", ExactSpelling = true)]
            public static extern IntPtr fftwf_malloc_32(int length);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_malloc", ExactSpelling = true)]
            public static extern IntPtr fftwf_malloc_64(int length);

            public static IntPtr malloc(int length)
            {
                return IntPtr.Size == 4
                    ? fftwf_malloc_32(length)
                    : fftwf_malloc_64(length);
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_free", ExactSpelling = true)]
            public static extern void fftwf_free_32(IntPtr mem);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_free", ExactSpelling = true)]
            public static extern void fftwf_free_64(IntPtr mem);

            public static void free(IntPtr mem)
            {
                if (IntPtr.Size == 4)
                {
                    fftwf_free_32(mem);
                }
                else
                {
                    fftwf_free_64(mem);
                }
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_destroy_plan", ExactSpelling = true)]
            public static extern void fftwf_destroy_plan_32(IntPtr plan);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_destroy_plan", ExactSpelling = true)]
            public static extern void fftwf_destroy_plan_64(IntPtr plan);

            public static void destroy_plan(IntPtr plan)
            {
                if (IntPtr.Size == 4)
                {
                    fftwf_destroy_plan_32(plan);
                }
                else
                {
                    fftwf_destroy_plan_64(plan);
                }
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_cleanup", ExactSpelling = true)]
            public static extern void fftwf_cleanup_32();

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_cleanup", ExactSpelling = true)]
            public static extern void fftwf_cleanup_64();

            public static void cleanup()
            {
                if (IntPtr.Size == 4)
                {
                    fftwf_cleanup_32();
                }
                else
                {
                    fftwf_cleanup_64();
                }
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_execute_dft", ExactSpelling = true)]
            public static extern void fftwf_execute_dft_32(IntPtr plan, IntPtr in_ptr, IntPtr out_ptr);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_execute_dft", ExactSpelling = true)]
            public static extern void fftwf_execute_dft_64(IntPtr plan, IntPtr in_ptr, IntPtr out_ptr);

            public static void fftw_execute_dft(IntPtr plan, IntPtr in_ptr, IntPtr out_ptr)
            {
                if (IntPtr.Size == 4)
                {
                    fftwf_execute_dft_32(plan, in_ptr, out_ptr);
                }
                else
                {
                    fftwf_execute_dft_64(plan, in_ptr, out_ptr);
                }
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_1d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_1d_32(int n, IntPtr input, IntPtr output, Direction direction, Flags flags);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_1d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_1d_64(int n, IntPtr input, IntPtr output, Direction direction, Flags flags);

            public static IntPtr plan_dft_1d(int n, IntPtr input, IntPtr output, Direction direction, Flags flags)
            {
                return IntPtr.Size == 4
                    ? fftwf_plan_dft_1d_32(n, input, output, direction, flags)
                    : fftwf_plan_dft_1d_64(n, input, output, direction, flags);
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_2d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_2d_32(int nx, int ny, IntPtr input, IntPtr output, Direction direction, Flags flags);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_2d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_2d_64(int nx, int ny, IntPtr input, IntPtr output, Direction direction, Flags flags);

            public static IntPtr plan_dft_2d(int nx, int ny, IntPtr input, IntPtr output, Direction direction, Flags flags)
            {
                return IntPtr.Size == 4
                    ? fftwf_plan_dft_2d_32(nx, ny, input, output, direction, flags)
                    : fftwf_plan_dft_2d_64(nx, ny, input, output, direction, flags);
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_3d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_3d_32(int nx, int ny, int nz, IntPtr input, IntPtr output, Direction direction, Flags flags);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_3d", ExactSpelling = true)]
            public static extern IntPtr fftwf_plan_dft_3d_64(int nx, int ny, int nz, IntPtr input, IntPtr output, Direction direction, Flags flags);

            public static IntPtr plan_dft_3d(int nx, int ny, int nz, IntPtr input, IntPtr output, Direction direction, Flags flags)
            {
                return IntPtr.Size == 4
                    ? fftwf_plan_dft_3d_32(nx, ny, nz, input, output, direction, flags)
                    : fftwf_plan_dft_3d_64(nx, ny, nz, input, output, direction, flags);
            }

            [DllImport("libfftw3f-3_32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftw_plan_with_nthreads", ExactSpelling = true)]
            public static extern IntPtr fftw_plan_with_nthreads_32(int n);

            [DllImport("libfftw3f-3_64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "fftwf_plan_dft_3d", ExactSpelling = true)]
            public static extern IntPtr fftw_plan_with_nthreads_64(int n);

            public static void fftw_plan_with_nthreads(int n)
            {
                if (IntPtr.Size == 4)
                {
                    fftw_plan_with_nthreads_32(n);
                }
                else
                {
                    fftw_plan_with_nthreads_64(n);
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_Disposed)
            {
                FreeFftw();
                m_Disposed = true;
            }
        }

        public static void FreeFftwLibrary()
        {
            Process currentProcess = Process.GetCurrentProcess();
            try
            {
                foreach (ProcessModule item in (ReadOnlyCollectionBase)currentProcess.Modules)
                {
                    if (item.FileName?.Contains("libfftw3f") ?? false)
                    {
                        Import.FreeLibrary(item.BaseAddress);
                    }
                }
            }
            finally
            {
                ((IDisposable)currentProcess)?.Dispose();
            }
        }

        private void FreeFftw()
        {
            lock (GeneratePlanLock)
            {
                Import.free(m_TemporalComplexFloatPointer);
                Import.free(m_SpectralComplexFloatPointer);
                m_TemporalComplexFloatHandle.Free();
                m_SpectralComplexFloatHandle.Free();
            }
        }
    }

    [TestClass]
    public class FftWTest
    {
        [TestMethod]
        public void TestFftwBins()
        {
            Assert.AreEqual(new Fftw(8).Width, 8);
            Assert.AreEqual(new Fftw(8).Bins, 5); // dc,1,2,3,nyquist
            Assert.AreEqual(Fftw.SampleCountToBinCount(8), 5); // dc,1,2,3,nyquist [removes ,3,2,1]
            Assert.AreEqual(new Fftw(9).Bins, 5);
            Assert.AreEqual(Fftw.SampleCountToBinCount(9), 5); // dc,1,2,3,4 [removes ,3,2,1]
            Assert.AreEqual(new Fftw(1024).HighestFrequency(samplesPerSecond: 44100), 22050, delta: 0.01);
            Assert.AreEqual(new Fftw(1024).HzToBin(hz: 129.2, samplesPerSecond: 44100), 3, delta: 0.01);

        }

        [TestMethod]
        public void TestFftwForward()
        {
            bool nearlyEqual(double[] a, double[] b) => a.Length == b.Length && a.Subtract(b).All(a => a < 0.025);
            bool nearlyEqualF(float[] a, double[] b) => a.Length == b.Length && a.Select(x => (double)x).ToArray().Subtract(b).All(a => a < 0.025);

            var realSinc8 = WaveformGenerator.SinCardinal(samples: 8, amplitude: 1, leftTime: -1, rightTime: 1, halfWidthTime: 0.5, delayTime: 0, baseline: 0);
            Assert.IsTrue(nearlyEqual(realSinc8, new double[] { 0, -0.21, 0, 0.64, 1, 0.64, 0, -0.21 }));

            var fft = new Fftw(realSinc8);
            Assert.AreEqual(fft.Bins, 5);
            Assert.AreEqual(fft.Width, 8);

            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 0, -0.21, 0, 0.64, 1, 0.64, 0, -0.21 }));
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 0, 0.21, 0, 0.64, 1, 0.64, 0, 0.21 })); // all positive
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 0, 0, -0.21, 0, 0, 0, 0.64, 0, 1, 0, 0.64, 0, 0, 0, -0.21, 0 }));
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 0.23, 0, -0.28, 0, 0.13, 0, 0.03, 0, 0.02, 0, 0.03, 0, 0.13, 0, -0.28, 0 })); // sinc causes all real
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 0.23, 0.28, 0.13, 0.03, 0.02 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 0.23, 0.28, 0.13, 0.03, 0.02 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { 0, Math.PI, 0, 0, 0 }));

            double[] realTest8 = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            fft = new Fftw(realTest8);// [36,0],[-4,9.66],[-4,4],[-4,1.66],[-4,0],[-4,-1.66],[-4,-4],[-4,-9.66] /width
            Assert.AreEqual(fft.Bins, 5);
            Assert.AreEqual(fft.Width, 8);
            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0, 8, 0 }));
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 4.5, 0, -0.5, 1.21, -0.5, 0.5, -0.5, 0.21, -0.5, 0, -0.5, -0.21, -0.5, -0.5, -0.5, -1.21 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 4.5, 1.31, 0.71, 0.54, 0.5 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 4.5, -0.5, -0.5, -0.5, -0.5 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { 0, 1.96, 2.36, 2.75, 3.14 }));

            fft.SpectralPhaseShift(Math.PI / 2);
            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 })); //unchanged
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0, 8, 0 })); //unchanged
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 0, -4.5, 1.21, 0.5, 0.5, 0.5, 0.21, 0.5, 0, 0.5, -0.21, 0.5, -0.5, 0.5, -1.21, 0.5 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 4.5, 1.31, 0.71, 0.54, 0.5 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 0, 1.21, .5, .21, 0 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { -1.57, 0.4, 0.79, 1.18, 1.57 }));

            fft.ExecuteReverse();
            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 })); //unchanged
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 0, -1, 0, -2, 0, -3, 0, -4, 0, -5, 0, -6, 0, -7, 0, -8 }));
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 0, -4.5, 1.21, 0.5, 0.5, 0.5, 0.21, 0.5, 0, 0.5, -0.21, 0.5, -0.5, 0.5, -1.21, 0.5 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 4.5, 1.31, 0.71, 0.54, 0.5 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 0, 1.21, .5, .21, 0 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { -1.57, 0.4, 0.79, 1.18, 1.57 })); //unchanged

            fft.ApplySpectralGain(new double[] { 2, 3, 4, 5, 6 });
            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 })); //unchanged
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 })); //unchanged
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 0, -1, 0, -2, 0, -3, 0, -4, 0, -5, 0, -6, 0, -7, 0, -8 })); //unchanged
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 0, -9, 3.62, 1.5, 2, 2, 1.04, 2.5, 0, 3, -1.04, 2.5, -2, 2, -3.62, 1.5 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 9, 3.92, 2.83, 2.71, 3 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 0, 3.62, 2, 1.04, 0 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { -1.57, 0.4, 0.79, 1.18, 1.57 })); //unchanged

            fft = new Fftw(8);
            fft.SetSpectral(new double[] { 4.5, 1.31, 0.71, 0.54, 0.5 }, new double[] { 0, 1.96, 2.36, 2.75, 3.14 });
            Assert.IsTrue(nearlyEqual(fft.SpectralMagnitude, new double[] { 4.5, 1.31, 0.71, 0.54, 0.5 }));
            Assert.IsTrue(nearlyEqual(fft.SpectralPhase, new double[] { 0, 1.96, 2.36, 2.75, 3.14 }));
            Assert.IsTrue(nearlyEqualF(fft.SpectralComplex, new double[] { 4.5, 0, -0.5, 1.21, -0.5, 0.5, -0.5, 0.21, -0.5, 0, -0.5, -0.21, -0.5, -0.5, -0.5, -1.21 })); // including doubling of rounding errors
            fft.ExecuteReverse();
            Assert.IsTrue(nearlyEqual(fft.SpectralReal, new double[] { 4.5, -0.5, -0.5, -0.5, -0.5 }));
            Assert.IsTrue(nearlyEqual(fft.TemporalReal, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.IsTrue(nearlyEqual(fft.TemporalMagnitude, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.IsTrue(nearlyEqualF(fft.TemporalComplex, new double[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0, 7, 0, 8, 0 }));
        }

        [TestMethod]
        public void TestFftwFilter()
        {
            //fixme

            // public double[] GenerateFir(double samplesPerSecond, double[] fftCoefficients)
            // public double[] ExecuteFilter(double[] input, double[] coefficients)
        }
    }
}
