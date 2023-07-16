using SehensWerte.Files;
using SehensWerte.Utils;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace SehensWerte.Controls.Sehens
{
    public class ImportExport
    {
        private static Dictionary<object, object> ImportExportForms = new Dictionary<object, object>();
        private static SaveFileDialog m_SaveFileDialog = new SaveFileDialog();
        private static OpenFileDialog m_OpenFileDialog = new OpenFileDialog();

        private class Traces
        {
            public List<object> Extracted = new List<object>();
            public List<string> Names = new List<string>();
            public List<double> SamplesPerSeconds = new List<double>();
            public List<int> SampleOffsets = new List<int>();
        }

        private enum ExportType
        {
            [Description("Single image")]
            [Extension(".png")]
            [EditFormType(typeof(ExportImageForm))]
            SinglePng,

            [Description("Multiple image")]
            [Extension(".rtf")]
            [EditFormType(typeof(ExportImageForm))]
            MultiplePng,

            [Description("Trace names")]
            [Extension(".txt")]
            TraceNames,

            [Description("Vertical CSV (data in columns, header row)")]
            [Extension(".csv")]
            [EditFormType(typeof(ExportDataForm))]
            CsvVertical,

            [Description("Excel tab separated (data in columns, header row)")]
            [Extension(".tsv")]
            [EditFormType(typeof(ExportDataForm))]
            TsvVertical,

            [Description("WAV File")]
            [Extension(".wav")]
            [EditFormType(typeof(ExportDataForm))]
            Wav,

            [Description("Matlab space separated values (fscanf_mat)")]
            [Extension(".ssv")]
            [EditFormType(typeof(ExportDataForm))]
            SpaceSeparatedValues,

            [Description("Binary Sehens File")]
            [Extension(".sehens")]
            SehensBinary,

            [Description("XML Sehens File")]
            [Extension(".xml")]
            SehensXML
        }

        private enum ImportType
        {
            [Description("Vertical CSV (data in columns, header row)")]
            [Extension(".csv")]
            [EditFormType(typeof(ImportDataVerticalCsvForm))]
            CsvVertical,

            [Description("Vertical CSV (data in columns, header row, gzip compressed)")]
            [Extension(".csv.gz")]
            [EditFormType(typeof(ImportDataVerticalCsvForm))]
            CsvGzVertical,

            [Description("Vertical tab separated (excel - data in columns, header row)")]
            [Extension(".tsv")]
            [EditFormType(typeof(ImportDataVerticalCsvForm))]
            TsvVertical,

            [Description("RIFF WAV file")]
            [Extension(".wav")]
            [EditFormType(typeof(ImportDataWavForm))]
            Wav,

            [Description("8-bit binary, 8 traces")]
            [Extension(".bin")]
            [EditFormType(typeof(ImportDataBinaryForm))]
            Binary8bit,

            [Description("Saleae Analysis output as features (CSV of rows seconds,data,,)")]
            [Extension(".bin")]
            [EditFormType(typeof(ImportDataSaleaeAnalysisForm))]
            SaleaeAnalysis,

            [Description("Events as features (time [space] text) over time trace")]
            [Extension(".txt")]
            [EditFormType(typeof(ImportDataFeaturesForm))]
            Features,

            [Description("Binary Sehens State File")]
            [Extension(".sehens")]
            SehensBinaryState,

            [Description("XML Sehens State File")]
            [Extension(".xml")]
            SehensXMLState
        }

        [AttributeUsage(AttributeTargets.All)]
        private class ExtensionAttribute : Attribute
        {
            public string Extension;
            public ExtensionAttribute(string description) { Extension = description; }
        }

        [AttributeUsage(AttributeTargets.All)]
        private class EditFormTypeAttribute : Attribute
        {
            public Type Form;
            public EditFormTypeAttribute(Type form) { Form = form; }
        }

        private class ExportDataFormBase
        {
            public enum Destination { File, Clipboard, TemporaryFile }

            [AutoEditorForm.Disabled]
            [AutoEditorForm.DisplayOrder(-2)]
            public string Filename = "";

            [AutoEditorForm.Disabled]
            [AutoEditorForm.DisplayName("Target")]
            public Destination Result;
        }

        private class ExportDataForm : ExportDataFormBase
        {
            public enum Scope { SelectedDisplayedSamples, SelectedTracesBeforeZoom, VisibleTracesBeforeZoom, AllTracesBeforeZoom }

            [AutoEditorForm.DisplayName("Scope")]
            virtual public Scope ExportScope { get; set; }
        }

        private class ExportImageForm : ExportDataFormBase
        {
            [AutoEditorForm.DisplayName("High quality render")]
            [AutoEditorForm.DisplayOrder(0)]
            public bool HighQualityRender { get => Skin.HighQualityRender; set => Skin.HighQualityRender = value; }

            [AutoEditorForm.DisplayName("Trace width (pixels)")]
            [AutoEditorForm.DisplayOrder(-5)]
            public int TraceWidth { get => Skin.TraceWidth; set => Skin.TraceWidth = value; }

            [AutoEditorForm.DisplayName("Per trace height (pixels)")]
            [AutoEditorForm.DisplayOrder(-5)]
            public int TraceHeight { get => Skin.TraceHeight; set => Skin.TraceHeight = value; }

            [AutoEditorForm.DisplayName("Scope")]
            [AutoEditorForm.DisplayOrder(-5)]
            public Skin.TraceSelections ExportScope { get => Skin.ExportTraces; set => Skin.ExportTraces = value; }

            [AutoEditorForm.DisplayOrder(0)]
            [AutoEditorForm.SubEditor]
            public Skin Skin = new Skin(Skin.CannedSkins.ScreenShot);

        }

        private class ImportDataForm
        {
            public enum Source { File, Clipboard, TemporaryFile }

            [AutoEditorForm.Hidden]
            public string[] Filenames = new string[0];

            [AutoEditorForm.Disabled]
            [AutoEditorForm.DisplayOrder(-1)]
            public ImportType FileType;

            [AutoEditorForm.DisplayName("Data source")]
            [AutoEditorForm.Disabled]
            public Source DataSource;

            [AutoEditorForm.DisplayName("Append filename prefix")]
            public bool AppendFilenamePrefix = false;

            [AutoEditorForm.DisplayName("Fix data length")]
            public int ForceDataLength = 0;

            [AutoEditorForm.Disabled]
            [AutoEditorForm.DisplayOrder(-2)]
            public string Filename
            {
                get => string.Join(", ", Filenames);
                private set { }
            }

            public virtual void ApplyTo(TraceData trace)
            {
                if (ForceDataLength != 0)
                {
                    trace.FirstView!.ViewLengthOverride = ForceDataLength;
                }
            }
        }

        private class ImportDataVerticalCsvForm : ImportDataForm
        {
            [AutoEditorForm.DisplayName("Samples per second")]
            public double SamplesPerSecond;

            [AutoEditorForm.DisplayName("Header row prefix")]
            public string HeaderLinePrefix = "";

            [AutoEditorForm.DisplayName("Append index suffix")]
            public bool AddIndex = true;

            [AutoEditorForm.DisplayName("Column match regex")]
            public string ColumnMatchRegex = "";

            [AutoEditorForm.DisplayName("Trace name suffix")]
            public string TraceNameSuffix = "";

            [AutoEditorForm.DisplayName("Trace prefix")]
            public string TraceNamePrefix = "Csv.";

            [AutoEditorForm.DisplayName("Remove invalid samples")]
            public bool RemoveNAN = true;
        }

        private class ImportDataWavForm : ImportDataForm
        {
            [AutoEditorForm.DisplayName("Trace name suffix")]
            public string TraceNameSuffix = "";

            [AutoEditorForm.DisplayName("Trace prefix")]
            public string TraceNamePrefix = "Wave.";
        }

        private class ImportDataBinaryForm : ImportDataForm
        {
            [AutoEditorForm.DisplayName("Samples per second")]
            public double SamplesPerSecond = 10000;

            [AutoEditorForm.DisplayName("Decimation factor")]
            public int DecimationFactor = 1;

            [AutoEditorForm.DisplayName("Trace name suffix")]
            public string TraceNameSuffix = "";

            [AutoEditorForm.DisplayName("Trace prefix")]
            public string TraceNamePrefix = "Binary.";
        }

        private class ImportDataSaleaeAnalysisForm : ImportDataForm
        {
            [AutoEditorForm.DisplayName("Samples per second")]
            public double SamplesPerSecond = 100000;

            [AutoEditorForm.DisplayName("Target trace view name")]
            public string TargetTrace = "";

            [AutoEditorForm.DisplayName("Trace name suffix")]
            public string TraceNameSuffix = "";

            [AutoEditorForm.DisplayName("Trace prefix")]
            public string TraceNamePrefix = "Logic.";
        }

        private class ImportDataFeaturesForm : ImportDataForm
        {
            [AutoEditorForm.DisplayName("Target trace view name")]
            public string TargetTrace = "";
        }

        public static string FilterText<T>()
        {
            return string.Join("|",
                Enum.GetValues(typeof(T)).Cast<T>()
                .Select((T a) => GetDescription(a) + "|*" + GetExtension(a))
                );
        }

        private static string GetDescription<T>(T value)
        {
            object[] customAttributes = typeof(T).GetField(value?.ToString() ?? "")?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false) ?? new object[0];
            return customAttributes.Length == 0 ? (value?.ToString() ?? "") : ((DescriptionAttribute)customAttributes[0]).Description;
        }

        private static string GetExtension<T>(T value)
        {
            object[] customAttributes = typeof(T).GetField(value?.ToString() ?? "")?.GetCustomAttributes(typeof(ExtensionAttribute), inherit: false) ?? new object[0];
            return customAttributes.Length == 0 ? value?.ToString() ?? "" : ((ExtensionAttribute)customAttributes[0]).Extension;
        }

        private static string[] GetDescriptions<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>().Select(a => GetDescription(a)).ToArray();
        }

        public static void AddContextMenus(List<ScopeContextMenu.MenuItem> contextMenu, List<ScopeContextMenu.EmbeddedMenu> embeddedContextMenu)
        {
            const string subMenuText = "Import/Export";

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Save state",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => SaveStateDialog(a.Scope),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Load state",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => LoadStateDialog(a.Scope),
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Export file",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => ExportDialog(a.Scope, a),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.S
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Export file (to clipboard)",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.TracesPresent,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) =>
                {
                    try
                    {
                        string? text = ListSelectForm.Show("Export", "Export", GetDescriptions<ExportType>(), GetDescription(ExportType.SinglePng));
                        if (text != null)
                        {
                            Export(a, ValueFromDescription<ExportType>(text), ExportDataForm.Destination.Clipboard);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Can't export");
                    }
                },
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.C
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Import file",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = (a) => ImportDialog(a.Scope, null, "", a.Views.Count == 0 ? "" : a.Views[0].Samples.Name),
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.O
            });

            contextMenu.Add(new ScopeContextMenu.MenuItem
            {
                SubMenuText = subMenuText,
                Text = "Import from clipboard",
                ShownWhenTrace = ScopeContextMenu.MenuItem.ShowWhen.Always,
                ShownWhenMouse = PaintBoxMouseInfo.GuiSection.Anywhere,
                Call = ScopeContextMenu.MenuItem.CallWhen.Once,
                ShownText = ScopeContextMenu.MenuItem.TextDisplay.NoChange,
                Clicked = delegate (ScopeContextMenu.DropDownArgs a)
                {
                    string? text = ListSelectForm.Show("Format", "Inport format", GetDescriptions<ImportType>(), GetDescription(ImportType.CsvVertical));
                    if (text != null)
                    {
                        ImportType enumValueFromDescription = ValueFromDescription<ImportType>(text);
                        TraceView traceView = a.Views[0];
                        string file = Path.GetTempFileName();
                        File.WriteAllText(file, Clipboard.GetText());
                        Import(a.Scope, enumValueFromDescription, new[] { file }, "", traceView?.Samples?.Name ?? "", ImportDataForm.Source.Clipboard);
                        File.Delete(file);
                    }
                },
                HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.Ctrl,
                HotKeyCode = Keys.V
            });
        }

        private static Traces ExtractWaveformsToSave(ScopeContextMenu.DropDownArgs a, ExportDataForm edit)
        {
            Traces waveforms = new Traces();
            bool drawnSamples;

            TraceView[] views;
            switch (edit?.ExportScope ?? ExportDataForm.Scope.SelectedDisplayedSamples)
            {
                case ExportDataForm.Scope.SelectedDisplayedSamples:
                    views = ((a.Views.Count == 0) ? a.Scope.VisibleViews : a.Views.ToArray());
                    drawnSamples = true;
                    break;
                case ExportDataForm.Scope.AllTracesBeforeZoom:
                    views = a.Scope.AllViews;
                    drawnSamples = false;
                    break;
                case ExportDataForm.Scope.SelectedTracesBeforeZoom:
                    views = ((a.Views.Count == 0) ? a.Scope.VisibleViews : a.Views.ToArray());
                    drawnSamples = false;
                    break;
                case ExportDataForm.Scope.VisibleTracesBeforeZoom:
                    views = a.Scope.VisibleViews;
                    drawnSamples = false;
                    break;
                default:
                    views = new TraceView[0];
                    drawnSamples = false;
                    break;
            }

            string error = "";
            foreach (TraceView traceView in views)
            {
                if (traceView.ZoomValue != 1.0 || (!traceView.IsRebasedResult && traceView.Samples.InputSampleCount != 0 && traceView.DrawnSamples != null && traceView.DrawnSamples!.Length != traceView.Samples.InputSampleCount))
                {
                    error = "Different lengths";
                }
                if (traceView.PaintMode == TraceView.PaintModes.PeakHold)
                {
                    double[]? min = drawnSamples ? traceView.PeakHoldMinDrawn : traceView.PeakHoldMinAll;
                    if (min != null)
                    {
                        waveforms.Extracted.Add(min);
                        waveforms.Names.Add("\"" + traceView.ViewName + ".Min\"");
                        waveforms.SamplesPerSeconds.Add(traceView.Samples.InputSamplesPerSecond);
                        waveforms.SampleOffsets.Add(traceView.Samples.InputSampleNumberDisplayOffset + (drawnSamples ? traceView.DrawnStartPosition : 0));
                    }

                    double[]? max = drawnSamples ? traceView.PeakHoldMaxDrawn : traceView.PeakHoldMaxAll;
                    if (max != null)
                    {
                        waveforms.Extracted.Add(max);
                        waveforms.Names.Add("\"" + traceView.ViewName + ".Max\"");
                        waveforms.SamplesPerSeconds.Add(traceView.Samples.InputSamplesPerSecond);
                        waveforms.SampleOffsets.Add(traceView.Samples.InputSampleNumberDisplayOffset + (drawnSamples ? traceView.DrawnStartPosition : 0));
                    }
                }
                else
                {
                    double[]? item = drawnSamples ? traceView.DrawnSamples : traceView.CalculatedBeforeZoom;
                    if (item != null)
                    {
                        waveforms.Extracted.Add(item);
                        waveforms.Names.Add("\"" + traceView.ViewName + "\"");
                        waveforms.SamplesPerSeconds.Add(traceView.Samples.InputSamplesPerSecond);
                        waveforms.SampleOffsets.Add(traceView.Samples.InputSampleNumberDisplayOffset + (drawnSamples ? traceView.DrawnStartPosition : 0));
                    }
                }
            }

            if (error != "")
            {
                MessageBox.Show(error, "Error");
            }
            return waveforms;
        }

        private static void ImportDialog(SehensControl display, ImportType? deftype = null, string csvHeaderLinePrefix = "", string targetTrace = "")
        {
            IEnumerable<ImportType> source = Enum.GetValues(typeof(ImportType)).Cast<ImportType>();
            m_OpenFileDialog.Filter = "Files|" + string.Join(";",
                source
                .Where(a => a != ImportType.SehensXMLState)
                .Select(a => "*" + GetExtension(a))
                .Distinct()) + "|" + FilterText<ImportType>();
            m_OpenFileDialog.Title = "Select file";
            m_OpenFileDialog.FilterIndex = deftype == null ? (int)ImportType.CsvVertical : (int)deftype + 2;
            m_OpenFileDialog.Multiselect = true;
            m_OpenFileDialog.RestoreDirectory = true;
            if (m_OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (m_OpenFileDialog.FilterIndex == 0)
                    {
                        MessageBox.Show("Unknown file type");
                    }
                    else if (m_OpenFileDialog.FilterIndex == 1)
                    {
                        string[] fileNames = m_OpenFileDialog.FileNames;
                        LoadWaveformsUsingExtension(display, fileNames, csvHeaderLinePrefix, targetTrace);
                    }
                    else
                    {
                        ImportType type = (ImportType)(m_OpenFileDialog.FilterIndex - 2);
                        Import(display, type, m_OpenFileDialog.FileNames, csvHeaderLinePrefix, targetTrace);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private static void ExportDialog(SehensControl scope, ScopeContextMenu.DropDownArgs a, ExportType deftype = ExportType.CsvVertical)
        {
            m_SaveFileDialog.Filter = FilterText<ExportType>();
            m_SaveFileDialog.Title = "Select file";
            m_SaveFileDialog.FileName = "sehens" + GetExtension(deftype);
            m_SaveFileDialog.FilterIndex = (int)(deftype + 1);
            m_SaveFileDialog.RestoreDirectory = true;
            if (m_SaveFileDialog.ShowDialog() != DialogResult.OK) return;

            if (m_SaveFileDialog.FilterIndex != 0)
            {
                try
                {
                    ExportType type = (ExportType)(m_SaveFileDialog.FilterIndex - 1);
                    Export(a, type, ExportDataForm.Destination.File, m_SaveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
            else
            {
                MessageBox.Show("Unknown type");
            }
        }

        private static void Export(ScopeContextMenu.DropDownArgs a, ExportType type, ExportDataForm.Destination dest, string filename = "", ExportDataFormBase? edit = null)
        {
            Traces? waveforms = null;
            if (dest == ExportDataForm.Destination.TemporaryFile || dest == ExportDataForm.Destination.Clipboard)
            {
                filename = Path.GetTempFileName() + GetExtension(type);
            }

            bool flag = true;
            if (edit == null)
            {
                using AutoEditorForm autoEditorForm = new AutoEditorForm();
                edit = (ExportDataFormBase?)ImportExportEdit(type, autoEditorForm);
                if (edit == null)
                {
                    flag = false;
                }
                else
                {
                    edit.Filename = filename;
                    edit.Result = dest;
                    flag = autoEditorForm.ShowDialog("Export " + GetExtension(type) + "file", GetDescription(type), edit);
                }
            }
            if (flag && edit is ExportDataForm)
            {
                waveforms = ExtractWaveformsToSave(a, (ExportDataForm)edit);
                if (waveforms.Extracted.Count == 0)
                {
                    MessageBox.Show("Result would be empty");
                    flag = false;
                }
            }
            if (flag)
            {
                switch (type)
                {
                    case ExportType.CsvVertical: SaveCSV((ExportDataForm)edit, waveforms); break;
                    case ExportType.TsvVertical: SaveCSV((ExportDataForm)edit, waveforms, "\t"); break;
                    case ExportType.Wav: SaveWAV((ExportDataForm)edit, waveforms); break;
                    case ExportType.SpaceSeparatedValues: SaveSpaceSeparated(a, (ExportDataForm)edit, waveforms); break;
                    case ExportType.SinglePng: SaveImage((ExportImageForm)edit, a.Scope, filename); break;
                    case ExportType.MultiplePng: SaveImages((ExportImageForm)edit, a.Scope, filename); break;
                    case ExportType.TraceNames: SaveNames(filename, a); break;
                }
                switch (dest)
                {
                    case ExportDataForm.Destination.TemporaryFile:
                        System.Diagnostics.Process.Start(filename);
                        break;

                    case ExportDataForm.Destination.Clipboard:
                        switch (type)
                        {
                            case ExportType.SinglePng:
                                using (Image image = Image.FromFile(filename))
                                {
                                    Clipboard.SetImage(image);
                                    image.Dispose();
                                }
                                break;
                            case ExportType.MultiplePng:
                                Clipboard.SetData(DataFormats.Rtf, File.ReadAllText(filename));
                                break;
                            default:
                                try
                                {
                                    Clipboard.SetText(File.ReadAllText(filename));
                                }
                                catch
                                {
                                    MessageBox.Show("Clipboard error");
                                }
                                break;
                        }
                        try
                        {
                            File.Delete(filename);
                        }
                        catch { }
                        break;
                }
            }
        }

        private static void SaveImages(ExportImageForm edit, SehensControl scope, string filename)
        {
            string contents = scope.ScreenshotToRtf(edit.Skin);
            File.WriteAllText(filename, contents);
        }

        private static void SaveImage(ExportImageForm edit, SehensControl scope, string filename)
        {
            scope.ScreenshotToBitmap(edit.Skin).Save(filename, ImageFormat.Png);
        }

        private static void SaveCSV(ExportDataForm edit, Traces waveforms, string separator = ",")
        {
            CSVSave.SaveCols(edit.Filename, waveforms.Names, waveforms.Extracted, separator);
        }

        private static void SaveWAV(ExportDataForm edit, Traces waveforms)
        {
            double[] samples = new double[waveforms.Extracted.Count];
            bool stop = false;

            int index = 0;
            RiffWriter riffWriter = new RiffWriter(edit.Filename, (int)waveforms.SamplesPerSeconds[0], waveforms.Extracted.Count);
            while (!stop)
            {
                stop = true;
                for (int channel = 0; channel < waveforms.Extracted.Count; channel++)
                {
                    double[] array = (double[])waveforms.Extracted[channel];
                    samples[channel] = (array.Length > index) ? array[index] : 0.0;
                    if (array.Length > index)
                    {
                        stop = false;
                    }
                }
                index++;
                riffWriter.Add(samples);
            }
            riffWriter.Close();
        }

        private static void SaveSpaceSeparated(ScopeContextMenu.DropDownArgs a, ExportDataForm edit, Traces waveforms)
        {
            List<object> extracted = waveforms.Extracted;
            extracted.Insert(0, waveforms.Names);
            CSVSave.SaveRows(edit.Filename, null, extracted, " ");
        }

        private static void SaveNames(string filename, ScopeContextMenu.DropDownArgs a)
        {
            File.WriteAllText(filename, string.Join("\r\n", a.Views.Select((TraceView x) => x.DecoratedName)));
        }

        private static T? ValueFromDescription<T>(string description)
        {
            return Enum.GetValues(typeof(T)).Cast<T>().Where(x => GetDescription(x) == description).FirstOrDefault();
        }


        private static void Import(SehensControl display, ImportType type, string[] fileNames, string csvHeaderLinePrefix = "", string targetTrace = "", ImportDataForm.Source dataSource = ImportDataForm.Source.File)
        {
            using AutoEditorForm autoEditorForm = new AutoEditorForm();
            ImportDataForm? importData = (ImportDataForm?)ImportExportEdit(type, autoEditorForm);
            bool flag = false;
            if (importData != null)
            {
                importData.Filenames = fileNames;
                importData.FileType = type;
                importData.DataSource = dataSource;
                if (importData is ImportDataVerticalCsvForm)
                {
                    ((ImportDataVerticalCsvForm)importData).HeaderLinePrefix = csvHeaderLinePrefix;
                }
                if (importData is ImportDataSaleaeAnalysisForm)
                {
                    ((ImportDataSaleaeAnalysisForm)importData).TargetTrace = targetTrace;
                }
                if (importData is ImportDataFeaturesForm)
                {
                    ((ImportDataFeaturesForm)importData).TargetTrace = targetTrace;
                }
                flag = !autoEditorForm.ShowDialog("Import " + GetExtension(type) + "file", GetDescription(type), importData);
            }
            if (flag)
            {
                return;
            }
            switch (type)
            {
                case ImportType.CsvVertical:
                case ImportType.CsvGzVertical:
                case ImportType.TsvVertical:
                    if (importData != null)
                    {
                        LoadWaveformsCSV(display, (ImportDataVerticalCsvForm)importData, type);
                    }
                    break;
                case ImportType.Wav:
                    if (importData != null)
                    {
                        LoadWaveformsWAV(display, (ImportDataWavForm)importData);
                    }
                    break;
                case ImportType.Binary8bit:
                    if (importData != null)
                    {
                        LoadWaveformsBinaryBits(display, (ImportDataBinaryForm)importData);
                    }
                    break;
                case ImportType.SaleaeAnalysis:
                    if (importData != null)
                    {
                        LoadWaveformSaleaeAnalysis(display, (ImportDataSaleaeAnalysisForm)importData);
                    }
                    break;
                case ImportType.SehensBinaryState:
                    {
                        string[] array = fileNames;
                        for (int i = 0; i < array.Length; i++)
                        {
                            LoadState(array[i], display);
                        }
                        break;
                    }
                case ImportType.SehensXMLState:
                    {
                        string[] array = fileNames;
                        for (int i = 0; i < array.Length; i++)
                        {
                            LoadStateXml(array[i], display);
                        }
                        break;
                    }
                case ImportType.Features:
                    if (importData != null)
                    {
                        ImportFeaturesToTimeTrace(display, (ImportDataFeaturesForm)importData);
                    }
                    break;
            }
        }

        private static object? ImportExportEdit<T>(T value, AutoEditorForm form)
        {
            object[] customAttributes = typeof(T).GetField(value?.ToString() ?? "")?.GetCustomAttributes(typeof(EditFormTypeAttribute), inherit: false) ?? new object[0];
            Type? type = customAttributes.Length == 0 ? null : ((EditFormTypeAttribute)customAttributes[0]).Form;
            object? obj = null;
            if (type != null)
            {
                if (ImportExportForms.ContainsKey(value))
                {
                    obj = ImportExportForms[value];
                }
                else
                {
                    obj = type == null ? null : Activator.CreateInstance(type);
                    if (obj != null)
                    {
                        ImportExportForms.Add(value, obj);
                    }
                }
            }
            return obj;
        }

        public static void LoadWaveformsCSV(string filename, SehensControl scope, double samplesPerSecond)
        {
            LoadWaveformsCSV(scope, new ImportDataVerticalCsvForm() { FileType = ImportType.CsvVertical, Filenames = new string[] { filename }, TraceNamePrefix = "CSV.", SamplesPerSecond = samplesPerSecond });
        }

        public static void LoadWaveformsCSV(string filename, string tracePrefix, SehensControl scope, double samplesPerSecond)
        {
            LoadWaveformsCSV(scope, new ImportDataVerticalCsvForm() { FileType = ImportType.CsvVertical, Filenames = new string[] { filename }, TraceNamePrefix = tracePrefix, SamplesPerSecond = samplesPerSecond });
        }

        public static void LoadWaveformsCSV(string filename, SehensControl scope, double samplesPerSecond, string headerLinePrefix)
        {
            LoadWaveformsCSV(scope, new ImportDataVerticalCsvForm() { FileType = ImportType.CsvVertical, Filenames = new string[] { filename }, TraceNamePrefix = "CSV.", SamplesPerSecond = samplesPerSecond, HeaderLinePrefix = headerLinePrefix });
        }

        public static void LoadWaveformsCSV(string filename, string tracePrefix, SehensControl scope, double samplesPerSecond, string headerLinePrefix)
        {
            LoadWaveformsCSV(scope, new ImportDataVerticalCsvForm() { FileType = ImportType.CsvVertical, Filenames = new string[1] { filename }, TraceNamePrefix = tracePrefix + ".", SamplesPerSecond = samplesPerSecond, HeaderLinePrefix = headerLinePrefix });
        }

        private static void LoadWaveformsCSV(SehensControl scope, ImportDataVerticalCsvForm edit, ImportType type = ImportType.CsvVertical)
        {
            SehensControl scope2 = scope;
            ImportDataVerticalCsvForm edit2 = edit;
            if (edit2.DataSource == ImportDataForm.Source.File)
            {
                new Thread((ThreadStart)delegate
                {
                    scope2.IncrementBackgroundThreadCount();
                    LoadWaveformsCSVInner(scope2, edit2, type);
                    scope2.DecrementBackgroundThreadCount();
                }).Start();
            }
            else
            {
                LoadWaveformsCSVInner(scope2, edit2, type);
            }
        }

        private static void LoadWaveformsCSVInner(SehensControl scope, ImportDataVerticalCsvForm edit, ImportType type)
        {
            char separator = type switch
            {
                ImportType.TsvVertical => '\t',
                ImportType.CsvVertical => ',',
                ImportType.CsvGzVertical => ',',
                _ => ','
            };

            try
            {
                Regex? regex = (edit.ColumnMatchRegex.Length > 0) ? new Regex(edit.ColumnMatchRegex, RegexOptions.IgnoreCase) : null;
                string[] fileNames = edit.Filenames;
                foreach (string path in fileNames)
                {
                    FileStream? fileStream = null;
                    GZipStream? gZipStream = null;
                    StreamReader? streamReader = null;
                    try
                    {
                        fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (type == ImportType.CsvGzVertical)
                        {
                            gZipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                            streamReader = new StreamReader(gZipStream, Encoding.UTF8);
                        }
                        else
                        {
                            streamReader = new StreamReader(fileStream, Encoding.UTF8);
                        }
                        var csv = new CSVLoad<double>(file: streamReader,
                            parse: (s) =>
                            {
                                double result = double.NaN;
                                return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : double.NaN;
                            },
                            defaultValue: 0.0,
                            separator: separator,
                            headerRowPrefix: edit.HeaderLinePrefix);

                        for (int col = 0; col < csv.ColCount; col++)
                        {
                            string text = edit.TraceNamePrefix
                                + (edit.AppendFilenamePrefix ? (Path.GetFileNameWithoutExtension(path) + ".") : "")
                                + csv.ColumnHeadings[col]
                                + (edit.AddIndex ? ("." + (col + 1)) : "")
                                + edit.TraceNameSuffix;
                            if (regex == null || regex.IsMatch(text))
                            {
                                List<double> list = new List<double>(csv.Column(col));
                                if (edit.RemoveNAN)
                                {
                                    list.RemoveAll((x) => x.Equals(double.NaN));
                                }
                                var trace = scope[text].UpdateByRef(list.ToArray(), edit.SamplesPerSecond);
                                scope.AddTrace(trace);
                                edit.ApplyTo(trace);
                            }
                        }
                    }
                    finally
                    {
                        streamReader?.Close();
                        streamReader?.Dispose();
                        gZipStream?.Close();
                        gZipStream?.Dispose();
                        fileStream?.Close();
                        fileStream?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error {ex.Message} loading CSV");
            }
        }

        private static void LoadWaveformsWAV(SehensControl scope, ImportDataWavForm edit)
        {
            foreach (string text in edit.Filenames)
            {
                RiffReader reader = new RiffReader(text);
                for (int channel = 0; channel < reader.ChannelCount; channel++)
                {
                    string key = edit.TraceNamePrefix
                        + (edit.AppendFilenamePrefix ? (Path.GetFileNameWithoutExtension(text) + ".") : "")
                        + (channel + 1)
                        + edit.TraceNameSuffix;
                    var trace = scope[key].UpdateByRef(reader.Channel(channel), reader.SamplesPerSecond);
                    scope.AddTrace(trace);
                    edit.ApplyTo(trace);
                }
            }
        }

        private static void LoadWaveformsBinaryBits(SehensControl scope, ImportDataBinaryForm edit)
        {
            string[] fileNames = edit.Filenames;
            foreach (string path in fileNames)
            {
                double[][] array = Extract8BitsPerByte(File.ReadAllBytes(path), edit.DecimationFactor);
                for (int bit = 0; bit < 8; bit++)
                {
                    string key = edit.TraceNamePrefix
                        + (edit.AppendFilenamePrefix ? (Path.GetFileNameWithoutExtension(path) + ".") : "")
                        + (bit + 1)
                        + edit.TraceNameSuffix;
                    TraceData trace = scope[key].UpdateByRef(array[bit], edit.SamplesPerSecond / (double)edit.DecimationFactor);
                    scope.AddTrace(trace);
                    edit.ApplyTo(trace);
                }
            }
        }

        private static double[][] Extract8BitsPerByte(byte[] logicBinary, int decimateBy)
        {
            double[][] result = new double[8][];
            for (int bit = 0; bit < 8; bit++)
            {
                result[bit] = new double[logicBinary.Length / decimateBy];
            }
            int index = 0;
            int decimate = 0;
            foreach (byte b in logicBinary)
            {
                decimate = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    result[bit][index] += b & (1 << bit);
                }
                if (decimate == decimateBy)
                {
                    index++;
                }
                decimate++;
            }
            return result;
        }

        private static void LoadWaveformSaleaeAnalysis(SehensControl display, ImportDataSaleaeAnalysisForm edit)
        {
            foreach (var filename in edit.Filenames)
            {
                List<TraceFeature> list = new List<TraceFeature>();
                bool first = true;
                foreach (string text in File.ReadAllLines(filename))
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        string[] split = text.Split(',');
                        double result;
                        if (double.TryParse(split[0], NumberStyles.Any, CultureInfo.InvariantCulture, out result) && split.Length >= 2)
                        {
                            list.Add(new TraceFeature
                            {
                                SampleNumber = (int)(result * edit.SamplesPerSecond),
                                Text = split[1]
                            });
                        }
                    }
                }

                var traceData = display[edit.TargetTrace];
                traceData.InputFeatures = list.ToArray();
                edit.ApplyTo(traceData);
            }
        }

        private static void ImportFeaturesToTimeTrace(SehensControl display, ImportDataFeaturesForm edit)
        {
            foreach (string filename in edit.Filenames)
            {
                var traceView = display.TraceByName(edit.TargetTrace);
                if (traceView != null)
                {
                    var samples = traceView.Samples.InputSamplesAsDouble;
                    if (filename.Length != 0 && File.Exists(filename))
                    {
                        int index(double value)
                        {
                            int length = samples.Length;
                            for (int loop = 0; loop < length; loop++)
                            {
                                if (samples[loop] >= value)
                                {
                                    return loop;
                                }
                            }
                            return -1;
                        }

                        string[] lines = File.ReadAllLines(filename);
                        foreach (var line in lines)
                        {
                            string[] split = line.Split(new char[] { ' ' }, 2);
                            if (split.Length == 2)
                            {
                                double result = 0.0;
                                if (double.TryParse(split[0], NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                                {
                                    traceView.Samples.AddFeature(index(result), split[1]);
                                }
                            }
                        }
                    }
                    display.ShowTraceFeatures = true;
                    edit.ApplyTo(traceView.Samples);
                }
                else
                {
                    MessageBox.Show("Can't find " + edit.TargetTrace);
                }
            }
        }

        ////////////////////////////////////////////
        // save and load state files

        public static void SaveState(string filename, SehensControl scope)
        {
            //fixme: grobuf
        }

        public static void SaveStateXml(string filename, SehensControl scope)
        {
            //fixme - save as xml other than samples
        }

        public static bool LoadState(string filename, SehensControl scope)
        {
            //fixme - use grobuf
            return false;
        }

        public static void LoadStateXml(string filename, SehensControl scope)
        {
            //fixme - load xml other than samples
        }

        ////////////////////////////////////////////
        // public save helpers

        public static void SaveWav(SehensControl scope, string filename, string[] viewNames)
        {
            var a = new ScopeContextMenu.DropDownArgs()
            {
                Views = new List<TraceView>(),
                Scope = scope
            };

            foreach (string view in viewNames)
            {
                TraceView? first = scope[view].FirstView;
                if (first != null && first.Samples.InputSampleCount > 0)
                {
                    a.Views.Add(first);
                }
            }

            if (a.Views.Count > 0)
            {
                ExportDataForm exportData = new ExportDataForm() { Filename = filename };
                exportData.ExportScope = ExportDataForm.Scope.SelectedDisplayedSamples;
                Export(a, ExportType.Wav, ExportDataForm.Destination.File, filename, exportData);
            }
        }

        public static void SaveStateDialog(SehensControl scope)
        {
            var a = new ScopeContextMenu.DropDownArgs() { Scope = scope };
            ExportDialog(scope, a, ExportType.SehensBinary);
        }

        public static void LoadWaveformsUsingExtension(SehensControl display, string[] filenames, string csvHeaderLinePrefix = "", string targetTrace = "")
        {
            IEnumerable<ImportType> types = Enum.GetValues(typeof(ImportType)).Cast<ImportType>();
            var files = filenames.Select((filename) => new
            {
                fileType = types.Where((ImportType y) => filename.EndsWith(GetExtension(y))).FirstOrDefault(),
                filename = filename
            });

            foreach (var file in files
                .Select(a => a.fileType)
                .Distinct()
                .ToDictionary(
                        key => key,
                        value => files.Where(pair => pair.fileType == value).Select(pair => pair.filename)))
            {
                Import(display, file.Key, file.Value.ToArray(), csvHeaderLinePrefix, targetTrace);
            }
        }

        public static void LoadStateDialog(SehensControl scope)
        {
            ImportDialog(scope, ImportType.SehensBinaryState);
        }

        public static void LoadWaveformsCSVDialog(SehensControl scope)
        {
            ImportDialog(scope, ImportType.CsvVertical);
        }

        public static void LoadWaveformsCSVDialog(SehensControl scope, string headerLinePrefix)
        {
            ImportDialog(scope, ImportType.CsvVertical, headerLinePrefix);
        }

        public static void LoadWaveformsWAVDialog(SehensControl scope)
        {
            ImportDialog(scope, ImportType.Wav);
        }

        public static void LoadWaveformsWAV(string filename, SehensControl scope)
        {
            LoadWaveformsWAV(scope, new ImportDataWavForm() { FileType = ImportType.Wav, Filenames = new string[] { Path.GetFileName(filename) } });
        }

        public static void LoadWaveformsWAV(string filename, string tracePrefix, SehensControl scope)
        {
            LoadWaveformsWAV(scope, new ImportDataWavForm() { FileType = ImportType.Wav, Filenames = new string[] { filename }, TraceNamePrefix = tracePrefix });
        }

        public static void LoadWaveformSaleaeAnalysis(string filename, SehensControl display, string trace, double samplesPerSecond)
        {
            LoadWaveformSaleaeAnalysis(display, new ImportDataSaleaeAnalysisForm()
            {
                Filenames = new string[] { filename },
                TargetTrace = trace,
                SamplesPerSecond = samplesPerSecond
            });
        }
    }
}
