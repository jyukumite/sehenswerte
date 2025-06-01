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
            public double Phase;
            [AutoEditor.DisplayName("Waveform")]
            public WaveformGenerator.Waveforms Waveform;
            [AutoEditor.DisplayName("Use Sin function")]
            public bool UseSin;
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
            public double Delay;
            [AutoEditor.DisplayName("Offset")]
            public double Offset;
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
            public double Offset;
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
            public WaveformGenerator.Waveforms Waveform;

            [AutoEditor.DisplayOrder(7)]
            [AutoEditor.DisplayName("Use Sin function")]
            public bool UseSinFunction;
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.TraceAutoRange,
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
                GetStyle = (a) => a.Menu.Menu.Checked = !a.Scope.TraceListVisible,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.StopUpdates,
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
                    try
                    {
                        double[]? samples = a.Views[0].CalculatedBeforeZoom;
                        if (samples != null)
                        {
                            int sampleNumberAfterTrim = a.Views[0].Measure(a.Mouse.WipeTopLeft).IndexAfterTrim;
                            int length = a.Views[0].Measure(a.Mouse.WipeBottomRight).IndexAfterTrim - sampleNumberAfterTrim;
                            Play(samples.Copy(sampleNumberAfterTrim, length), a.Views[0].Samples.InputSamplesPerSecond);
                        }
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show(ex2.Message);
                    }
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
                    try
                    {
                        double[]? samples = a.Views[0].CalculatedBeforeZoom;
                        if (samples != null)
                        {
                            Play(samples, a.Views[0].Samples.InputSamplesPerSecond);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.PaintBoxRateLimitedRefresh,
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Paint Statistics",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                Clicked = (a) => a.Scope.PaintBoxShowStats = !a.Scope.PaintBoxShowStats,
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.PaintBoxShowStats,
            });

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
                Text = "Crosshair cursor",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => a.Scope.CursorMode = a.Scope.CursorMode == Skin.Cursors.CrossHair ? Skin.Cursors.Pointer : Skin.Cursors.CrossHair,
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.CursorMode == Skin.Cursors.CrossHair,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.CursorMode == Skin.Cursors.VerticalLine,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.ShowTraceStatistics != Skin.TraceStatistics.None,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.ShowTraceLabels != Skin.TraceLabels.None,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.ShowHoverInfo,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.ShowHoverValue,
            });
        }

        private static void AddGenerateSubMenu(List<ScopeContextMenu.MenuItem> contextMenu)
        {
            const string subMenuText = "Generate";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Filter Coefficients",
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
                                                FilterGenInfo.FftBandpassHPF6dB, FilterGenInfo.FftBandpassHPF3dB, FilterGenInfo.FftBandpassLPF3dB, FilterGenInfo.FftBandpassLPF6dB,
                                                FilterGenInfo.SamplesPerSecond, FilterGenInfo.FftBandpassWindow),
                            TraceView.FftFilterTypes.BandPassFit => FftFilter.GenerateBandPassFir(FilterGenInfo.Width,
                                                FilterGenInfo.FftBandpassHPF3dB, FilterGenInfo.FftBandpassLPF3dB,
                                                FilterGenInfo.SamplesPerSecond,
                                                FilterGenInfo.FftBandpassWindow),
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
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    a.Scope.BeginUpdate();
                    try
                    {
                        Random random = new Random();
                        Parallel.For(0, 99, (x) =>
                        {
                            TraceData data = a.Scope["Test" + x];
                            data.Update(new NoiseGenerator().Generate(250000));
                            TraceData[] traceList = a.Scope.AllTraces;
                            data.FirstView!.GroupWithView = traceList[random.Next(traceList.Length)].Name;
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
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    new Thread((ThreadStart)delegate
                    {
                        double[] yt3y = new double[1000];
                        double[] yt3t = new double[1000];
                        double[] yt4y = new double[1000];
                        double[] yt4t = new double[1000];
                        Random random = new Random();
                        double t = 0.0;
                        for (int i = 0; i < yt3y.Length; i++)
                        {
                            t += random.NextDouble() + 0.01;
                            yt3t[i] = t * 2;
                            yt4t[i] = t - 3;
                            yt3y[i] = random.NextDouble();
                            yt4y[i] = Math.Sin(t);
                        }
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
                SubMenuText = "Generate",
                Text = "All filters",
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
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => SampleWindow.Scope((trace, data) => a.Scope[trace].Update(data)),
            });
        }

        private enum EmbedFftLabel { Normal, FFT, FFT2D }

        private static void AddTraceEmbeddedMenu(List<ScopeContextMenu.EmbeddedMenu> embeddedContextMenu)
        {
            EmbedFftLabel FftLabel(TraceView view)
            {
                return view.MathType == TraceView.MathTypes.FFT10Log10 || view.MathType == TraceView.MathTypes.FFT20Log10
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
                    a.View.MathType = (fft == EmbedFftLabel.FFT) ? TraceView.MathTypes.FFT10Log10 : TraceView.MathTypes.Normal;
                    a.View.PaintMode = (fft == EmbedFftLabel.FFT2D) ? TraceView.PaintModes.FFT2D : TraceView.PaintModes.PolygonDigital;
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].PaintMode == TraceView.PaintModes.XYLine ? true : false,
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
                    a.Views[0].MathType = a.Views[0].MathType == TraceView.MathTypes.Normal ? TraceView.MathTypes.FFT10Log10 : TraceView.MathTypes.Normal;
                    a.Views[0].MathPhase = TraceView.CalculatePhases.BeforeZoom;
                    a.Views[0].AutoRange();
                },
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].MathType != TraceView.MathTypes.Normal,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].PaintMode == TraceView.PaintModes.FFT2D,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].AutoReduceRange,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].HoldPanZoom,
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Views[0].ShowPictureInPicture,
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
                    TraceView.CalculatedTypes.Resample => Create("Resample", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataCount()),
                    TraceView.CalculatedTypes.PolyFilter => Create("PolyFilter", math, ScopeContextMenu.MenuItem.ShowWhen.OnePlusSelected, ScopeContextMenu.MenuItem.CallWhen.PerTrace, new TraceView.CalculatedTraceDataOrder()),

                    TraceView.CalculatedTypes.Atan2 => Create("Atan2", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.RescaledError => Create("RescaledError", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.NormalisedError => Create("NormalisedError", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.FIR => Create("FIR", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Subtract => Create("Subtract", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Difference => Create("Difference", math, ScopeContextMenu.MenuItem.ShowWhen.TwoSelected, ScopeContextMenu.MenuItem.CallWhen.Once),

                    TraceView.CalculatedTypes.Sum => Create("Sum", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Magnitude => Create("Magnitude", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Product => Create("Product", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),
                    TraceView.CalculatedTypes.Mean => Create("Mean", math, ScopeContextMenu.MenuItem.ShowWhen.TwoPlusSelected, ScopeContextMenu.MenuItem.CallWhen.Once),

                    TraceView.CalculatedTypes.PythonScript => null,
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
                    view.CalculatedParameter = data ?? new TraceView.CalculatedTraceData();
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
            List<double[]> result = new List<double[]>();

            bool foundFirstTrigger = false;
            int startSliceIndex = 0;

            for (int loop = 1; loop < samples.Length; loop++)
            {
                bool trigger;
                switch (TriggeredSliceInfo.Phase)
                {
                    default:
                    case TriggeredSliceForm.TriggerPhase.Rising:
                        trigger = (samples[loop - 1] < TriggeredSliceInfo.TriggerValue && samples[loop] >= TriggeredSliceInfo.TriggerValue);
                        break;
                    case TriggeredSliceForm.TriggerPhase.Falling:
                        trigger = (samples[loop - 1] > TriggeredSliceInfo.TriggerValue && samples[loop] <= TriggeredSliceInfo.TriggerValue);
                        break;
                }
                if (trigger)
                {
                    if (!foundFirstTrigger)
                    {
                        startSliceIndex = loop;
                        foundFirstTrigger = true;
                    }
                    else
                    {
                        if ((loop - startSliceIndex) >= TriggeredSliceInfo.PostTriggerMinimumSamples)
                        {
                            Add(startSliceIndex, loop);
                            startSliceIndex = loop;
                        }
                    }
                }
            }
            if (startSliceIndex != 0)
            {
                Add(startSliceIndex, samples.Length);
            }

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

            void Add(int start, int end)
            {
                int pretrigger = TriggeredSliceInfo.PreTriggerSamples;
                result.Add(samples.Copy(start - pretrigger, end - start + pretrigger));
            }
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
                GetStyle = (a) => a.Menu.Menu.Checked = a.Scope.ShowTraceFeatures,
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
}
