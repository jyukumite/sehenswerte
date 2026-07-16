using GroBuf;
using GroBuf.DataMembersExtracters;
using Microsoft.VisualBasic.FileIO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Controls;
using SehensWerte.Controls.Sehens;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static SehensWerte.Controls.Sehens.TraceData;

namespace SehensWerte
{
    public class SehensSave
    {
        public class Sehens
        {
            [XmlAnyElement]
            public List<XmlElement> OtherElements = new List<XmlElement>();

            public TraceNameHints Hints = new TraceNameHints();
            public Skin ViewSkin = new Skin();
            public Skin ScreenshotSkin = new Skin();
            public List<string[]> Groups = new List<string[]>();
            public List<View> Views = new List<View>();
            public List<Trace> Traces = new List<Trace>();
            //XmlSerialisableDictionary

            public Sehens() { }

            public Sehens(SehensControl obj, bool copySamples = true)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
                Hints = new TraceNameHints().MergedWith(obj.TraceNameHints);
                ViewSkin = new Skin(obj.ActiveSkin);
                ScreenshotSkin = new Skin(obj.ScreenshotSkin);
                Groups = obj.AllViewGroupNames;
                Views = obj.AllViews.Select(x => new View(x)).ToList();
                Traces = obj.AllTraces.Select(x => new Trace(x, copySamples)).ToList();
            }

            // destructive restore: closes and recreates every saved view/trace, including sample data - the binary load path
            public void SaveTo(SehensControl obj)
            {
                XmlSaveAttribute.Inject(obj, OtherElements);
                XmlSaveAttribute.Inject(obj.ActiveSkin, ViewSkin.OtherElements);
                XmlSaveAttribute.Inject(obj.ScreenshotSkin, ScreenshotSkin.OtherElements);

                var traces = Traces.ToDictionary(a => a.Name, b => b);
                foreach (var view in Views)
                {
                    TraceData td = new TraceData();
                    if (traces.TryGetValue(view.TraceName, out var trace))
                    {
                        trace.SaveTo(td);
                    }

                    obj.ViewByName(view.ViewName)?.Close();
                    obj.TraceByName(view.TraceName)?.Close();
                    TraceView tv = new TraceView(obj, td, view.ViewName);
                    view.SaveTo(tv);
                    obj.AddView(tv);
                }

                foreach (var view in Views)
                { //fixup CalculatedSourceViews and TriggerViews
                    TraceView? tv = obj.ViewByName(view.ViewName);
                    if (tv != null)
                    {
                        tv.CalculatedSourceViews.AddRange(
                                view.CalculatedSourceViews.Select(
                                    x => obj.EnsureView(x)));
                        if (view.TriggerView != "")
                        {
                            tv.TriggerView = obj.ViewByName(view.TriggerView);
                        }
                    }
                }

                foreach (var group in Groups)
                {
                    obj.GroupViews(group);
                }
            }

            // non-destructive apply over the currently loaded traces, fuzzy match via hints
            public void ApplyTo(SehensControl obj)
            {
                obj.BeginUpdate();
                try
                {
                    XmlSaveAttribute.Inject(obj, OtherElements);
                    XmlSaveAttribute.Inject(obj.ActiveSkin, ViewSkin.OtherElements);
                    XmlSaveAttribute.Inject(obj.ScreenshotSkin, ScreenshotSkin.OtherElements);

                    TraceView[] liveViews = obj.AllViews;
                    TraceNameHints scopeHints = obj.TraceNameHints;
                    var resolveCache = new Dictionary<string, List<(TraceView View, string Prefix, string Suffix)>>();

                    List<(TraceView View, string Prefix, string Suffix)> Resolve(string savedName)
                    {
                        if (resolveCache.TryGetValue(savedName, out var cached))
                        {
                            return cached;
                        }
                        var matches = liveViews
                            .Where(x => x.ViewName == savedName)
                            .Select(x => (View: x, Prefix: "", Suffix: ""))
                            .ToList();
                        if (matches.Count == 0 && !Hints.IsEmpty)
                        {
                            string savedBase = Hints.Strip(savedName);
                            foreach (TraceView view in liveViews)
                            {
                                if (scopeHints.Strip(view.ViewName, out string prefix, out string suffix) == savedBase)
                                {
                                    matches.Add((view, prefix, suffix));
                                }
                            }
                        }
                        resolveCache[savedName] = matches;
                        return matches;
                    }

                    var matched = new HashSet<TraceView>();
                    foreach (View entry in Views)
                    {
                        foreach (var match in Resolve(entry.ViewName))
                        {
                            entry.ApplyTo(match.View);
                            matched.Add(match.View);
                        }
                    }

                    foreach (TraceView view in matched)
                    { // clear stale membership; groups are rebuilt below
                        view.GroupWithView = "";
                    }

                    var rank = new Dictionary<TraceView, int>();
                    for (int groupIndex = 0; groupIndex < Groups.Count; groupIndex++)
                    {
                        var members = new List<(TraceView View, string Prefix, string Suffix, int SavedIndex)>();
                        string[] group = Groups[groupIndex];
                        for (int savedIndex = 0; savedIndex < group.Length; savedIndex++)
                        {
                            foreach (var match in Resolve(group[savedIndex]))
                            {
                                members.Add((match.View, match.Prefix, match.Suffix, savedIndex));
                            }
                        }
                        foreach (var member in members)
                        {
                            if (!rank.ContainsKey(member.View))
                            {
                                rank[member.View] = groupIndex;
                            }
                        }
                        // partition by decoration so two loaded files regroup as two parallel groups instead of merging into one
                        foreach (var partition in members.GroupBy(x => (x.Prefix, x.Suffix)))
                        {
                            var ordered = partition.OrderBy(x => x.SavedIndex).Select(x => x.View).ToList();
                            if (ordered.Count >= 2)
                            {
                                obj.GroupViews(ordered);
                            }
                        }
                    }

                    // saved order first; unmatched views keep relative order at the end
                    obj.OrderViewGroups(x => rank.TryGetValue(x, out int value) ? value : int.MaxValue);
                }
                finally
                {
                    obj.EndUpdate();
                }
            }
        }

        public class Skin
        {
            [XmlAnyElement]
            public List<XmlElement> OtherElements = new List<XmlElement>();

            public Skin() { }

            public Skin(Controls.Sehens.Skin obj)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
            }

            public void SaveTo(Controls.Sehens.Skin obj)
            {
                XmlSaveAttribute.Inject(obj, OtherElements);
            }
        }

        public class View
        {
            [XmlAnyElement]
            public List<XmlElement> OtherElements = new List<XmlElement>();

            public List<string> CalculatedSourceViews = new List<String>();
            public string TraceName = "";
            public string ViewName = "";
            public string TriggerView = "";

            public View() { }

            public View(TraceView obj)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
                CalculatedSourceViews = obj.CalculatedSourceViews.Select(x => x.ViewName).ToList();
                TraceName = obj.Samples.Name;
                ViewName = obj.ViewName;
                TriggerView = obj.TriggerView?.ViewName ?? "";
            }

            public void SaveTo(TraceView obj)
            {
                TranslateLegacyTraceXml(OtherElements);
                XmlSaveAttribute.Inject(obj, OtherElements);
                //CalculatedSourceViews fixup after loading all views
                //TriggerView fixup after loading all views
            }

            // GroupWithView holds a raw sibling view name that cannot resolve after fuzzy matching
            // Sehens.ApplyTo rebuilds grouping from Groups instead. Selected is transient UI state.
            private static readonly string[] ApplyExcludedElements = { "GroupWithView", "Selected" };

            // non-destructive variant of SaveTo, used by Sehens.ApplyTo:
            // injects view state into an existing live view, excluding grouping/selection which the caller rebuilds
            public void ApplyTo(TraceView obj)
            {
                TranslateLegacyTraceXml(OtherElements);
                XmlSaveAttribute.Inject(obj,
                    OtherElements.Where(x => !ApplyExcludedElements.Contains(x.Name)).ToList());
            }

            // Rewrites legacy <MathType>/<LogVertical>/<LogHorizontal>
            private static void TranslateLegacyTraceXml(List<XmlElement> elements)
            {
                if (elements == null) return;
                foreach (var el in elements)
                {
                    if (el.Name == "LogVertical")
                    {
                        if (el.InnerText == "True") el.InnerText = "Log";
                        else if (el.InnerText == "False") el.InnerText = "Off";
                    }
                    else if (el.Name == "LogHorizontal")
                    {
                        if (el.InnerText == "True") el.InnerText = "Log";
                        else if (el.InnerText == "False") el.InnerText = "Off";
                    }
                }
                XmlElement? math = elements.FirstOrDefault(e => e.Name == "MathType");
                if (math != null && (math.InnerText == "FFT10Log10" || math.InnerText == "FFT20Log10"))
                {
                    string dbValue = math.InnerText == "FFT20Log10" ? "dB20" : "dB10";
                    math.InnerText = "FFTMagnitude";
                    XmlElement? logV = elements.FirstOrDefault(e => e.Name == "LogVertical");
                    if (logV != null)
                    {
                        logV.InnerText = dbValue;
                    }
                    else
                    {
                        XmlElement created = math.OwnerDocument.CreateElement("LogVertical");
                        created.InnerText = dbValue;
                        elements.Add(created);
                    }
                }
            }
        }

        public class Data
        {
            [XmlAnyElement]
            public List<XmlElement> OtherElements = new List<XmlElement>();
            public TraceFeature[] Features = new TraceFeature[0];
            [XmlIgnore]
            public double[] InputSamples = new double[0]; // converted to double
            [XmlIgnore]
            public double[]? UnixTime = null;

            public Data() { }
            internal Data(DataStore obj, bool copySamples = true)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
                Features = obj.Features.Select(x => (TraceFeature)x.Clone()).ToArray();
                if (copySamples)
                { // samples are [XmlIgnore]; only the binary save needs the copies
                    InputSamples = obj.InputSamples.CopyToDoubleArray();
                    UnixTime = obj.UnixTime == null ? null : obj.UnixTime.CopyToDoubleArray();
                }
            }

            internal void SaveTo(DataStore obj)
            {
                XmlSaveAttribute.Inject(obj, OtherElements);
                obj.Features = Features.ToList();
                obj.InputSamples = InputSamples;
                obj.UnixTime = UnixTime;
            }
        }

        public class Trace
        {
            [XmlAnyElement]
            public List<XmlElement> OtherElements = new List<XmlElement>();
            public Data InputData = new Data();
            public Data? ViewedData = new Data(); // never deserialises as null
            public string Name = "";
            public double[]? HorizontalAxisValues = null;
            public string HorizontalAxisUnit = "";

            public Trace() { }

            public Trace(TraceData obj, bool copySamples = true)
            {
                lock (obj.DataLock)
                {
                    Name = obj.Name;
                    OtherElements = XmlSaveAttribute.Extract(obj);
                    InputData = new Data(obj.SaveInputData, copySamples);
                    DataStore? saveViewedData = obj.SaveViewedData;
                    ViewedData = saveViewedData == null ? null : new Data(saveViewedData, copySamples);
                    if (copySamples)
                    {
                        HorizontalAxisValues = obj.HorizontalAxisValues;
                        HorizontalAxisUnit = obj.HorizontalAxisUnit;
                    }
                }
            }

            public void SaveTo(TraceData obj)
            {
                lock (obj.DataLock)
                {
                    obj.Name = Name;
                    XmlSaveAttribute.Inject(obj, OtherElements);
                    if (HorizontalAxisValues != null)
                    {
                        obj.SetHorizontalAxis(HorizontalAxisValues, HorizontalAxisUnit);
                    }

                    var inputData = new DataStore();
                    InputData.SaveTo(inputData);
                    obj.SaveInputData = inputData;

                    if (obj.StopUpdates && ViewedData != null)
                    {
                        var viewedData = new DataStore();
                        ViewedData.SaveTo(inputData);
                        obj.SaveViewedData = viewedData;
                    }
                }
            }
        }

        internal static void SaveStateXml(string filename, SehensControl scope)
        {
            // SehensControl is a user control, makes it difficult to extract XML
            string xml = new Sehens(scope, copySamples: false).ToXml();
            System.IO.File.WriteAllText(filename, xml);
        }

        internal static void LoadStateXml(string filename, SehensControl scope)
        {
            // XML state carries no sample data, so it applies over the live traces (fuzzy name matching) rather than rebuilding them
            string xml = System.IO.File.ReadAllText(filename);
            Sehens? newScope = xml.FromXml<Sehens>();
            newScope?.ApplyTo(scope);
        }

        internal class BinaryTrace
        {
            public double[] InputSamples = new double[0];
            public double[]? InputUnixTime = new double[0];
            public double[]? ViewedSamples = new double[0];
            public double[]? ViewedUnixTime = new double[0];
        }

        internal class BinarySave
        {
            public string Version = "1";
            public string Xml = "";
            public Dictionary<string, BinaryTrace> Traces = new();
        }

        internal static void SaveStateBinary(string filename, SehensControl scope)
        {
            var save = new Sehens(scope);
            var binary = new BinarySave();
            binary.Xml = save.ToXml();
            binary.Traces = save.Traces.ToDictionary(
                a => a.Name,
                b => new BinaryTrace()
                {
                    InputSamples = b.InputData.InputSamples,
                    InputUnixTime = b.InputData?.UnixTime,
                    ViewedSamples = b.ViewedData?.InputSamples,
                    ViewedUnixTime = b.ViewedData?.UnixTime
                });

            var serializer = new GroBuf.Serializer(new FieldsExtractor(), options: GroBufOptions.WriteEmptyObjects);
            byte[] serialized = serializer.Serialize(binary);
            var file = Compression.GZipCompress(serialized);
            System.IO.File.WriteAllBytes(filename, file);
        }

        internal static void LoadStateBinary(string filename, SehensControl scope)
        {
            scope.ExceptionToMessagebox(() =>
            {
                var serializer = new GroBuf.Serializer(new FieldsExtractor(), options: GroBufOptions.WriteEmptyObjects);
                var compressed = System.IO.File.ReadAllBytes(filename);
                var uncompressed = Compression.GZipDecompress(compressed);
                var binary = serializer.Deserialize<BinarySave>(uncompressed);

                Sehens? newScope = binary.Xml.FromXml<Sehens>();
                if (newScope != null)
                {
                    var traces = newScope.Traces.ToDictionary(a => a.Name, b => b);
                    // copy binary trace data into loaded xml trace data
                    foreach (var binTrace in binary.Traces)
                    {
                        BinaryTrace binData = binTrace.Value;
                        var trace = traces[binTrace.Key];

                        trace.InputData.InputSamples = binData.InputSamples;
                        if ((binData.InputUnixTime?.Length ?? 0) != 0)
                        {
                            trace.InputData.UnixTime = binData.InputUnixTime;
                        }
                        if ((binData.ViewedSamples?.Length ?? 0) > 0 && trace.ViewedData != null)
                        {
                            trace.ViewedData.InputSamples = binData.ViewedSamples!;
                            if ((binData.ViewedUnixTime?.Length ?? 0) != 0)
                            {
                                trace.ViewedData.UnixTime = binData.ViewedUnixTime;
                            }
                        }
                    }
                    newScope.SaveTo(scope);
                }
            }, "Load saved state");
        }

    }


    [TestClass]
    public class SehensSaveTest
    {
        [TestMethod]
        public void TestExactRoundTrip()
        {
            var scope = new SehensControl();
            scope["A"].Update(new double[] { 1, 2, 3 });
            scope["B"].Update(new double[] { 4, 5, 6 });
            scope["C"].Update(new double[] { 7, 8, 9 });
            scope.GroupViews(new[] { "A", "B" });
            SetView(scope, "A", Color.Red, visible: true);
            SetView(scope, "B", Color.Blue, visible: false);
            SetView(scope, "C", Color.Green, visible: true);

            string xml = new SehensSave.Sehens(scope, copySamples: false).ToXml(compact: true);

            SetView(scope, "A", Color.Black, visible: false);
            SetView(scope, "B", Color.Black, visible: true);
            RequireView(scope, "B").GroupWithView = "";

            SehensSave.Sehens layout = xml.FromXml<SehensSave.Sehens>()
                ?? throw new AssertFailedException("state xml failed to parse");
            layout.ApplyTo(scope);

            Assert.AreEqual(Color.Red.ToArgb(), RequireView(scope, "A").Colour.ToArgb());
            Assert.IsTrue(RequireView(scope, "A").Visible);
            Assert.AreEqual(Color.Blue.ToArgb(), RequireView(scope, "B").Colour.ToArgb());
            Assert.IsFalse(RequireView(scope, "B").Visible);
            Assert.AreEqual("A", RequireView(scope, "B").GroupWithView);
            // apply must not disturb the live sample data
            Assert.AreEqual(3, RequireView(scope, "A").Samples.InputSampleCount);
        }

        [TestMethod]
        public void TestFuzzyApply()
        {
            var scope1 = new SehensControl();
            scope1["A_f1"].Update(new double[] { 1, 2, 3 });
            scope1["B_f1"].Update(new double[] { 4, 5, 6 });
            scope1.GroupViews(new[] { "A_f1", "B_f1" });
            SetView(scope1, "A_f1", Color.Red, visible: true);
            SetView(scope1, "B_f1", Color.Blue, visible: false);
            scope1.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1" } };

            string xml = new SehensSave.Sehens(scope1, copySamples: false).ToXml(compact: true);

            var scope2 = new SehensControl();
            scope2["C_f2"].Update(new double[] { 7, 8, 9 }); // added first: apply must reorder after saved groups
            scope2["B_f2"].Update(new double[] { 4, 5, 6 });
            scope2["A_f2"].Update(new double[] { 1, 2, 3 });
            scope2.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f2" } };
            Color untouched = RequireView(scope2, "C_f2").Colour;

            SehensSave.Sehens layout = xml.FromXml<SehensSave.Sehens>()
                ?? throw new AssertFailedException("state xml failed to parse");
            layout.ApplyTo(scope2);

            Assert.AreEqual(Color.Red.ToArgb(), RequireView(scope2, "A_f2").Colour.ToArgb());
            Assert.IsTrue(RequireView(scope2, "A_f2").Visible);
            Assert.AreEqual(Color.Blue.ToArgb(), RequireView(scope2, "B_f2").Colour.ToArgb());
            Assert.IsFalse(RequireView(scope2, "B_f2").Visible);
            Assert.AreEqual("A_f2", RequireView(scope2, "B_f2").GroupWithView);
            Assert.AreEqual(untouched.ToArgb(), RequireView(scope2, "C_f2").Colour.ToArgb());

            var groups = scope2.AllViewGroupNames;
            CollectionAssert.AreEqual(new[] { "A_f2", "B_f2" }, groups[0]); // saved group first, saved leader first
            CollectionAssert.AreEqual(new[] { "C_f2" }, groups[1]); // unmatched at the end
        }

        [TestMethod]
        public void TestTwoFilesLoaded()
        {
            var scope1 = new SehensControl();
            scope1["A_f1"].Update(new double[] { 1, 2, 3 });
            scope1["B_f1"].Update(new double[] { 4, 5, 6 });
            scope1.GroupViews(new[] { "A_f1", "B_f1" });
            SetView(scope1, "A_f1", Color.Red, visible: true);
            scope1.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1" } };
            string xml = new SehensSave.Sehens(scope1, copySamples: false).ToXml(compact: true);

            var scope2 = new SehensControl();
            scope2["A_f2"].Update(new double[] { 1, 2 });
            scope2["B_f2"].Update(new double[] { 3, 4 });
            scope2["A_f3"].Update(new double[] { 5, 6 });
            scope2["B_f3"].Update(new double[] { 7, 8 });
            scope2.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f2", "_f3" } };

            SehensSave.Sehens layout = xml.FromXml<SehensSave.Sehens>()
                ?? throw new AssertFailedException("state xml failed to parse");
            layout.ApplyTo(scope2);

            // both files styled, but grouped per file - not merged into one group
            Assert.AreEqual(Color.Red.ToArgb(), RequireView(scope2, "A_f2").Colour.ToArgb());
            Assert.AreEqual(Color.Red.ToArgb(), RequireView(scope2, "A_f3").Colour.ToArgb());
            Assert.AreEqual("A_f2", RequireView(scope2, "B_f2").GroupWithView);
            Assert.AreEqual("A_f3", RequireView(scope2, "B_f3").GroupWithView);
            Assert.AreNotSame(RequireView(scope2, "A_f2").Group, RequireView(scope2, "A_f3").Group);
        }

        [TestMethod]
        public void TestExactBeatsFuzzy()
        {
            var scope1 = new SehensControl();
            scope1["A_f1"].Update(new double[] { 1, 2, 3 });
            SetView(scope1, "A_f1", Color.Red, visible: true);
            scope1.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1" } };
            string xml = new SehensSave.Sehens(scope1, copySamples: false).ToXml(compact: true);

            var scope2 = new SehensControl();
            scope2["A_f1"].Update(new double[] { 1, 2, 3 }); // literal match
            scope2["A_f2"].Update(new double[] { 4, 5, 6 }); // fuzzy candidate
            scope2.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1", "_f2" } };
            Color untouched = RequireView(scope2, "A_f2").Colour;

            SehensSave.Sehens layout = xml.FromXml<SehensSave.Sehens>()
                ?? throw new AssertFailedException("state xml failed to parse");
            layout.ApplyTo(scope2);

            Assert.AreEqual(Color.Red.ToArgb(), RequireView(scope2, "A_f1").Colour.ToArgb());
            Assert.AreEqual(untouched.ToArgb(), RequireView(scope2, "A_f2").Colour.ToArgb());
        }

        [TestMethod]
        public void TestManyViewsRoundTrip()
        { // realistic scale: a zip of 5 sessions x ~60 traces
            var scope = new SehensControl();
            var suffixes = new List<string>();
            for (int file = 0; file < 5; file++)
            {
                suffixes.Add($"_file{file}.bin");
                for (int trace = 0; trace < 60; trace++)
                {
                    scope[$"Trace{trace}_file{file}.bin"].Update(new double[] { 1, 2, 3 });
                }
            }
            scope.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = suffixes };

            var timer = System.Diagnostics.Stopwatch.StartNew();
            string xml = new SehensSave.Sehens(scope, copySamples: false).ToXml(compact: true);
            long captureMs = timer.ElapsedMilliseconds;

            timer.Restart();
            SehensSave.Sehens layout = xml.FromXml<SehensSave.Sehens>()
                ?? throw new AssertFailedException("state xml failed to parse");
            layout.ApplyTo(scope);
            long applyMs = timer.ElapsedMilliseconds;

            Console.WriteLine($"capture+serialise {captureMs}ms, parse+apply {applyMs}ms, xml {xml.Length} chars, {scope.AllViews.Length} views");
            Assert.AreEqual(300, scope.AllViews.Length);
            // regression guard for the uncached XmlSerializer(Type, Type[]) trap:
            // each construction code-gens a dynamic assembly (5-20ms); 300 views x 2
            // nested members made save and load take seconds each
            Assert.IsTrue(captureMs + applyMs < 2000, $"round trip too slow: {captureMs}+{applyMs}ms");
        }

        private static TraceView RequireView(SehensControl scope, string name)
        {
            return scope.ViewByName(name) ?? throw new AssertFailedException($"view {name} missing");
        }

        private static void SetView(SehensControl scope, string name, Color colour, bool visible)
        {
            TraceView view = RequireView(scope, name);
            view.Colour = colour;
            view.Visible = visible;
        }
    }

}
