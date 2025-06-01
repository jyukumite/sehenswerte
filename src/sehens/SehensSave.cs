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

            public Skin ViewSkin = new Skin();
            public Skin ScreenshotSkin = new Skin();
            public List<string[]> Groups = new List<string[]>();
            public List<View> Views = new List<View>();
            public List<Trace> Traces = new List<Trace>();
            //XmlSerialisableDictionary

            public Sehens() { }

            public Sehens(SehensControl obj)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
                ViewSkin = new Skin(obj.ActiveSkin);
                ScreenshotSkin = new Skin(obj.ScreenshotSkin);
                Groups = obj.AllViewGroupNames;
                Views = obj.AllViews.Select(x => new View(x)).ToList();
                Traces = obj.AllTraces.Select(x => new Trace(x)).ToList();
            }

            public void SaveTo(SehensControl obj)
            {
                XmlSaveAttribute.Inject(obj, OtherElements);
                XmlSaveAttribute.Inject(obj.ActiveSkin, ViewSkin.OtherElements);
                XmlSaveAttribute.Inject(obj.ScreenshotSkin, ScreenshotSkin.OtherElements);

                var traces = Traces.ToDictionary(a => a.Name, b => b);
                foreach (var view in Views)
                {
                    TraceData td = new TraceData();
                    traces[view.TraceName].SaveTo(td);

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
                XmlSaveAttribute.Inject(obj, OtherElements);
                //CalculatedSourceViews fixup after loading all views
                //TriggerView fixup after loading all views
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
            internal Data(DataStore obj)
            {
                OtherElements = XmlSaveAttribute.Extract(obj);
                Features = obj.Features.Select(x => (TraceFeature)x.Clone()).ToArray();
                InputSamples = obj.InputSamples.CopyToDoubleArray();
                UnixTime = obj.UnixTime == null ? null : obj.UnixTime.CopyToDoubleArray();
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
            public string Name;

            public Trace() { }

            public Trace(TraceData obj)
            {
                lock (obj.DataLock)
                {
                    Name = obj.Name;
                    OtherElements = XmlSaveAttribute.Extract(obj);
                    InputData = new Data(obj.SaveInputData);
                    DataStore? saveViewedData = obj.SaveViewedData;
                    ViewedData = saveViewedData == null ? null : new Data(saveViewedData);
                }
            }

            public void SaveTo(TraceData obj)
            {
                lock (obj.DataLock)
                {
                    obj.Name = Name;
                    XmlSaveAttribute.Inject(obj, OtherElements);

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
            string xml = new Sehens(scope).ToXml();
            System.IO.File.WriteAllText(filename, xml);
        }

        internal static void LoadStateXml(string filename, SehensControl scope)
        {
            string xml = System.IO.File.ReadAllText(filename);
            var save = new Sehens(scope);
            Sehens? newScope = xml.FromXml<Sehens>();
            newScope?.SaveTo(scope);
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
            public string Xml;
            public Dictionary<string, BinaryTrace> Traces;
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
            try
            {
                var serializer = new GroBuf.Serializer(new FieldsExtractor(), options: GroBufOptions.WriteEmptyObjects);
                var compressed = System.IO.File.ReadAllBytes(filename);
                var uncompressed = Compression.GZipDecompress(compressed);
                var binary = serializer.Deserialize<BinarySave>(uncompressed);

                var save = new Sehens(scope);
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
            }
            catch (Exception ex)//remove
            {
                MessageBox.Show(ex.Message, "Save file appears corrupt");
            }
        }

    }


    [TestClass]
    public class SehensSaveTest
    {
        //fixme: unit tests
        [TestMethod]
        public void TestXml()
        {
        }
        [TestMethod]
        public void TestBianary()
        {
        }
    }

}
