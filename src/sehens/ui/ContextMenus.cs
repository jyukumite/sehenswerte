using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Files;
using SehensWerte.Filters;
using SehensWerte.Generators;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.Media;
using System.Security.Policy;

namespace SehensWerte.Controls.Sehens
{
    public class ContextMenus
    {
        private const int MinimumTestTraceSamples = 250_000;
        private const int MaximumTestTraceSamples = 10_000_000;

        // Weighted random length for bulk test traces: min..max, cubically biased toward the minimum.
        private static int RandomTestTraceSampleCount()
        {
            double weight = Random.Shared.NextDouble();
            double lowBiasedWeight = weight * weight * weight;
            return MinimumTestTraceSamples
                + (int)(lowBiasedWeight * (MaximumTestTraceSamples - MinimumTestTraceSamples + 1));
        }

        // Sine test data: `cycles` full cycles across the trace, so alignment and overlap are
        // visually obvious when traces are grouped.
        private static double[] TestSine(int count, double cycles, double amplitude = 1.0)
        {
            double[] result = new double[count];
            for (int loop = 0; loop < count; loop++)
            {
                result[loop] = amplitude * Math.Sin(2.0 * Math.PI * cycles * loop / count);
            }
            return result;
        }

        // Monotonic random-walk times for YT test traces (mean step ~0.5s, never zero).
        private static double[] RandomWalkTimes(int count, Random random)
        {
            double[] result = new double[count];
            double t = 0.0;
            for (int loop = 0; loop < count; loop++)
            {
                t += random.NextDouble() + 0.01;
                result[loop] = t;
            }
            return result;
        }

        // One small deterministic group per row of the horizontal-axis grouping taxonomy
        // (horizontal-axis-value-positioning-plan.md): aligned / ragged / gapped / incompatible
        // affine, time (rate, count, sample-offset), fake and real YT, FFT, log-X, and the
        // bad-axis warning. Distinct cycle counts per member make alignment errors obvious.
        // internal for the smoke test below.
        internal static void GenerateAxisTestMatrix(SehensControl scope)
        {
            scope.BeginUpdate();
            try
            {
                TraceView Add(string name, double[] samples, double sps = double.NaN)
                {
                    scope[name].Update(samples, sps);
                    TraceView view = scope[name].FirstView ?? throw new InvalidOperationException(name);
                    view.PaintMode = TraceView.PaintModes.PolygonDigital;
                    return view;
                }
                void Pair(string groupA, string groupB) => scope.GroupViews(new[] { groupA, groupB });

                // ax01 Stretch: no axes, SAME counts - sample numbers line up, no warning.
                // (Differing counts warn "mixed horizontal axes" - the shared sample-number
                // gutter would only be right for the leader; see the ax20 zoo.)
                Add("ax01 stretch A", TestSine(500, 3));
                Add("ax01 stretch B", TestSine(500, 5, 0.5));
                Pair("ax01 stretch A", "ax01 stretch B");

                // ax02 ValueAlign identical: same affine axis - overlap exactly
                Add("ax02 align A", TestSine(500, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");
                Add("ax02 align B", TestSine(500, 5, 0.5)).Samples.SetHorizontalAffine(0, 10, "rpm");
                Pair("ax02 align A", "ax02 align B");

                // ax03 ValueAlign ragged: same unit, B covers only the upper half of A's range
                Add("ax03 ragged full", TestSine(500, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");     // 0..5000
                Add("ax03 ragged upper", TestSine(250, 5, 0.5)).Samples.SetHorizontalAffine(250, 10, "rpm"); // 2500..5000
                Pair("ax03 ragged full", "ax03 ragged upper");

                // ax04 ValueAlign gap: same unit, non-overlapping ranges - a visible gap is correct
                Add("ax04 gap low", TestSine(200, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");   // 0..2000
                Add("ax04 gap high", TestSine(200, 5, 0.5)).Samples.SetHorizontalAffine(400, 10, "rpm"); // 4000..6000
                Pair("ax04 gap low", "ax04 gap high");

                // ax05 Incompatible: same shape, different units -> "mixed horizontal axes"
                Add("ax05 unit rpm", TestSine(500, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");
                Add("ax05 unit kph", TestSine(500, 5, 0.5)).Samples.SetHorizontalAffine(0, 5, "kph");
                Pair("ax05 unit rpm", "ax05 unit kph");

                // ax06 Incompatible: plain index grouped with a real axis -> warn
                Add("ax06 plain", TestSine(500, 3));
                Add("ax06 affine", TestSine(500, 5, 0.5)).Samples.SetHorizontalAffine(0, 10, "rpm");
                Pair("ax06 plain", "ax06 affine");

                // ax07 Time, same rate, different counts - align left, short ends early
                Add("ax07 time long", TestSine(5000, 3), sps: 1000);  // 0..5 s
                Add("ax07 time short", TestSine(2000, 5, 0.5), sps: 1000); // 0..2 s
                Pair("ax07 time long", "ax07 time short");

                // ax08 Time, different rates, same count - ragged by duration
                Add("ax08 rate fast", TestSine(2000, 3), sps: 1000); // 0..2 s
                Add("ax08 rate slow", TestSine(2000, 5, 0.5), sps: 250); // 0..8 s
                Pair("ax08 rate fast", "ax08 rate slow");

                // ax09 Time, same rate, B shifted by a SAMPLE offset - ragged right
                Add("ax09 shift base", TestSine(5000, 3), sps: 1000); // 0..5 s
                Add("ax09 shift late", TestSine(2000, 5, 0.5), sps: 1000).Samples.SetHorizontalAffine(2000, 1, ""); // 2..4 s
                Pair("ax09 shift base", "ax09 shift late");

                // ax10 Time + affine-"s": sps and an explicit seconds axis are compatible
                Add("ax10 sps seconds", TestSine(2000, 3), sps: 1000); // 0..2 s
                Add("ax10 affine seconds", TestSine(3000, 5, 0.5)).Samples.SetHorizontalAffine(0, 0.001, "s"); // 0..3 s
                Pair("ax10 sps seconds", "ax10 affine seconds");

                // ax11 Incompatible: lin-X grouped with log-X -> warn
                Add("ax11 lin", TestSine(500, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");
                TraceView ax11log = Add("ax11 log", TestSine(500, 5, 0.5));
                ax11log.Samples.SetHorizontalAffine(0, 10, "rpm");
                ax11log.LogHorizontal = TraceView.LogHorizontalMode.Log;
                Pair("ax11 lin", "ax11 log");

                // ax12 Log pair: identical ranges, both log-X - aligned
                TraceView ax12a = Add("ax12 log A", TestSine(500, 3));
                ax12a.Samples.SetHorizontalAffine(0, 10, "Hz");
                ax12a.LogHorizontal = TraceView.LogHorizontalMode.Log;
                TraceView ax12b = Add("ax12 log B", TestSine(500, 5, 0.5));
                ax12b.Samples.SetHorizontalAffine(0, 10, "Hz");
                ax12b.LogHorizontal = TraceView.LogHorizontalMode.Log;
                Pair("ax12 log A", "ax12 log B");

                // ax13 fake YT: sps + start time; B starts halfway through A - overlap by time
                Add("ax13 fakeyt A", TestSine(1000, 3), sps: 100).Samples.InputLeftmostUnixTime = 1_700_000_000; // 10 s
                Add("ax13 fakeyt B", TestSine(600, 5, 0.5), sps: 100).Samples.InputLeftmostUnixTime = 1_700_000_005; // 6 s, +5 s
                Pair("ax13 fakeyt A", "ax13 fakeyt B");

                // ax14 real YT: random-walk times, B lags into the second half of A
                Random random = new Random(12345); // deterministic so re-runs are comparable
                double[] walkA = RandomWalkTimes(800, random);
                double halfway = walkA[walkA.Length / 2];
                double[] walkB = RandomWalkTimes(500, random).Select(t => t + halfway).ToArray();
                scope["ax14 realyt A"].Update(walkA.Select(Math.Sin), walkA);
                scope["ax14 realyt B"].Update(walkB.Select(t => 0.5 * Math.Cos(t)), walkB);
                Pair("ax14 realyt A", "ax14 realyt B");

                // ax15 fake YT grouped with a plain trace
                Add("ax15 yt", TestSine(1000, 3), sps: 100).Samples.InputLeftmostUnixTime = 1_700_000_000;
                Add("ax15 plain", TestSine(400, 5, 0.5));
                Pair("ax15 yt", "ax15 plain");

                // ax16 FFT pair: two tones - Hz-aligned by the FFT painter's own path
                Add("ax16 fft 500Hz", ToneSamples(8000, 2048, 500), sps: 8000).MathType = TraceView.MathTypes.FFTMagnitude;
                Add("ax16 fft 1500Hz", ToneSamples(8000, 2048, 1500), sps: 8000).MathType = TraceView.MathTypes.FFTMagnitude;
                Pair("ax16 fft 500Hz", "ax16 fft 1500Hz");

                // ax17 FFT grouped with a plain trace -> warn
                Add("ax17 fft", ToneSamples(8000, 2048, 500), sps: 8000).MathType = TraceView.MathTypes.FFTMagnitude;
                Add("ax17 plain", TestSine(500, 3));
                Pair("ax17 fft", "ax17 plain");

                // ax18 YT grouped with FFT - the taxonomy's deepest mismatch
                Add("ax18 yt", TestSine(1000, 3), sps: 100).Samples.InputLeftmostUnixTime = 1_700_000_000;
                Add("ax18 fft", ToneSamples(8000, 2048, 500), sps: 8000).MathType = TraceView.MathTypes.FFTMagnitude;
                Pair("ax18 yt", "ax18 fft");

                // ax19 bad axis: invalid multiplier -> "(bad horizontal axis)" warning, sample fallback
                Add("ax19 bad multiplier", TestSine(500, 3)).Samples.SetHorizontalAffine(0, -5, "rpm");

                // ax20 window zoo: IDENTICAL source data, one member per view-window reshape, so
                // each override's effect is directly comparable. Offset N drops/trims the left
                // (source[N] at index 0), negative offset opens a left region, length trims or
                // extends the right. Pads fill only the regions the window creates (DC-shifted
                // data so the flat pads are visible; pad-right skips a last value of exactly 0).
                // The members' drawn counts differ, so the group deliberately carries the
                // "mixed horizontal axes" warning - the sample-number gutter fits only the leader.
                double[] ax20base = TestSine(500, 2.5, 0.4).Select(v => v + 0.5).ToArray();
                TraceView Window20(string name, int offset, int length, bool pads = false)
                {
                    TraceView v = Add(name, ax20base);
                    v.ViewOffsetOverride = offset;
                    v.ViewLengthOverride = length;
                    if (pads)
                    {
                        v.PadLeftWithFirstValue = true;
                        v.PadRightWithLastValue = true;
                    }
                    return v;
                }
                Add("ax20 base", ax20base);
                Window20("ax20 trim left", offset: 150, length: 350);
                Window20("ax20 trim right", offset: 0, length: 350);
                Window20("ax20 trim both", offset: 100, length: 300);
                Window20("ax20 slide", offset: -150, length: 0);            // zeros lead in, tail lost
                Window20("ax20 pad left", offset: -150, length: 650, pads: true);
                Window20("ax20 pad right", offset: 0, length: 650, pads: true);
                Window20("ax20 pad both", offset: -150, length: 800, pads: true);
                scope.GroupViews(new[]
                {
                    "ax20 base", "ax20 trim left", "ax20 trim right", "ax20 trim both",
                    "ax20 slide", "ax20 pad left", "ax20 pad right", "ax20 pad both",
                });

                // ax21 view length/offset on an affine group: B MOVES source samples 100..350 to
                // its start (view offset/length reshape the data; the axis does not follow), so it
                // reads 0..2500 rpm at the left of A's 0..5000, plus the "(Offset)" warning
                Add("ax21 window full", TestSine(500, 3)).Samples.SetHorizontalAffine(0, 10, "rpm");
                TraceView ax21crop = Add("ax21 window cropped", TestSine(500, 5, 0.5));
                ax21crop.Samples.SetHorizontalAffine(0, 10, "rpm");
                ax21crop.ViewOffsetOverride = 100;
                ax21crop.ViewLengthOverride = 250;
                Pair("ax21 window full", "ax21 window cropped");

                // ax22 view length/offset on a time group: B moves 2 s of a 5 s trace to its
                // start - drawn as 0..1 s alongside A
                Add("ax22 time full", TestSine(5000, 3), sps: 1000);
                TraceView ax22crop = Add("ax22 time cropped", TestSine(5000, 5, 0.5), sps: 1000);
                ax22crop.ViewOffsetOverride = 2000;
                ax22crop.ViewLengthOverride = 1000;
                Pair("ax22 time full", "ax22 time cropped");

                // ax23 EVERYTHING affine: offset+multiplier+unit AND a view window. Both share
                // value = 10 * (sample + 50) rpm; the window member draws source samples 100..350
                // and reads 500..3000 rpm inside the full member's 500..5500
                TraceView ax23full = Add("ax23 combo full", TestSine(500, 3));
                ax23full.Samples.SetHorizontalAffine(50, 10, "rpm");
                TraceView ax23win = Add("ax23 combo window", TestSine(500, 5, 0.5));
                ax23win.Samples.SetHorizontalAffine(50, 10, "rpm");
                ax23win.ViewOffsetOverride = 100;
                ax23win.ViewLengthOverride = 250;
                Pair("ax23 combo full", "ax23 combo window");

                // ax24 EVERYTHING time: sps + affine sample-offset AND a view window. Both share
                // value = (sample + 500)/1000 s; the window member shows 0.5..1.5 s of 0.5..5.5 s
                TraceView ax24full = Add("ax24 combo time full", TestSine(5000, 3), sps: 1000);
                ax24full.Samples.SetHorizontalAffine(500, 1, "");
                TraceView ax24win = Add("ax24 combo time window", TestSine(5000, 5, 0.5), sps: 1000);
                ax24win.Samples.SetHorizontalAffine(500, 1, "");
                ax24win.ViewOffsetOverride = 2000;
                ax24win.ViewLengthOverride = 1000;
                Pair("ax24 combo time full", "ax24 combo time window");

                // ax25 FFT of a real-YT trace: non-uniformly timed samples are RESAMPLED onto a
                // uniform grid (TraceData.InterpolateYT feeds ViewedSamplesInterpolatedAsDouble)
                // before the FFT. The grid rate comes from the SMALLEST positive time gap
                // (CalculateSamplesPerSecond), so the densest region loses nothing and sparse
                // stretches are upsampled - the walk's ~0.01 s min step gives ~50 Hz Nyquist.
                // The tone is in WALK time, surviving the resample as a clean ~0.3 Hz peak.
                // Grouped with the untransformed YT source for comparison.
                double[] walk25 = RandomWalkTimes(2048, random);
                double[] tone25 = walk25.Select(t => Math.Sin(2.0 * Math.PI * 0.3 * t)).ToArray();
                scope["ax25 yt source"].Update(tone25, walk25);
                scope["ax25 yt source"].FirstView!.PaintMode = TraceView.PaintModes.PolygonDigital;
                scope["ax25 yt fft"].Update(tone25, walk25);
                TraceView ax25fft = scope["ax25 yt fft"].FirstView ?? throw new InvalidOperationException("ax25");
                ax25fft.PaintMode = TraceView.PaintModes.PolygonDigital;
                ax25fft.MathType = TraceView.MathTypes.FFTMagnitude;
                Pair("ax25 yt source", "ax25 yt fft");

                // ax26 fake YT with DIFFERENT sample rates: alignment is by wall clock, so a
                // 100 sps and a 25 sps member line up by start time regardless of rate
                Add("ax26 fakeyt fast", TestSine(1000, 3), sps: 100).Samples.InputLeftmostUnixTime = 1_700_000_000;   // 10 s
                Add("ax26 fakeyt slow", TestSine(150, 5, 0.5), sps: 25).Samples.InputLeftmostUnixTime = 1_700_000_002; // 6 s, +2 s
                Pair("ax26 fakeyt fast", "ax26 fakeyt slow");

                // ax27 fake YT pads: paint-level for YT traces - the first/last value is held
                // flat to the edges of the visible time window (the array pads never run for YT)
                TraceView ax27early = Add("ax27 ytpad early", TestSine(1000, 3), sps: 100); // 0..10 s of 0..11
                ax27early.Samples.InputLeftmostUnixTime = 1_700_000_000;
                ax27early.PadRightWithLastValue = true;
                TraceView ax27late = Add("ax27 ytpad late", TestSine(600, 5, 0.5), sps: 100); // 5..11 s
                ax27late.Samples.InputLeftmostUnixTime = 1_700_000_005;
                ax27late.PadLeftWithFirstValue = true;
                Pair("ax27 ytpad early", "ax27 ytpad late");
            }
            finally
            {
                scope.EndUpdate();
            }

            double[] ToneSamples(double sps, int count, double frequencyHz)
            {
                return new ToneGenerator
                {
                    SamplesPerSecond = sps,
                    FrequencyStart = frequencyHz,
                    FrequencyEnd = frequencyHz,
                    Amplitude = 1.0,
                }.Generate(count);
            }
        }

        internal static void GenerateMathTestTraces(SehensControl scope)
        {
            scope.BeginUpdate();
            try
            {
                const double sps = 1000.0;
                scope["math src A"].Update(TestSine(2000, 3), sps);
                scope["math src B"].Update(TestSine(2000, 5, 0.5), sps);
                scope["math src fir"].Update(Enumerable.Repeat(1.0 / 31, 31)); // boxcar coefficients

                TraceView Math(TraceView.CalculatedTypes type, int sources,
                    TraceView.CalculatedTraceData? parameter = null, bool firSource = false)
                {
                    string name = "math " + type;
                    var view = new TraceView(scope, new TraceData(name), name);
                    if (parameter != null)
                    {
                        view.CalculatedParameter = parameter;
                    }
                    view.CalculatedSourceViews.Add(scope["math src A"].FirstView ?? throw new InvalidOperationException(name));
                    if (sources >= 2)
                    {
                        string second = firSource ? "math src fir" : "math src B";
                        view.CalculatedSourceViews.Add(scope[second].FirstView ?? throw new InvalidOperationException(name));
                    }
                    view.CalculateType = type; // last, like the Math menu - the setter arms the calc
                    return view;
                }

                Math(TraceView.CalculatedTypes.Abs, 1);
                Math(TraceView.CalculatedTypes.Normalised, 1);
                Math(TraceView.CalculatedTypes.Differentiate, 1);
                Math(TraceView.CalculatedTypes.Integrate, 1);
                Math(TraceView.CalculatedTypes.ProjectYTtoY, 1);
                Math(TraceView.CalculatedTypes.SubtractOffset, 1, new TraceView.CalculatedTraceDataOneDouble { Param = 0.25 });
                Math(TraceView.CalculatedTypes.ProductSimple, 1, new TraceView.CalculatedTraceDataOneDouble { Param = 2.0 });
                Math(TraceView.CalculatedTypes.PolyFilter, 1, new TraceView.CalculatedTraceDataOrder { Order = 5 });
                Math(TraceView.CalculatedTypes.Rescale, 1, new TraceView.CalculatedTraceDataMinMax { Min = 0, Max = 1 });
                Math(TraceView.CalculatedTypes.Quantize, 1, new TraceView.CalculatedTraceDataQuantise { Offset = 0.0, Scale = 4.0 });
                Math(TraceView.CalculatedTypes.RollingRMS, 1, new TraceView.CalculatedTraceDataWindow { Window = 50 });
                Math(TraceView.CalculatedTypes.RollingMean, 1, new TraceView.CalculatedTraceDataWindow { Window = 50 });
                Math(TraceView.CalculatedTypes.Resample, 1, new TraceView.CalculatedTraceDataCount { Count = 500 });
                Math(TraceView.CalculatedTypes.Atan2, 2);
                Math(TraceView.CalculatedTypes.Difference, 2);
                Math(TraceView.CalculatedTypes.Subtract, 2);
                Math(TraceView.CalculatedTypes.RescaledError, 2);
                Math(TraceView.CalculatedTypes.NormalisedError, 2);
                Math(TraceView.CalculatedTypes.FIR, 2, firSource: true);
                Math(TraceView.CalculatedTypes.Magnitude, 2);
                Math(TraceView.CalculatedTypes.Sum, 2);
                Math(TraceView.CalculatedTypes.Mean, 2);
                Math(TraceView.CalculatedTypes.Product, 2);
                // skipped: None (not a calc), PythonScript (not implemented)
            }
            finally
            {
                scope.EndUpdate();
            }
        }

        internal static void GenerateFilterTestTraces(SehensControl scope)
        {
            scope.BeginUpdate();
            try
            {
                TraceData noise = scope["filter src"];
                noise.Update(new NoiseGenerator().Generate(20000), 10000.0);
                foreach (string filter in FilterChoice.FilterNames)
                {
                    if (filter == "None") continue;
                    var view = new TraceView(scope, noise, "filter " + filter);
                    view.TraceFilter = filter;
                }
            }
            finally
            {
                scope.EndUpdate();
            }
        }

        private class NoiseTraceForm
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "Noise";
            [AutoEditor.DisplayName("Samples")]
            public int SampleCount = 10000;
            [AutoEditor.DisplayName("Samples per second")]
            public double SamplesPerSecond = 10000.0;
            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;
        }

        private class WaveformTraceForm
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "Waveform";
            [AutoEditor.DisplayName("Samples")]
            public int SampleCount = 10000;
            [AutoEditor.DisplayName("Samples per second")]
            public double SamplesPerSecond = 10000.0;
            [AutoEditor.DisplayName("Frequency")]
            public double Frequency = 1000.0;
            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;
            [AutoEditor.DisplayName("Phase (0-1)")]
            public double Phase = 0;
            [AutoEditor.DisplayName("Waveform")]
            public WaveformGenerator.Waveforms Waveform = WaveformGenerator.Waveforms.Sine;
            [AutoEditor.DisplayName("Use Sin function")]
            public bool UseSin = true;
            [AutoEditor.DisplayName("Window")]
            public SampleWindow.WindowType Window = SampleWindow.WindowType.Rectangular;
        }

        private class SincTraceForm : AutoEditorBase
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "Sinc";
            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;
            [AutoEditor.DisplayName("Delay")]
            public double Delay = 0;
            [AutoEditor.DisplayName("Offset")]
            public double Offset = 0;
            [AutoEditor.DisplayName("Samples")]
            public int Count
            {
                get => m_Count;
                set { m_Count = value; CalculateFrequency(); }
            }

            [AutoEditor.DisplayName("Samples per second")]
            public double SamplesPerSecond
            {
                get => m_SamplesPerSecond;
                set { m_SamplesPerSecond = value; CalculateFrequency(); }
            }
            [AutoEditor.DisplayName("Left time")]
            public double LeftTime
            {
                get => m_LeftTime;
                set { m_LeftTime = value; CalculateFrequency(); }
            }
            [AutoEditor.DisplayName("Right time")]
            public double RightTime
            {
                get => m_RightTime;
                set { m_RightTime = value; CalculateFrequency(); }
            }
            [AutoEditor.DisplayName("Half width time")]
            public double halfwidth
            {
                get => m_HalfWidthTime;
                set { m_HalfWidthTime = value; CalculateFrequency(); }
            }
            [AutoEditor.DisplayName("Frequency")]
            public double frequency
            {
                get => m_Frequency;
                set { m_Frequency = value; CalculateHalfwidth(); }
            }

            private void CalculateHalfwidth()
            {
                if (Updating) return;
                m_HalfWidthTime = m_Count == 0 || frequency == 0.0 ? 0.0 : (SamplesPerSecond * ((RightTime - LeftTime) / 2.0) / m_Count / frequency);
                UpdateControls?.Invoke();
            }

            private void CalculateFrequency()
            {
                if (Updating) return;
                m_Frequency = SamplesPerSecond == 0.0 || halfwidth == 0.0 ? 0.0 : (SamplesPerSecond * ((RightTime - LeftTime) / 2.0) / m_Count / halfwidth);
                UpdateControls?.Invoke();
            }

            [AutoEditor.Hidden]
            public int m_Count = 10000;
            [AutoEditor.Hidden]
            private double m_Frequency;
            [AutoEditor.Hidden]
            private double m_LeftTime = -10.0;
            [AutoEditor.Hidden]
            private double m_RightTime = 10.0;
            [AutoEditor.Hidden]
            private double m_SamplesPerSecond = 44100.0;
            [AutoEditor.Hidden]
            public double m_HalfWidthTime = 1.0;

        }

        private class FilterGenTraceForm
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "FIR Filter";

            public double SamplesPerSecond = 10000;

            public int Width = 256;

            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;

            [AutoEditor.DisplayName("FFT Filter Style")]
            public TraceView.FftFilterTypes FftFilterType = TraceView.FftFilterTypes.BandPass;

            [AutoEditor.DisplayName("FFT Bandpass Window")]
            public SampleWindow.WindowType FftBandpassWindow = SampleWindow.WindowType.RaisedCosine;

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("FFT Bandpass HPF 6dB Hz")]
            public double FftBandpassHPF6dB = 50.0;

            [AutoEditor.DisplayOrder(2)]
            [AutoEditor.DisplayName("FFT Bandpass HPF 3dB Hz")]
            public double FftBandpassHPF3dB = 300.0;

            [AutoEditor.DisplayOrder(3)]
            [AutoEditor.DisplayName("FFT Bandpass LPF 3dB Hz")]
            public double FftBandpassLPF3dB = 3000.0;

            [AutoEditor.DisplayOrder(4)]
            [AutoEditor.DisplayName("FFT Bandpass LPF 6dB Hz")]
            public double FftBandpassLPF6dB = 3500.0;
        }

        private class WindowTraceForm
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "Window";

            [AutoEditor.DisplayOrder(0)]
            [AutoEditor.DisplayName("Count")]
            public int Count = 10000;

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("Samples per second")]
            public double samplesPerSecond = 10000;

            [AutoEditor.DisplayOrder(2)]
            [AutoEditor.DisplayName("Window type")]
            public SampleWindow.WindowType Window = SampleWindow.WindowType.RaisedCosine;

            [AutoEditor.DisplayOrder(3)]
            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;

            [AutoEditor.DisplayOrder(4)]
            [AutoEditor.DisplayName("Offset")]
            public double Offset = 0;
        }

        private class SweepTraceInput
        {
            [AutoEditor.DisplayName("Name")]
            [AutoEditor.DisplayOrder(-1)]
            public string Name = "Sweep";

            [AutoEditor.DisplayOrder(0)]
            [AutoEditor.DisplayName("Count")]
            public int Count = 10000;

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("Samples per second")]
            public double SamplesPerSecond = 10000;

            [AutoEditor.DisplayOrder(2)]
            [AutoEditor.DisplayName("Start frequency")]
            public double FrequencyStart = 1000.0;

            [AutoEditor.DisplayOrder(3)]
            [AutoEditor.DisplayName("End frequency")]
            public double FrequencyEnd = 2000.0;

            [AutoEditor.DisplayOrder(4)]
            [AutoEditor.DisplayName("Sweeps per second")]
            public double SweepRate = 1.0;

            [AutoEditor.DisplayOrder(5)]
            [AutoEditor.DisplayName("Amplitude")]
            public double Amplitude = 1.0;

            [AutoEditor.DisplayOrder(6)]
            [AutoEditor.DisplayName("Waveform")]
            public WaveformGenerator.Waveforms Waveform = WaveformGenerator.Waveforms.Sine;

            [AutoEditor.DisplayOrder(7)]
            [AutoEditor.DisplayName("Use Sin function")]
            public bool UseSinFunction = true;
        }

        private class FilterForm
        {
            private TraceView m_View;
            public FilterForm(TraceView view)
            {
                m_View = view;
            }

            [AutoEditor.DisplayOrder(-2)]
            [AutoEditor.DisplayName("Filter")]
            [AutoEditor.Values(typeof(FilterChoice))]
            public string TraceFilter
            {
                get => m_View.TraceFilter;
                set { m_View.TraceFilter = value; }
            }

            [AutoEditor.DisplayOrder(-1)]
            [AutoEditor.DisplayName("Transform")]
            public TraceView.FilterTransforms FilterTransform
            {
                get => m_View.FilterTransform;
                set { m_View.FilterTransform = value; }
            }

            [AutoEditor.DisplayName("FFT Bandpass Window")]
            public SampleWindow.WindowType FftBandpassWindow
            {
                get => m_View.FftBandpassWindow;
                set { m_View.FftBandpassWindow = value; }
            }

            [AutoEditor.DisplayName("FFT Filter Style")]
            public TraceView.FftFilterTypes FftFilterType
            {
                get => m_View.FftFilterType;
                set { m_View.FftFilterType = value; }
            }

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("FFT Bandpass HPF 6dB Hz")]
            public double FftFilterHPF6dB
            {
                get => m_View.FftBandpassHPF6dB;
                set { m_View.FftBandpassHPF6dB = value; }
            }

            [AutoEditor.DisplayOrder(2)]
            [AutoEditor.DisplayName("FFT Bandpass HPF 3dB Hz")]
            public double FftFilterHPF3dB
            {
                get => m_View.FftBandpassHPF3dB;
                set { m_View.FftBandpassHPF3dB = value; }
            }

            [AutoEditor.DisplayOrder(3)]
            [AutoEditor.DisplayName("FFT Bandpass LPF 3dB Hz")]
            public double FftFilterLPF3dB
            {
                get => m_View.FftBandpassLPF3dB;
                set { m_View.FftBandpassLPF3dB = value; }
            }

            [AutoEditor.DisplayOrder(4)]
            [AutoEditor.DisplayName("FFT Bandpass LPF 6dB Hz")]
            public double FftFilterLPF6dB
            {
                get => m_View.FftBandpassLPF6dB;
                set { m_View.FftBandpassLPF6dB = value; }
            }
        }

        private class TriggeredSliceForm
        {
            [AutoEditor.DisplayOrder(0)]
            [AutoEditor.DisplayName("Trigger value")]
            public double TriggerValue = 0.1;

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("Pre-trigger samples")]
            public int PreTriggerSamples = 100;

            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("Post-trigger minimum samples")]
            public int PostTriggerMinimumSamples = 100;

            public enum TriggerPhase { Rising, Falling };
            [AutoEditor.DisplayOrder(1)]
            [AutoEditor.DisplayName("Phase")]
            public TriggerPhase Phase = TriggerPhase.Rising;

            //fixme? lpf?
        }

        private static SincTraceForm SincInfo = new SincTraceForm();
        private static SweepTraceInput SweepInfo = new SweepTraceInput();
        private static NoiseTraceForm NoiseInfo = new NoiseTraceForm();
        private static WaveformTraceForm WaveformInfo = new WaveformTraceForm();
        private static FilterGenTraceForm FilterGenInfo = new FilterGenTraceForm();
        private static WindowTraceForm WindowInfo = new WindowTraceForm();
        private static TriggeredSliceForm TriggeredSliceInfo = new TriggeredSliceForm();

        public static void AddContextMenus(List<ScopeContextMenu.MenuItem> contextMenu, List<ScopeContextMenu.EmbeddedMenu> embeddedContextMenu)
        {
            AddTraceEmbeddedMenu(embeddedContextMenu);
            AddDisplaySubMenu(contextMenu);
            AddGenerateSubMenu(contextMenu);
            AddDiagnosticSubMenu(contextMenu);
            AddFeaturesSubMenu(contextMenu);
            AddSortTracesSubMenu(contextMenu);
            AddTraceSubMenu(contextMenu);
            AddSkinSubMenu(contextMenu);
            AddRecolourSubMenu(contextMenu);
            AddTraceFilterSubMenu(contextMenu);
            AddMathSubMenu(contextMenu);

            void Swap(ref double a, ref double b) { double num = a; a = b; b = num; }

            (double left, double right) GetTimebaseTarget(ScopeContextMenu.DropDownArgs a)
            {
                double left = a.Views[0].Measure(a.Mouse.WipeTopLeft).IndexBeforeTrim / (double)a.Views[0].Measure(a.Mouse.WipeTopLeft).CountBeforeTrim;
                double right = a.Views[0].Measure(a.Mouse.WipeBottomRight).IndexBeforeTrim / (double)a.Views[0].Measure(a.Mouse.WipeBottomRight).CountBeforeTrim;
                if (left > right)
                {
                    Swap(ref left, ref right);
                }
                return (left, right);
            }

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Time match source",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                Clicked = (a) => (a.Scope.m_TimebaseLineupLeftX, a.Scope.m_TimebaseLineupRightX) = GetTimebaseTarget(a),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Time match target",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                Clicked = (a) =>
                {
                    double left1 = a.Scope.m_TimebaseLineupLeftX;
                    double right1 = a.Scope.m_TimebaseLineupRightX;
                    double delta1 = right1 - left1;
                    int inputSampleCount = a.Views[0].Samples.InputSampleCount;
                    (var left2, var right2) = GetTimebaseTarget(a);
                    double delta2 = right2 - left2;
                    double time = (delta2 == 0.0 || delta1 == 0.0) ? 1.0 : (delta2 / delta1);
                    a.Views[0].ViewLengthOverride = (int)Math.Round(inputSampleCount * time);
                    a.Views[0].ViewOffsetOverride = (int)Math.Round(left2 * inputSampleCount - left1 * inputSampleCount * time);
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Auto range",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = (PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope),
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.TraceAutoRange = !a.Scope.TraceAutoRange,
                GetStyle = (a) => a.Checked = a.Scope.TraceAutoRange,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Auto range all",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = (PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope),
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.AutoRangeAll(),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.R
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Auto range time",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = (PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope),
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.AutoRangeTimeAll(),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.AltCtrl,
                HotKeyCode = Keys.R
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Recalculate traces",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.CalculateBeforeZoomRequired(),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Screenshot",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.ScreenshotToClipboard(),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Hide controls",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.TraceListVisible = !a.Scope.TraceListVisible,
                GetStyle = (a) => a.Checked = !a.Scope.TraceListVisible,
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.AltCtrl,
                HotKeyCode = Keys.X
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Stop view updates",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.StopUpdates = !a.Scope.StopUpdates,
                GetStyle = (a) => a.Checked = a.Scope.StopUpdates,
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.None,
                HotKeyCode = Keys.Space
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "New trace (reference) - ",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Scope.DuplicateTraceView(a.Views[0]),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.D
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "New trace (copy) - ",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Scope.DuplicateTraceData(a.Views[0]),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Show selected displayed samples in Data Grid",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = ImportExport.ShowDataGridView,
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.CtrlShift,
                HotKeyCode = Keys.G
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Combine to new YT trace (copy) - ",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) =>
                {
                    var y = a.Views[0].Samples;
                    var t = a.Views[1].Samples;
                    string s = a.Scope.EnsureUnique($"y={y.Name} t={t.Name}",
                                    x => a.Scope.TryGetTrace(x) != null || a.Scope.TryGetView(x) != null);
                    a.Scope[s].Update(y.InputSamplesAsDouble, t.InputSamplesAsDouble);
                }
            });


            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "New trace (copy visible samples) - ",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) =>
                {
                    var view = a.Views[0];
                    string s = a.Scope.EnsureUnique(view.Samples.Name + " displayed",
                                    x => a.Scope.TryGetTrace(x) != null || a.Scope.TryGetView(x) != null);
                    if (view.DrawnSamples != null)
                    {
                        a.Scope[s].Update(view.DrawnSamples, view.Samples.InputSamplesPerSecond);
                    }
                    a.Scope[s].VerticalUnit = a.Scope[view.Samples.Name].VerticalUnit;
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Ungroup",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusUnderMouse,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Views[0].GroupWithView = "",
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.U
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Group",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Scope.GroupViews(a.Views),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.G
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Hide",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Views[0].Visible = false,
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.X
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Close View",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => a.Views[0].Close(),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.None,
                HotKeyCode = Keys.Delete
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Set samples to 0",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                Clicked = (a) =>
                {
                    a.Views[0].Samples.SetSelectedSamples(
                        a.Views[0].Measure(a.Mouse.WipeTopLeft).IndexBeforeTrim,
                        a.Views[0].Measure(a.Mouse.WipeBottomRight).IndexBeforeTrim,
                        0.0);
                },
            });

            void Play(double[] samples, double sps)
            {
                if (samples.Length <= 1) return;
                new Thread(() =>
                {
                    using Stream stream = RiffWriter.ToStream(samples.Select(delegate (double x)
                    {
                        int sample = (int)Math.Round(x * 32768.0);
                        return (short)((sample < -32768) ? (-32768) : ((sample > 32767) ? 32767 : sample));
                    }).ToArray(), (int)sps);

                    SoundPlayer player = new SoundPlayer(stream);
                    try
                    {
                        player.PlaySync();
                    }
                    finally
                    {
                        player.Dispose();
                    }
                }).Start();
            }

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Play samples",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                Clicked = (a) =>
                {
                    a.Scope.ExceptionToMessagebox(() =>
                    {
                        //fixme? use TraceViewAudioPlayback version, but support mouse wipe selection
                        double[]? samples = a.Views[0].CalculatedBeforeZoom;
                        if (samples != null)
                        {
                            int sampleNumberAfterTrim = a.Views[0].Measure(a.Mouse.WipeTopLeft).IndexAfterTrim;
                            int length = a.Views[0].Measure(a.Mouse.WipeBottomRight).IndexAfterTrim - sampleNumberAfterTrim;
                            Play(samples.Copy(sampleNumberAfterTrim, length), a.Views[0].Samples.InputSamplesPerSecond);
                        }
                    }, "Play samples");
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Play samples",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OneSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                Clicked = (a) =>
                {
                    a.Scope.ExceptionToMessagebox(() =>
                    {
                        double[]? samples = a.Views[0].CalculatedBeforeZoom;
                        if (samples != null)
                        {
                            Play(samples, a.Views[0].Samples.InputSamplesPerSecond);
                        }
                    }, "Play samples");
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                Text = "Zoom",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) =>
                {
                    a.Scope.TraceAutoRange = false;
                    a.Views[0].AutoReduceRange = false;
                    a.Views[0].HighestValue = a.Views[0].Measure(a.Mouse.WipeTopLeft).YValue;
                    a.Views[0].LowestValue = a.Views[0].Measure(a.Mouse.WipeBottomRight).YValue;
                    double xRatioLeft = a.Views[0].Measure(a.Mouse.WipeTopLeft).XRatio;
                    double xRatioRight = a.Views[0].Measure(a.Mouse.WipeBottomRight).XRatio;
                    a.Scope.SetZoomPan(a.Scope.ZoomValue * (xRatioRight - xRatioLeft), a.Scope.PanValue + a.Scope.ZoomValue * xRatioLeft);
                },
            });
        }

        private static void AddRecolourSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            void Colour(ScopeContextMenu.DropDownArgs a, Func<int, Color> func)
            {
                foreach (var (view, index) in a.Views[0].Group.Where(x => x.Selected).OrderBy(x => x.ViewName).Select((x, i) => (x, i)))
                {
                    view.Colour = func(index);
                }
            }

            Color Blend(Color a, Color b, double alpha) => Color.FromArgb(
                    (int)((b.R - a.R) * alpha + a.R),
                    (int)((b.G - a.G) * alpha + a.G),
                    (int)((b.B - a.B) * alpha + a.B));

            const string subMenuText = "Re-colour";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Standard",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => Colour(a, index => a.Views[0].Painted.Group.Count == 1
                                    ? a.Scope.ActiveSkin.DefaultTraceColour
                                    : a.Scope.ActiveSkin.ColourByIndex(index)),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.H
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Red",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => Colour(a, (int index) => Blend(Color.LightSalmon, Color.DarkRed, index / (double)a.Views[0].Painted.Group.Count)),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Green",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => Colour(a, (int index) => Blend(Color.LightGreen, Color.DarkGreen, index / (double)a.Views[0].Painted.Group.Count)),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Blue",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => Colour(a, (int index) => Blend(Color.LightBlue, Color.DarkBlue, index / (double)a.Views[0].Painted.Group.Count)),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Black",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.AddViewNames,
                Clicked = (a) => Colour(a, (int index) => Color.Black),
            });
        }

        private static void AddSkinSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Skin";
            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Edit Skin",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) =>
                {
                    using AutoEditorForm autoEditorForm = new AutoEditorForm();
                    if (autoEditorForm.ShowDialog("Display settings", subMenuText, a.Scope.ActiveSkin))
                    {
                        a.Scope.RecalculateProjection();
                    }
                }
            });
            foreach (Skin.CannedSkins t in Enum.GetValues(typeof(Skin.CannedSkins)))
            {
                if (t != Skin.CannedSkins.Custom)
                {
                    contextMenu.Add(new ScopeContextMenu.MenuItem
                    {
                        SubMenuText = subMenuText,
                        Text = $"Skin {t}",
                        ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                        ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                        Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                        Clicked = (a) => a.Scope.ActiveSkin = new Skin(t),
                    });
                }
            }
        }

        private static void AddSortTracesSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Sort Traces";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Name,Colour",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.SortViewGroups(byColour: false),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Colour,Name",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.SortViewGroups(byColour: true),
            });
        }

        private static void AddDisplaySubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Display";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Rate limit refresh",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.PaintBoxRateLimitedRefresh = !a.Scope.PaintBoxRateLimitedRefresh,
                GetStyle = (a) => a.Checked = a.Scope.PaintBoxRateLimitedRefresh,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Crosshair cursor",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.CursorMode = a.Scope.CursorMode == Skin.Cursors.CrossHair ? Skin.Cursors.Pointer : Skin.Cursors.CrossHair,
                GetStyle = (a) => a.Checked = a.Scope.CursorMode == Skin.Cursors.CrossHair,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Vertical cursor",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.CursorMode = a.Scope.CursorMode == Skin.Cursors.VerticalLine ? Skin.Cursors.Pointer : Skin.Cursors.VerticalLine,
                GetStyle = (a) => a.Checked = a.Scope.CursorMode == Skin.Cursors.VerticalLine,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Trace statistics",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.ShowTraceStatistics = (Skin.TraceStatistics)a.Scope.ShowTraceStatistics.NextEnumValue(),
                GetStyle = (a) => a.Checked = a.Scope.ShowTraceStatistics != Skin.TraceStatistics.None,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Trace labels",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.ShowTraceLabels = (Skin.TraceLabels)a.Scope.ShowTraceLabels.NextEnumValue(),
                GetStyle = (a) => a.Checked = a.Scope.ShowTraceLabels != Skin.TraceLabels.None,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Hover statistics",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = (PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope),
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.ShowHoverInfo = !a.Scope.ShowHoverInfo,
                GetStyle = (a) => a.Checked = a.Scope.ShowHoverInfo,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Hover value",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = (PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope),
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.ShowHoverValue = !a.Scope.ShowHoverValue,
                GetStyle = (a) => a.Checked = a.Scope.ShowHoverValue,
            });
        }

        private static void AddDiagnosticSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Diagnostic";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Log",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    Form form = new Form { Text = "Sehens log" };
                    LogControl control = new LogControl();
                    control.Parent = form;
                    control.Dock = DockStyle.Fill;
                    SehensControl scope = a.Scope;
                    scope.OnLog += control.Add;
                    form.FormClosing += (s, o) => { SehensControl scope = a.Scope; scope.OnLog -= control.Add; };
                    form.Show();
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Paint Statistics",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.PaintBoxShowStats = !a.Scope.PaintBoxShowStats,
                GetStyle = (a) => a.Checked = a.Scope.PaintBoxShowStats,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Paint benchmark",
                Sort = 1,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => PaintBenchmark(a.Scope),
            });
        }

        internal static void PaintBenchmark(SehensControl scope, int passes = 10)
        {
            Skin.TraceSelections exportTraces = scope.ActiveSkin.ExportTraces;
            bool highQualityRender = scope.HighQualityRender;
            Cursor previousCursor = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                scope.ActiveSkin.ExportTraces = Skin.TraceSelections.VisibleTraces;
                scope.PaintBox.ScreenshotToBitmap(scope.ActiveSkin, null, parallel: true).Dispose(); // prime

                double Time(bool highQuality)
                {
                    scope.HighQualityRender = highQuality;
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    for (int loop = 0; loop < passes; loop++)
                    {
                        scope.PaintBox.ScreenshotToBitmap(scope.ActiveSkin, null, parallel: true).Dispose();
                    }
                    return timer.Elapsed.TotalMilliseconds / passes;
                }

                double highMs = Time(true);
                double lowMs = Time(false);
                string text = $"Parallel paint of {scope.VisibleViews.Length} visible traces, mean of {passes} passes:"
                    + $"\n\nHighQualityRender on: {highMs:0.0} ms\nHighQualityRender off: {lowMs:0.0} ms"
                    + (scope.PaintBox.PaintExceptionCount == 0
                        ? ""
                        : $"\n\n{scope.PaintBox.PaintExceptionCount} paint exceptions: {scope.PaintBox.LastPaintExceptionText}");
                scope.OnLog?.Invoke(new CsvLog.Entry(text.Replace("\n\n", " - ").Replace("\n", ", "), CsvLog.Priority.Info));
                MessageBox.Show(text, "Paint benchmark");
            }
            finally
            {
                Cursor.Current = previousCursor;
                scope.HighQualityRender = highQualityRender;
                scope.ActiveSkin.ExportTraces = exportTraces;
            }
        }

        private static void AddGenerateSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Generate";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Filter Coefficients",
                Sort = 8,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm autoEditorForm2 = new AutoEditorForm();
                    if (autoEditorForm2.ShowDialog("FIR Filter information", "Generate Filter", FilterGenInfo))
                    {
                        double[] array = FilterGenInfo.FftFilterType switch
                        {
                            TraceView.FftFilterTypes.BandPass => FftFilter.GenerateBandPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF6dB, FilterGenInfo.FftBandpassLPF3dB,
                                                FilterGenInfo.FftBandpassHPF3dB, FilterGenInfo.FftBandpassHPF6dB,
                                                FilterGenInfo.SamplesPerSecond, FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.BandPassFit => FftFilter.GenerateBandPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF3dB,
                                                FilterGenInfo.FftBandpassHPF3dB,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.Notch => FftFilter.GenerateNotchFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF3dB, FilterGenInfo.FftBandpassLPF6dB,
                                                FilterGenInfo.FftBandpassHPF6dB, FilterGenInfo.FftBandpassHPF3dB,
                                                FilterGenInfo.SamplesPerSecond, FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.NotchFit => FftFilter.GenerateNotchFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF3dB,
                                                FilterGenInfo.FftBandpassHPF3dB,
                                                FilterGenInfo.SamplesPerSecond, FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.HighPass => FftFilter.GenerateHighPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassHPF6dB, FilterGenInfo.FftBandpassHPF3dB,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.HighPass3dBPerOctave => FftFilter.GenerateHighPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassHPF3dB / 2.0, FilterGenInfo.FftBandpassHPF3dB,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.LowPass => FftFilter.GenerateLowPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF3dB, FilterGenInfo.FftBandpassLPF6dB,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.LowPass3dBPerOctave => FftFilter.GenerateLowPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassLPF3dB, FilterGenInfo.FftBandpassLPF3dB * 2.0,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.WeightedAudioA => FftFilter.GenerateArbitraryFir(
                                                FilterGenInfo.Width, FftFilter.WeightingA, FilterGenInfo.SamplesPerSecond),
                            TraceView.FftFilterTypes.WeightedAudioC => FftFilter.GenerateArbitraryFir(
                                                FilterGenInfo.Width, FftFilter.WeightingC, FilterGenInfo.SamplesPerSecond),
                            _ => new double[] { 1 },
                        };
                        a.Scope[FilterGenInfo.Name].UpdateByRef(array.ElementProduct(FilterGenInfo.Amplitude), FilterGenInfo.SamplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Window",
                Sort = 5,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm autoEditorForm = new AutoEditorForm();
                    if (autoEditorForm.ShowDialog("Config", "Generate Window", WindowInfo))
                    {
                        double[] samples = SampleWindow.GenerateWindow(WindowInfo.Count, WindowInfo.Window).ElementProduct(WindowInfo.Amplitude).Add(WindowInfo.Offset);
                        a.Scope[WindowInfo.Name].UpdateByRef(samples, WindowInfo.samplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Noise",
                Sort = 3,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm form = new AutoEditorForm();
                    if (form.ShowDialog("Config", "Generate Noise", NoiseInfo))
                    {
                        double[] samples = new NoiseGenerator { Amplitude = NoiseInfo.Amplitude }.Generate(NoiseInfo.SampleCount);
                        a.Scope[NoiseInfo.Name].UpdateByRef(samples, NoiseInfo.SamplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Tone",
                Sort = 1, // signal generators first
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm form = new AutoEditorForm();
                    if (form.ShowDialog("Config", "Generate Waveform", WaveformInfo))
                    {
                        double[] samples = new ToneGenerator
                        {
                            FrequencyStart = WaveformInfo.Frequency,
                            FrequencyEnd = WaveformInfo.Frequency,
                            Phase = WaveformInfo.Phase,
                            WaveTable = WaveformGenerator.List[WaveformInfo.Waveform],
                            SamplesPerSecond = WaveformInfo.SamplesPerSecond,
                            UseMathSin = WaveformInfo.UseSin
                        }.Generate(WaveformInfo.SampleCount);
                        samples = SampleWindow.Window(samples, WaveformInfo.Window);
                        a.Scope[WaveformInfo.Name].UpdateByRef(samples, WaveformInfo.SamplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Sweep",
                Sort = 2,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm form = new AutoEditorForm();
                    if (form.ShowDialog("Config", "Generate Sweep", SweepInfo))
                    {
                        double[] samples = new ToneGenerator
                        {
                            FrequencyStart = SweepInfo.FrequencyStart,
                            FrequencyEnd = SweepInfo.FrequencyEnd,
                            WaveTable = WaveformGenerator.List[SweepInfo.Waveform],
                            UseMathSin = SweepInfo.UseSinFunction,
                            SweepsPerSecond = SweepInfo.SweepRate,
                            SamplesPerSecond = SweepInfo.SamplesPerSecond
                        }.Generate(SweepInfo.Count);
                        a.Scope[SweepInfo.Name].UpdateByRef(samples, SweepInfo.SamplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Sin Cardinal (sinc)",
                Sort = 4,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    using AutoEditorForm form = new AutoEditorForm();
                    if (form.ShowDialog("Config", "Generate Sinc", SincInfo))
                    {
                        double[] samples = WaveformGenerator.SinCardinal(SincInfo.Count, SincInfo.Amplitude, SincInfo.LeftTime, SincInfo.RightTime, SincInfo.halfwidth, SincInfo.Delay, SincInfo.Offset);
                        a.Scope[SincInfo.Name].UpdateByRef(samples, SincInfo.SamplesPerSecond);
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "100 test traces",
                Sort = 12,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Scope.BeginUpdate();
                    try
                    {
                        Parallel.For(0, 99, (loop) =>
                        {
                            TraceData data = a.Scope["Test" + loop];
                            int sampleCount = RandomTestTraceSampleCount();
                            data.UpdateByRef(new NoiseGenerator().Generate(sampleCount), double.NaN);
                            TraceData[] traceList = a.Scope.AllTraces;
                            data.FirstView!.GroupWithView = traceList[Random.Shared.Next(traceList.Length)].Name;
                        });
                    }
                    finally
                    {
                        a.Scope.EndUpdate();
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "YT test traces",
                Sort = 11,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    new Thread((ThreadStart)delegate
                    {
                        Random random = new Random();
                        double[] walk = RandomWalkTimes(1000, random);
                        double[] yt3t = walk.Select(t => t * 2).ToArray();
                        double[] yt4t = walk.Select(t => t - 3).ToArray();
                        double[] yt3y = walk.Select(t => random.NextDouble()).ToArray();
                        double[] yt4y = walk.Select(Math.Sin).ToArray();
                        a.Scope["yt test 1"].Update(DoubleVectorExtensions.Range(1, 15, 2), DoubleVectorExtensions.Range(4, 15, 1));
                        a.Scope["yt test 2"].Update(DoubleVectorExtensions.Range(1, 5, 2), new double[5] { 10.0, 15.0, 19.0, 22.0, 23.0 });
                        a.Scope["yt test 3"].Update(yt3y, yt3t);
                        a.Scope["yt test 4"].Update(yt4y, yt4t);
                        a.Scope["yt test 5"].Update(new double[5] { 1.0, 3.0, 5.0, 7.0, 9.0 }, new double[5] { 11.0, 10.0, 6.0, 5.0, 4.0 });
                        a.Scope["yt test 6"].Update(new double[5] { 1.0, 3.0, 5.0, 7.0, 9.0 }, DoubleVectorExtensions.Range(12345678, 5, 0.001));
                        a.Scope["yt test 7"].Update(
                            DoubleVectorExtensions.Range(0, 1000000, 0.01),
                            DoubleVectorExtensions.Range(41000000, 1000000, 0.001));
                        a.Scope["yt test 8"].Update(
                            Enumerable.Range(0, 1000000).Select(x => random.NextDouble()),
                            Enumerable.Range(0, 1000000).Select(x => x % 10000 + (x / 10000) * 100000.0));
                    }).Start();
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Axis test matrix",
                Sort = 10, // bulk test data last
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => GenerateAxisTestMatrix(a.Scope),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Math test traces",
                Sort = 13, // bulk test data last
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => GenerateMathTestTraces(a.Scope),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Filter test traces",
                Sort = 14, // bulk test data last
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => GenerateFilterTestTraces(a.Scope),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = "Generate",
                Text = "All filters",
                Sort = 7,
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    string? text = InputFieldForm.Show("Samples per second?", "Show scope coefficients", 10000, cache: true);
                    if (text != null && double.TryParse(text, out var sps))
                    {
                        foreach (var filter in FilterCoefficients.List)
                        {
                            a.Scope.AddTrace(a.Scope[filter.Key].Update(filter.Value, sps));
                        }
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = "Generate",
                Text = "All windows",
                Sort = 6, // reference sets
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => SampleWindow.Scope((trace, data) => a.Scope[trace].Update(data)),
            });
        }

        private enum EmbedFftLabel { Normal, FFT, Spectral, FFT2D }

        private static void AddTraceEmbeddedMenu(List<ScopeContextMenu.EmbeddedMenu> embeddedContextMenu)
        {
            EmbedFftLabel FftLabel(TraceView view)
            {
                // Spectral is an FFTMagnitude trace too, so test it before the plain-FFT check.
                return view.PaintMode == TraceView.PaintModes.Spectral
                        ? EmbedFftLabel.Spectral
                        : view.MathType == TraceView.MathTypes.FFTMagnitude
                            ? EmbedFftLabel.FFT
                            : view.PaintMode == TraceView.PaintModes.FFT2D
                                ? EmbedFftLabel.FFT2D
                                : EmbedFftLabel.Normal;
            }

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "PiP",
                Sort = 5,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    a.View.ShowPictureInPicture = !a.View.ShowPictureInPicture;
                },
                GetStyle = (a) =>
                {
                    a.Menu.Style = a.View.ShowPictureInPicture ? TraceViewEmbedText.Style.Selected : TraceViewEmbedText.Style.Normal;
                    a.Menu.Text = "PiP";
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "FFT",
                Sort = 10,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    EmbedFftLabel fft = (EmbedFftLabel)FftLabel(a.View).NextEnumValue();
                    bool isFft = fft == EmbedFftLabel.FFT || fft == EmbedFftLabel.Spectral;
                    a.View.MathType = isFft ? TraceView.MathTypes.FFTMagnitude : TraceView.MathTypes.Normal;
                    a.View.PaintMode = fft switch
                    {
                        EmbedFftLabel.FFT2D => TraceView.PaintModes.FFT2D,
                        EmbedFftLabel.Spectral => TraceView.PaintModes.Spectral,
                        _ => TraceView.PaintModes.PolygonDigital,
                    };
                    a.View.AutoRange();
                },
                GetStyle = (a) =>
                {
                    EmbedFftLabel fft = FftLabel(a.View);
                    a.Menu.Style = fft == EmbedFftLabel.Normal ? TraceViewEmbedText.Style.Normal : TraceViewEmbedText.Style.Selected;
                    a.Menu.Text = (fft == EmbedFftLabel.Normal ? EmbedFftLabel.FFT : fft).ToString();
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "LinV",
                Sort = 11,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    a.View.LogVertical = (TraceView.LogVerticalMode)a.View.LogVertical.NextEnumValue();
                    a.View.AutoRange();
                },
                GetStyle = (a) =>
                {
                    // Label the mode that is actually applied, so cycling the FFT button shows the
                    // vertical mode following it. Auto gets a trailing "*" - without it Auto and the
                    // same mode chosen explicitly are adjacent cycle steps that look identical.
                    TraceView.LogVerticalMode effective = a.View.EffectiveLogVertical;
                    a.Menu.Style = effective == TraceView.LogVerticalMode.Off
                        ? TraceViewEmbedText.Style.Normal
                        : TraceViewEmbedText.Style.Selected;
                    a.Menu.Text = effective switch
                    {
                        TraceView.LogVerticalMode.Off => "LinV",
                        TraceView.LogVerticalMode.Log => "LogV",
                        TraceView.LogVerticalMode.dB10 => "10Log10",
                        TraceView.LogVerticalMode.dB20 => "20Log10",
                        _ => effective.ToString(),
                    };
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "LinH",
                Sort = 12,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    a.View.LogHorizontal = (TraceView.LogHorizontalMode)a.View.LogHorizontal.NextEnumValue();
                },
                GetStyle = (a) =>
                {
                    a.Menu.Style = a.View.LogHorizontal == TraceView.LogHorizontalMode.Off
                        ? TraceViewEmbedText.Style.Normal
                        : TraceViewEmbedText.Style.Selected;
                    a.Menu.Text = a.View.LogHorizontal == TraceView.LogHorizontalMode.Log ? "LogH" : "LinH";
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Line",
                Sort = 10,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    a.View.PaintMode = a.View.PaintMode switch
                    {
                        TraceView.PaintModes.PolygonDigital => TraceView.PaintModes.PolygonContinuous,
                        TraceView.PaintModes.PolygonContinuous => TraceView.PaintModes.Points,
                        TraceView.PaintModes.Points => TraceView.PaintModes.PointsIfChanged,
                        _ => TraceView.PaintModes.PolygonDigital,
                    };
                },
                GetStyle = (a) =>
                {
                    a.Menu.Text = a.View.PaintMode switch
                    {
                        TraceView.PaintModes.PolygonContinuous => "Continuous",
                        TraceView.PaintModes.PolygonDigital => "Digital",
                        TraceView.PaintModes.Points => "Dots",
                        TraceView.PaintModes.PointsIfChanged => "Dots diff",
                        _ => a.View.PaintMode.ToString(),
                    };
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Range",
                Sort = 10,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) => a.View.AutoReduceRange = !a.View.AutoReduceRange,
                GetStyle = (a) =>
                {
                    a.Menu.Style = a.View.AutoReduceRange ? TraceViewEmbedText.Style.Selected : TraceViewEmbedText.Style.Normal;
                    a.Menu.Text = "Shrink";
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {

                Text = "Phase",
                Sort = 10,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    a.View.MathPhase = a.View.MathPhase == TraceView.CalculatePhases.AfterZoom
                                    ? TraceView.CalculatePhases.BeforeZoom
                                    : TraceView.CalculatePhases.AfterZoom;
                    a.View.AutoRange();
                },
                GetStyle = (a) =>
                {
                    a.Menu.Style = a.View.MathPhase == TraceView.CalculatePhases.BeforeZoom
                                    ? TraceViewEmbedText.Style.Selected
                                    : TraceViewEmbedText.Style.Normal;
                    a.Menu.Text = a.View.MathPhase.ToString();
                }
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Hold Zoom",
                Sort = 20,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) => a.View.HoldPanZoom = !a.View.HoldPanZoom,
                GetStyle = (a) => a.Menu.Style = a.View.HoldPanZoom ? TraceViewEmbedText.Style.Selected : TraceViewEmbedText.Style.Normal
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Trim",
                Sort = 30,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) =>
                {
                    if (a.View.ViewOffsetOverride != 0 || a.View.ViewOverrideEnabled)
                    {
                        a.View.ViewOverrideEnabled = !a.View.ViewOverrideEnabled;
                    }
                    else
                    {
                        var extents = a.View.DrawnExtents();
                        if (extents.rightSampleNumber != 0 && extents.rightSampleNumber > extents.leftSampleNumber)
                        {
                            a.View.ViewOffsetOverride = extents.leftSampleNumber;
                            a.View.ViewLengthOverride = extents.rightSampleNumber - extents.leftSampleNumber;
                            a.View.ViewOverrideEnabled = false;
                        }
                    }
                },
                GetStyle = (a) => a.Menu.Style = a.View.ViewOverrideEnabled ? TraceViewEmbedText.Style.Selected : TraceViewEmbedText.Style.Normal
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Trigger",
                Sort = 40,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) => a.View.TriggerMode = a.View.TriggerMode == TraceView.TriggerModes.None
                                    ? TraceView.TriggerModes.RisingAuto
                                    : TraceView.TriggerModes.None,
                GetStyle = (a) => a.Menu.Style = a.View.TriggerMode == TraceView.TriggerModes.None
                                    ? TraceViewEmbedText.Style.Normal
                                    : TraceViewEmbedText.Style.Selected
            });

            embeddedContextMenu.Add(new ScopeContextMenu.EmbeddedMenu
            {
                Text = "Audio",
                Sort = 50,
                Style = TraceViewEmbedText.Style.Normal,
                Clicked = (a) => ShowAudioPopup(a.View),
                GetStyle = (a) =>
                {
                    a.Menu.Style = a.View.Samples.InputSamplesPerSecond > 0
                        ? (a.View.IsPlaying ? TraceViewEmbedText.Style.Selected : TraceViewEmbedText.Style.Normal)
                        : TraceViewEmbedText.Style.Invisible;
                }
            });
        }

        private static void ShowAudioPopup(TraceView view)
        {
            var menu = new ContextMenuStrip();

            var play = new ToolStripMenuItem("Play");
            play.Enabled = !view.IsPlaying;
            play.Click += (s, e) =>
            {
                menu.ExceptionToMessagebox(() =>
                {
                    view.StartPlayback();
                }, "Play samples");
            };
            menu.Items.Add(play);

            var stop = new ToolStripMenuItem("Stop");
            stop.Enabled = view.IsPlaying;
            stop.Click += (s, e) => view.StopPlayback();
            menu.Items.Add(stop);

            var loop = new ToolStripMenuItem("Loop");
            loop.CheckOnClick = true;
            loop.Checked = view.AudioLoop;
            loop.CheckedChanged += (s, e) => view.AudioLoop = loop.Checked;
            menu.Items.Add(loop);

            var afterFilter = new ToolStripMenuItem("After filter");
            afterFilter.CheckOnClick = true;
            afterFilter.Checked = view.AudioAfterFilter;
            afterFilter.CheckedChanged += (s, e) => view.AudioAfterFilter = afterFilter.Checked;
            menu.Items.Add(afterFilter);

            menu.Show(Cursor.Position);
        }

        private static void AddTraceSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Trace";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            { //also doubleclick
                SubMenuText = subMenuText,
                Text = "Settings",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => new AutoEditorForm().ShowDialog(sourceData: a.Views[0], prompt: "Trace settings", title: a.Views[0].DecoratedName),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Match vertical",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    double high = a.Views.Max(x => x.HighestValue);
                    double low = a.Views.Min(x => x.LowestValue);
                    foreach (TraceView view in a.Views)
                    {
                        view.AutoReduceRange = false;
                        view.HighestValue = high;
                        view.LowestValue = low;
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Match Horizontal (ViewLengthOverride)",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    int length = Math.Max(a.Views.Max(x => x.ViewLengthOverride), a.Views.Max(x => x.Samples.InputSampleCount));
                    foreach (TraceView view in a.Views)
                    {
                        view.ViewLengthOverride = length;
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Copy trigger",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    TraceView view = a.Views[0];
                    a.Views[0].TriggerMode = view.TriggerMode;
                    a.Views[0].TriggerValue = view.TriggerValue;
                    a.Views[0].TriggerView = view.TriggerView;
                    a.Views[0].PreTriggerSampleCount = view.PreTriggerSampleCount;
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Rename",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    string? newName = InputFieldForm.Show("New name?", "Rename", a.Views[0].ViewName);
                    if (newName != null)
                    {
                        a.Views[0].ViewName = newName;
                        a.Views[0].Samples.Name = newName;
                    }
                },
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.None,
                HotKeyCode = Keys.F2
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Rename View",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    string? newName = InputFieldForm.Show("New view name?", "Rename View", a.Views[0].ViewName);
                    if (newName != null)
                    {
                        a.Views[0].ViewName = newName;
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Rename Samples",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    string? newName = InputFieldForm.Show("New samples name?", "Rename Samples", a.Views[0].Samples.Name);
                    if (newName != null)
                    {
                        a.Views[0].Samples.Name = newName;
                    }
                },
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Close empty/flat/hidden traces",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Scope.CloseEmptyTraces();
                    a.Scope.CloseFlatTraces();
                    a.Scope.CloseInvisibleTraces();
                }
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Auto-range",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Views[0].AutoRange(),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Select all",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.SelectAllVisible(),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.A
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "XY",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Views[0].PaintMode = a.Views[0].PaintMode == TraceView.PaintModes.XYLine ? TraceView.PaintModes.PolygonDigital : TraceView.PaintModes.XYLine;
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Checked = a.Views[0].PaintMode == TraceView.PaintModes.XYLine ? true : false,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "FFT",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Views[0].MathType = a.Views[0].MathType == TraceView.MathTypes.Normal ? TraceView.MathTypes.FFTMagnitude : TraceView.MathTypes.Normal;
                    a.Views[0].MathPhase = TraceView.CalculatePhases.BeforeZoom;
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Checked = a.Views[0].MathType != TraceView.MathTypes.Normal,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "FFT2D",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Views[0].PaintMode = a.Views[0].PaintMode == TraceView.PaintModes.FFT2D ? TraceView.PaintModes.PolygonDigital : TraceView.PaintModes.FFT2D;
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Checked = a.Views[0].PaintMode == TraceView.PaintModes.FFT2D,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Spectral",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    if (a.Views[0].PaintMode == TraceView.PaintModes.Spectral)
                    {
                        a.Views[0].MathType = TraceView.MathTypes.Normal;
                        a.Views[0].PaintMode = TraceView.PaintModes.PolygonDigital;
                    }
                    else
                    {
                        a.Views[0].MathType = TraceView.MathTypes.FFTMagnitude;
                        a.Views[0].MathPhase = TraceView.CalculatePhases.BeforeZoom;
                        a.Views[0].PaintMode = TraceView.PaintModes.Spectral;
                    }
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Checked = a.Views[0].PaintMode == TraceView.PaintModes.Spectral,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Auto shrink",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Views[0].AutoReduceRange = !a.Views[0].AutoReduceRange;
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Checked = a.Views[0].AutoReduceRange,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Hold zoom",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Views[0].HoldPanZoom = !a.Views[0].HoldPanZoom,
                GetStyle = (a) => a.Checked = a.Views[0].HoldPanZoom,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Show picture-in-picture",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Views[0].ShowPictureInPicture = !a.Views[0].ShowPictureInPicture,
                GetStyle = (a) => a.Checked = a.Views[0].ShowPictureInPicture,
            });
        }

        private static void AddMathSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Math";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Triggered slice",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OneSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    TriggeredSliceTrace(a);
                },
            });

            foreach (var math in Enum.GetValues<TraceView.CalculatedTypes>())
            {
                var menuItem = math switch
                {
                    TraceView.CalculatedTypes.None => null,

                    TraceView.CalculatedTypes.Differentiate => Create("Differentiate", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace),
                    TraceView.CalculatedTypes.Integrate => Create("Integrate", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace),
                    TraceView.CalculatedTypes.ProjectYTtoY => Create("ProjectYTtoY", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace),
                    TraceView.CalculatedTypes.Normalised => Create("Normalised", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace),
                    TraceView.CalculatedTypes.Abs => Create("Abs", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace),

                    TraceView.CalculatedTypes.SubtractOffset => Create("SubtractOffset", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataOneDouble()),
                    TraceView.CalculatedTypes.ProductSimple => Create("ProductSimple", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataOneDouble()),
                    TraceView.CalculatedTypes.Rescale => Create("Rescale", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataMinMax()),
                    TraceView.CalculatedTypes.Quantize => Create("Quantize", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataQuantise()),
                    TraceView.CalculatedTypes.RollingRMS => Create("RollingRMS", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataWindow()),
                    TraceView.CalculatedTypes.RollingMean => Create("RollingMean", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataWindow()),
                    TraceView.CalculatedTypes.Hampel => Create("Hampel despike", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataWindow { Window = 8 }),
                    TraceView.CalculatedTypes.RollingMAD => Create("Rolling MAD", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataWindow { Window = 21 }),
                    TraceView.CalculatedTypes.Kalman => Create("Kalman smooth", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataKalman()),
                    TraceView.CalculatedTypes.Resample => Create("Resample", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataCount()),
                    TraceView.CalculatedTypes.PolyFilter => Create("PolyFilter", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataOrder()),

                    TraceView.CalculatedTypes.Atan2 => Create("Atan2", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.RescaledError => Create("RescaledError", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.NormalisedError => Create("NormalisedError", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Mard => Create("MARD (rel diff %)", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.FIR => Create("FIR", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Subtract => Create("Subtract", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Difference => Create("Difference", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),

                    TraceView.CalculatedTypes.Sum => Create("Sum", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Magnitude => Create("Magnitude", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Product => Create("Product", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Mean => Create("Mean", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),

                    TraceView.CalculatedTypes.PythonScript => null,
                    _ => null,
                };
                if (menuItem != null)
                {
                    contextMenu.Add(menuItem);
                }
            }

            ScopeContextMenu.MenuItem Create(string text, TraceView.CalculatedTypes type, ScopeContextMenu.MenuItem.ShowWhen when, ScopeContextMenu.MenuItem.CallWhen call, TraceView.CalculatedTraceData? prompt = null)
            {
                return new ScopeContextMenu.MenuItem
                {
                    SubMenuText = subMenuText,
                    Text = text,
                    ShownWhenTrace = when,
                    ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                    Call = call,
                    ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                    Clicked = (a) => Click(type, prompt, a)
                };
            }

            static void Click(TraceView.CalculatedTypes type, TraceView.CalculatedTraceData? data, ScopeContextMenu.DropDownArgs a)
            {
                bool create = true;
                if (data != null)
                {
                    using AutoEditorForm form = new AutoEditorForm();
                    create = form.ShowDialog("Information", "Calculated view", data);
                }
                if (create)
                {
                    string viewName = type.ToString() + "(" + string.Join(",", a.Views.Select(x => x.Samples.Name)) + ")";
                    TraceView view = a.Scope.EnsureView(viewName);
                    // Clone so each calculated trace gets its own parameter instance, not the data template
                    view.CalculatedParameter = data?.Clone() ?? new TraceView.CalculatedTraceData();
                    view.Samples.InputSamplesPerSecond = a.Views[0].Samples.InputSamplesPerSecond;
                    view.CalculatedSourceViews = a.Views.ToList();
                    view.CalculateType = type;
                }
            }
        }

        private static void TriggeredSliceTrace(ScopeContextMenu.DropDownArgs a)
        {
            using AutoEditorForm autoEditorForm = new AutoEditorForm();
            if (!autoEditorForm.ShowDialog("Config", "Triggered slice", TriggeredSliceInfo)) return;

            const string separator = " slice ";
            foreach (var trace in a.Scope.AllViews)
            {
                if (trace.ViewName.StartsWith(a.Views[0].ViewName + separator))
                {
                    trace.Close();
                }
            }

            double[] samples = a.Views[0].Samples.InputSamplesAsDouble;
            var result = SehensWerte.Maths.FFTAnalyse.SliceSamples(
                samples: samples,
                triggerValue: TriggeredSliceInfo.TriggerValue,
                risingPhase: TriggeredSliceInfo.Phase == TriggeredSliceForm.TriggerPhase.Rising,
                preTriggerSamples: TriggeredSliceInfo.PreTriggerSamples,
                postTriggerMinimumSamples: TriggeredSliceInfo.PostTriggerMinimumSamples);

            int index = 1;
            int maxLen = result.Max(x => x.Length);
            var viewNames = new List<string>();
            foreach (var piece in result)
            {
                string trace = a.Views[0].ViewName + separator + index.ToString();
                a.Scope[trace].Update(piece, a.Views[0].SamplesPerSecond);
                a.Scope[trace].FirstView!.ViewLengthOverride = maxLen;
                viewNames.Add(trace);
                index++;
            }

            a.Scope.GroupViews(viewNames);

        }


        private static void AddTraceFilterSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Trace Filter";
            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "FFT Filter",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => new AutoEditorForm().ShowDialog(sourceData: new FilterForm(a.Views[0]), prompt: "Filter settings", title: a.Views[0].DecoratedName),
            });
            foreach (string filter in FilterChoice.FilterNames)
            {

                contextMenu.Add(new ScopeContextMenu.MenuItem
                {
                    SubMenuText = subMenuText,
                    Text = filter,
                    ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                    ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                    Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                    ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                    Clicked = (a) => a.Views[0].TraceFilter = a.Menu.Text,
                });
            }
        }

        private static void AddFeaturesSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            void Add(ScopeContextMenu.DropDownArgs a)
            {
                TraceView traceView = a.Views[0];
                if (a.Mouse.WipeStart == null)
                {
                    var measureInfo = traceView.Measure(a.Mouse.Click);
                    string? text = InputFieldForm.Show($"Add text on trace {traceView.Samples.Name} sample {measureInfo.IndexBeforeTrim}", "Text", cache: true);
                    if (text != null)
                    {
                        traceView.Samples.AddFeature(measureInfo.IndexBeforeTrim, text);
                    }
                }
                else
                {
                    traceView.Samples.AddFeature(new TraceFeature
                    {
                        Type = TraceFeature.Feature.Highlight,
                        SampleNumber = traceView.Measure(a.Mouse.WipeTopLeft).IndexBeforeTrim,
                        RightSampleNumber = traceView.Measure(a.Mouse.WipeBottomRight).IndexBeforeTrim,
                        Colour = Color.FromArgb(128, Color.Yellow)
                    });
                }
                a.Scope.Invalidate();
            }

            const string subMenuText = "Features";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Show",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea | PaintBoxMouseInfo.GuiSection.EmptyScope,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.ShowTraceFeatures = !a.Scope.ShowTraceFeatures,
                GetStyle = (a) => a.Checked = a.Scope.ShowTraceFeatures,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Clear",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Views[0].Samples.InputFeatures = new TraceFeature[0],
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Add",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => Add(a),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Highlight",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.RightWipeSelect,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.TraceArea,
                Call = ScopeContextMenu.MenuItem.CallWhen.PerTrace,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => Add(a),
            });
        }
    }

    // Smoke test for the axis test matrix: every group classifies as its taxonomy row intends,
    // and the whole board paints.
    [TestClass]
    public class AxisTestMatrixTests
    {
        [TestMethod]
        public void MatrixGeneratesEveryTaxonomyRow()
        {
            var scope = new SehensControl();
            ContextMenus.GenerateAxisTestMatrix(scope);
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(59, scope.AllViews.Length); // 25 pairs + the ax20 zoo of 8 + lone ax19

            TraceGroupDisplay Info(string name) => scope.PaintBox.TraceToGroupDisplayInfo(
                scope.ViewByName(name) ?? throw new AssertFailedException($"view {name} missing"));

            Assert.AreEqual(HorizontalMode.Stretch, Info("ax01 stretch A").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax02 align A").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax03 ragged full").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax04 gap low").HMode);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax05 unit rpm").HMode);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax06 plain").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax07 time long").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax08 rate fast").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax09 shift base").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax10 sps seconds").HMode);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax11 lin").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax12 log A").HMode);
            Assert.IsTrue(Info("ax13 fakeyt A").YTTrace);
            Assert.AreEqual(HorizontalMode.Stretch, Info("ax13 fakeyt A").HMode); // YT pair keeps its own time window
            Assert.IsTrue(Info("ax14 realyt A").YTTrace);
            Assert.AreEqual(HorizontalMode.Stretch, Info("ax14 realyt A").HMode);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax15 yt").HMode); // YT + plain -> warn
            Assert.AreEqual(HorizontalMode.Stretch, Info("ax16 fft 500Hz").HMode); // FFT pair keeps its own path
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax17 fft").HMode);
            Assert.IsTrue(Info("ax18 yt").YTTrace);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax18 yt").HMode); // YT + FFT -> warn even YT-led
            var bad = scope.ViewByName("ax19 bad multiplier") ?? throw new AssertFailedException("ax19 missing");
            Assert.IsTrue(bad.Samples.HorizontalAffineInvalid);
            Assert.AreEqual(HorizontalMode.Incompatible, Info("ax20 base").HMode); // differing counts warn
            double[] Drawn(string name) => (scope.ViewByName(name)
                ?? throw new AssertFailedException($"view {name} missing")).DrawnSamples
                ?? throw new AssertFailedException($"view {name} drew nothing");
            double[] ax20base = Drawn("ax20 base");
            Assert.AreEqual(350, Drawn("ax20 trim left").Length);
            Assert.AreEqual(ax20base[150], Drawn("ax20 trim left")[0], 1e-9);  // source[150] at index 0
            Assert.AreEqual(350, Drawn("ax20 trim right").Length);
            Assert.AreEqual(300, Drawn("ax20 trim both").Length);
            Assert.AreEqual(500, Drawn("ax20 slide").Length);
            Assert.AreEqual(0.0, Drawn("ax20 slide")[0], 1e-9);                // unpadded lead-in is zeros
            Assert.AreEqual(ax20base[0], Drawn("ax20 slide")[150], 1e-9);      // source[0] at index 150
            Assert.AreEqual(650, Drawn("ax20 pad left").Length);
            Assert.AreEqual(ax20base[0], Drawn("ax20 pad left")[0], 1e-9);     // pad-left holds first value
            Assert.AreEqual(650, Drawn("ax20 pad right").Length);
            Assert.AreEqual(ax20base[499], Drawn("ax20 pad right")[649], 1e-9); // pad-right holds last value
            double[] padBoth = Drawn("ax20 pad both");
            Assert.AreEqual(800, padBoth.Length);
            Assert.AreEqual(padBoth[150], padBoth[0], 1e-9);
            Assert.AreEqual(padBoth[649], padBoth[799], 1e-9);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax21 window full").HMode);
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax22 time full").HMode);
            // ax23/ax24: affine offset+multiplier / sps+offset combined with a view window
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax23 combo full").HMode);
            var combo = (scope.ViewByName("ax23 combo window") ?? throw new AssertFailedException("ax23 missing")).DrawnExtents();
            Assert.AreEqual(500.0, combo.leftSampleNumberValue, 1e-9);   // 10 * (0 + 50)
            Assert.AreEqual(3000.0, combo.rightSampleNumberValue, 1e-9); // 10 * (250 + 50)
            Assert.AreEqual(HorizontalMode.ValueAlign, Info("ax24 combo time full").HMode);
            var comboTime = (scope.ViewByName("ax24 combo time window") ?? throw new AssertFailedException("ax24 missing")).DrawnExtents();
            Assert.AreEqual(0.5, comboTime.leftSampleNumberValue, 1e-9); // (0 + 500) / 1000
            Assert.AreEqual(1.5, comboTime.rightSampleNumberValue, 1e-9);
            Assert.AreEqual("s", comboTime.sampleValueUnit);
            // ax25: FFT of a real-YT trace - resampled onto a uniform grid whose rate comes from
            // the SMALLEST time gap (walk min step ~0.01 s -> Nyquist ~50 Hz), peak at 0.3 Hz
            TraceView ytFft = scope.ViewByName("ax25 yt fft") ?? throw new AssertFailedException("ax25 missing");
            Assert.AreEqual(HorizontalKind.Fft, ytFft.HorizontalKind);
            var ytExt = ytFft.DrawnExtents();
            Assert.AreEqual("Hz", ytExt.sampleValueUnit);
            Assert.IsTrue(ytExt.rightSampleNumberValue > 10.0 && ytExt.rightSampleNumberValue < 60.0,
                $"Nyquist from the min-gap-derived rate, got {ytExt.rightSampleNumberValue:0.###} Hz");
            double[] spectrum = ytFft.DrawnSamples ?? throw new AssertFailedException("ax25 drew nothing");
            int ytPeak = 1;
            for (int loop = 1; loop < spectrum.Length; loop++)
            {
                if (spectrum[loop] > spectrum[ytPeak]) ytPeak = loop;
            }
            double ytPeakHz = ytPeak * ytExt.rightSampleNumberValue / spectrum.Length;
            Assert.AreEqual(0.3, ytPeakHz, 0.1, $"resampled tone peak at {ytPeakHz:0.###} Hz");
            // ax26: mixed-rate fake YT still time-aligns; group window is the union of both
            TraceGroupDisplay mixedRate = Info("ax26 fakeyt fast");
            Assert.IsTrue(mixedRate.YTTrace);
            Assert.AreEqual(1_700_000_000.0, mixedRate.LeftUnixTime, 0.1);
            Assert.AreEqual(1_700_000_010.0, mixedRate.RightUnixTime, 0.1);
            Assert.IsTrue(Info("ax27 ytpad early").YTTrace);

            scope.ActiveSkin.ExportTraces = Skin.TraceSelections.VisibleTraces;
            using Bitmap bmp = scope.PaintBox.ScreenshotToBitmap(scope.ActiveSkin, null);
            Assert.IsTrue(bmp.Width > 1 && bmp.Height > 1, "the whole matrix must paint");
            // paint exceptions are caught and painted as pixels; the counter makes them fail tests
            // (this is how the fake-YT IndexOutOfRange would have been caught)
            Assert.AreEqual(0, scope.PaintBox.PaintExceptionCount,
                scope.PaintBox.LastPaintExceptionText ?? "paint exception recorded");
        }

        [TestMethod]
        public void MathTestTracesComputeAndSettle()
        {
            var scope = new SehensControl();
            ContextMenus.GenerateMathTestTraces(scope);
            for (int pass = 0; pass < 3; pass++)
            {
                SehensTestHarness.Layout(scope);
            }

            TraceView[] calcs = scope.AllViews.Where(x => x.CalculateType != TraceView.CalculatedTypes.None).ToArray();
            Assert.AreEqual(23, calcs.Length, "one view per implemented CalculatedTypes value");
            foreach (TraceView view in calcs)
            {
                Assert.IsTrue((view.DrawnSamples?.Length ?? 0) > 0, $"{view.ViewName} produced no samples");
                Assert.IsFalse(view.m_BeforeZoomCalculateRequired, $"{view.ViewName} did not settle (paint loop)");
            }
        }

        [TestMethod]
        public void FilterTestTracesComputeForEveryFilter()
        {
            var scope = new SehensControl();
            ContextMenus.GenerateFilterTestTraces(scope);
            SehensTestHarness.Layout(scope);

            int expected = FilterChoice.FilterNames.Count(x => x != "None");
            TraceView[] filtered = scope.AllViews.Where(x => x.TraceFilter != "None").ToArray();
            Assert.AreEqual(expected, filtered.Length, "one view per filter choice");
            foreach (TraceView view in filtered)
            {
                Assert.IsTrue((view.DrawnSamples?.Length ?? 0) > 0, $"{view.ViewName} produced no samples");
                Assert.IsTrue(view.DrawnSamples!.Any(v => v != 0.0), $"{view.ViewName} output is all zero");
            }
        }
    }
}
