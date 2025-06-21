using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Controls
{

    /// <summary>
    /// AutoEditor provides a dynamic reflection-based UI binding framework for WinForms,
    /// enabling form controls to be automatically linked to properties and fields of a data source object.
    /// 
    /// Features:
    /// - Automatically maps data source members to controls via reflection.
    /// - Supports a rich set of UI interaction features (e.g., ComboBoxes, Buttons, Panels, etc.).
    /// - Allows extensible customization through attributes:
    ///   - <see cref="ValuesAttribute"/> for value lists, e.g. user defined combo boxes,
    ///   - <see cref="DisplayNameAttribute"/>, control order of field display,
    ///   - <see cref="SubEditorAttribute"/> for nested edit forms,
    ///   - <see cref="PushButtonAttribute"/> for action binding,
    ///   - <see cref="HiddenAttribute"/> and <see cref="DisabledAttribute"/> for visibility/enabling.
    /// - Automatically propagates UI changes to the data source and vice versa.
    /// - Provides optional callbacks <see cref="OnChanging"/> and <see cref="OnChanged"/> for data change notification.
    /// 
    /// Intended Use:
    /// - Create an instance of AutoEditor by providing a data object and a control collection, e.g. a form or a panel's controls).
    /// - Define your data class with appropriate attributes to indicate UI behavior and layout.
    /// - AutoEditor will walk through the control hierarchy, attach event handlers, and keep controls and data in sync.
    /// 
    /// Design Notes:
    /// - Internally maintains recursion guards
    /// 
    /// Related:
    /// - <see cref="AutoEditorBase"/>: A base class for data models that integrates with AutoEditor:
    ///   - <c>UpdateControls</c> action for forcing UI refreshes
    ///   - <c>OnChanged</c> callback for post-edit hooks
    ///   - <c>Updating</c> flag for guarding re-entrant updates
    ///   
    /// Example:
    /// <code>
    /// class MyData {
    ///     [AutoEditor.DisplayName("Label")] 
    ///     public string Name { get; set; }
    /// }
    /// 
    /// var editor = new AutoEditor(myDataInstance, this.Controls);
    /// [or]
    /// AutoEditorControl AutoEdit;
    /// [add control to form]
    /// AutoEdit.Generate(myDataInstance);
    /// </code>
    /// </summary>

    public class AutoEditor
    {
        public object SourceData;
        public Control.ControlCollection? Controls;
        public Action<AutoEditor>? OnChanging;
        public Action<AutoEditor>? OnChanged;
        public AutoEditor? Parent;
        private bool m_Updating;
        private static int m_UpdateRecursion;

        internal class EditRow
        {
            public MemberInfo MemberInfo;
            public string? DisplayText;
            public int? ObjectIndex;
            public double DisplayOrder;
            public string? GroupName;
            public Type Type;
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class ValuesAttribute : Attribute
        {
            private Type? m_Type;
            public string[]? m_Values;

            public string[] Values
            {
                get
                {
                    if (m_Values != null)
                    {
                        return m_Values;
                    }
                    else if (m_Type == null)
                    {
                        return new string[0];
                    }
                    else if (m_Type.IsEnum)
                    {
                        return m_Values = Enum.GetNames(m_Type);
                    }
                    else
                    {
                        var obj = Activator.CreateInstance(m_Type);
                        string name = nameof(ValuesAttributeInterface.GetValues);
                        var array = (m_Type?.GetMethod(name))?.Invoke(obj, null);
                        return array == null ? new string[0] : ((IEnumerable<string>)array).ToArray();
                    }
                }
            }

            public ValuesAttribute() : this(new string[0]) { }
            public ValuesAttribute(string[] values)
            {
                m_Values = values;
            }
            public ValuesAttribute(Type type)
            {
                m_Type = type;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class DisplayNameAttribute : Attribute
        {
            public string DisplayName;
            public DisplayNameAttribute() : this("") { }
            public DisplayNameAttribute(string displayName) { DisplayName = displayName; }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class DisplayOrderAttribute : Attribute
        {
            public double DisplayOrder;
            public string? GroupName; // groups on (int)DisplayOrder so items can be reordered within group

            public DisplayOrderAttribute() : this(0) { }
            public DisplayOrderAttribute(double displayOrder, string? groupName = null)
            {
                DisplayOrder = displayOrder;
                GroupName = groupName;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class SubEditorAttribute : Attribute
        {
            public bool CloseOnClick;
            public SubEditorAttribute(bool closeOnClick = false) { CloseOnClick = closeOnClick; }
        }

        public class ValueListEntry // for List<ValueArrayEntry>
        {
            public string Name = "";
            public string Value = "";
            public int Order;
            public object? Tag;
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class HiddenAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class DisabledAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class PushButtonAttribute : Attribute
        {
            public string Caption;
            public PushButtonAttribute(string caption) { Caption = caption; }
        }

        public interface ValuesAttributeInterface
        {
            public IEnumerable<string> GetValues();
        }

        public AutoEditor(object source, Control.ControlCollection? controls)
        {
            Controls = controls;
            if (source is AutoEditor)
            {
                SourceData = ((AutoEditor)source).SourceData;
                Parent = (AutoEditor)source;
            }
            else
            {
                SourceData = source;
            }
            WalkControls();
        }

        internal static string[]? Values(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(ValuesAttribute), inherit: false);
            return ((customAttributes != null) && (customAttributes.Length != 0)) ? ((customAttributes[0] as ValuesAttribute)?.Values) : null;
        }

        internal static string DisplayName(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(DisplayNameAttribute), inherit: false);
            if (customAttributes.Length != 0 && customAttributes[0] is DisplayNameAttribute attr && !string.IsNullOrWhiteSpace(attr.DisplayName))
            {
                return attr.DisplayName;
            }
            else
            {
                return PrettyName(info.Name);
            }
        }

        internal static string PrettyName(string rawName)
        {
            string name = rawName.StartsWith("m_") ? rawName.Substring(2) : rawName;
            string padded = "  " + name + " ";
            var sb = new System.Text.StringBuilder();
            for (int loop = 0; loop < name.Length; loop++)
            {
                char prev2 = padded[loop + 0];
                char prev = padded[loop + 1];
                char curr = padded[loop + 2];
                char next = padded[loop + 3];
                bool split =
                    // lowercase -> uppercase, but not after digit (3dB)
                    (char.IsLower(prev) && char.IsUpper(curr) && !char.IsDigit(prev2)) ||
                    // acronym -> Word, but not after digit (2MHz)
                    (char.IsUpper(prev) && char.IsUpper(curr) && char.IsLower(next) && !char.IsDigit(prev2)) ||
                    // letter -> digit
                    (char.IsLetter(prev) && char.IsDigit(curr));
                sb.Append(split ? " " : "");
                sb.Append(curr);
            }
            return sb.ToString();
        }

        internal static (double order, string? name) DisplayOrder(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(DisplayOrderAttribute), inherit: false);
            if ((customAttributes.Length == 0))
            {
                return (0, null);
            }
            else
            {
                DisplayOrderAttribute? att = customAttributes[0] as DisplayOrderAttribute;
                return (att?.DisplayOrder ?? 0, att?.GroupName);
            }
        }

        internal static bool IsHidden(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(HiddenAttribute), inherit: false).Length != 0;
        }

        internal static bool IsSubEditor(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(SubEditorAttribute), inherit: false).Length != 0;
        }

        internal static bool IsPushButton(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(PushButtonAttribute), inherit: false).Length != 0;
        }

        internal static string PushButtonCaption(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(PushButtonAttribute), inherit: false);
            return ((customAttributes == null) || (customAttributes.Length == 0)) ? "" : ((customAttributes[0] as PushButtonAttribute)?.Caption ?? "");
        }

        internal static bool IsEnabled(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(DisabledAttribute), inherit: false).Length == 0;
        }

        private void WalkControls()
        {
            if (Controls == null || SourceData == null) return;

            SetupControls();
            UpdateControls();
            SetEvents();

            if (SourceData != null && SourceData is AutoEditorBase)
            {
                AutoEditorBase obj = (AutoEditorBase)SourceData;
                obj.UpdateControls -= UpdateControls;
                obj.UpdateControls += UpdateControls;
            }
        }

        private void SetupControls()
        {
            if (Controls == null) return;
            foreach (Control item in Controls)
            {
                if (item is ComboBox)
                {
                    ((ComboBox)item).SelectedIndexChanged += TextChanged;
                    ((ComboBox)item).TextChanged += TextChanged;
                    string[]? array = EnumValues(SourceData, item.Tag as EditRow);
                    if (array != null)
                    {
                        ((ComboBox)item).Items.Clear();
                        ComboBox.ObjectCollection items = ((ComboBox)item).Items;
                        items.AddRange((object[])array);
                        ((ComboBox)item).DropDownWidth = MaxItemWidth((ComboBox)item);
                    }
                }
                else if (item is ListBox)
                {
                    ((ListBox)item).SelectedIndexChanged += TextChanged;
                    ((ListBox)item).TextChanged += TextChanged;
                    string[]? array = EnumValues(SourceData, item.Tag as EditRow);
                    if (array != null)
                    {
                        ((ListBox)item).Items.Clear();
                        ((ListBox)item).Items.AddRange(array);
                    }
                }

                if (item.Controls.Count > 0) // sub controls
                {
                    new AutoEditor(this, item.Controls);
                }
            }
        }
        private void SetEvents()
        {
            if (Controls == null) return;
            foreach (Control item in Controls)
            {
                if (item is Button)
                {
                    ((Button)item).Click += MouseClicked;
                }
                else if (item is CheckBox)
                {
                    ((CheckBox)item).CheckedChanged += TextChanged;
                }
                else if (item is ComboBox)
                {
                    ((ComboBox)item).SelectedIndexChanged += TextChanged;
                    ((ComboBox)item).TextChanged += TextChanged;
                }
                else if (item is ListBox)
                {
                    ((ListBox)item).SelectedIndexChanged += TextChanged;
                    ((ListBox)item).TextChanged += TextChanged;
                }
                else if (item is Panel)
                {
                    ((Panel)item).MouseClick += MouseClicked;
                }
                else if (item is RadioButton)
                {
                    ((RadioButton)item).CheckedChanged += TextChanged;
                }
                else
                {
                    item.TextChanged += TextChanged;
                }
            }
        }


        internal void RemoveDelegates()
        {
            if (SourceData != null && SourceData is AutoEditorBase)
            {
                AutoEditorBase obj = (AutoEditorBase)SourceData;
                obj.UpdateControls -= UpdateControls;
            }
        }

        public void UpdateControls()
        {
            if (SourceData != null && Controls != null && !m_Updating && m_UpdateRecursion == 0)
            {
                m_Updating = true;
                if (SourceData is AutoEditorBase)
                {
                    ((AutoEditorBase)SourceData).Updating = true;
                }
                if (Controls.Count > 0)
                {
                    Controls[0].BeginInvokeIfRequired(() => UpdateControls(Controls, SourceData));
                }
                if (SourceData is AutoEditorBase)
                {
                    ((AutoEditorBase)SourceData).Updating = false;
                }
                m_Updating = false;
            }
        }

        public static void UpdateControls(Control.ControlCollection controls, object sourceData)
        {
            m_UpdateRecursion++;
            foreach (Control control in controls)
            {

                if (control is CheckBox)
                {
                    CheckBox checkBox = (CheckBox)control;
                    object? value = GetValue(sourceData, checkBox.Tag as EditRow);
                    if (value != null && checkBox.Checked != (bool)value)
                    {
                        checkBox.Checked = (bool)value;
                    }
                }
                else if (control is ComboBox)
                {
                    ComboBox comboBox = (ComboBox)control;
                    object? value = GetValue(sourceData, comboBox.Tag as EditRow);
                    if (value != null && (string)comboBox.SelectedText != value.ToString())
                    {
                        comboBox.SelectedIndex = comboBox.Items.IndexOf(value.ToString());
                    }
                }
                else if (control is RadioButton)
                {
                    RadioButton radioButton = (RadioButton)control;
                    object? value = GetValue(sourceData, radioButton.Tag as EditRow);
                    if (value != null && radioButton.Checked != (bool)value)
                    {
                        radioButton.Checked = (bool)value;
                    }
                }
                else if (control is Panel)
                {
                    Panel panel = (Panel)control;
                    object? value = GetValue(sourceData, panel.Tag as EditRow);
                    if (value != null && value is Color)
                    {
                        panel.BackColor = (Color)value;
                    }
                }
                else if (!(control is Button))
                {
                    object? value = GetValue(sourceData, control.Tag as EditRow);
                    if (value != null && control.Text != value.ToString())
                    {
                        control.Text = value.ToString();
                    }
                }
                if (control.Controls.Count > 0)
                {
                    UpdateControls(control.Controls, sourceData);
                }
            }
            m_UpdateRecursion--;
        }

        private void MouseClicked(object? sender, EventArgs e)
        {
            if (SourceData == null || !(sender is Control)) return;
            Control control = (Control)sender;
            object? value = GetValue(SourceData, control.Tag as EditRow);
            if (value is Color && control is Panel)
            {
                ColorDialog colorDialog = new ColorDialog();
                colorDialog.Color = (Color)value;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    SetValue((EditRow)control.Tag, colorDialog.Color);
                    control.BackColor = colorDialog.Color;
                }
            }
            else if (control is Button && value != null)
            {
                object[]? array = ((EditRow)control.Tag)?.MemberInfo
                    .GetCustomAttributes(typeof(AutoEditor.SubEditorAttribute), inherit: false);
                if (array != null && array.Length != 0)
                {
                    // sub editor
                    AutoEditorForm? autoEditorForm = ParentForm(control) as AutoEditorForm;
                    bool closeOnClick = ((array[0] as AutoEditor.SubEditorAttribute)?.CloseOnClick ?? false) && autoEditorForm != null;
                    if (closeOnClick && autoEditorForm != null)
                    {
                        autoEditorForm.Visible = false;
                    }
                    new AutoEditorForm().ShowDialog(control.Text, ((EditRow)control.Tag).DisplayText, value);
                    if (closeOnClick)
                    {
                        autoEditorForm?.ButtonOK_Click(autoEditorForm, new EventArgs());
                    }
                }
                else if (value.GetType().IsSubclassOf(typeof(Delegate)))
                {
                    ((Delegate)value).DynamicInvoke();
                }
                else if (value.GetType() == typeof(bool))
                {
                    SetValue(control.Tag as EditRow, true);
                    UpdateControls();
                }
            }
        }

        private Form? ParentForm(Control control)
        {
            return (control == null) ? null
                   : (control is Form) ? (Form)control
                   : ParentForm(control.Parent);
        }

        private void TextChanged(object? sender, EventArgs e)
        {
            if (sender is Control)
            {
                if (sender is CheckBox)
                {
                    SetValue(((Control)sender).Tag as EditRow, ((CheckBox)sender).Checked);
                }
                else if (sender is RadioButton)
                {
                    SetValue(((Control)sender).Tag as EditRow, ((RadioButton)sender).Checked);
                }
                else
                {
                    SetValue(((Control)sender).Tag as EditRow, ((Control)sender).Text);
                }
            }
        }

        private static object? GetValue(object sourceData, EditRow? row)
        {
            if (row == null) return null;
            object? obj = null;
            if (row.ObjectIndex != null)
            {
                var va = ((FieldInfo)row.MemberInfo).GetValue(sourceData) as List<ValueListEntry>;
                return va == null ? null : va[row.ObjectIndex.Value].Value;
            }
            else if (row.MemberInfo is FieldInfo)
            {
                obj = ((FieldInfo)row.MemberInfo).GetValue(sourceData);
            }
            else if (row.MemberInfo is PropertyInfo)
            {
                obj = ((PropertyInfo)row.MemberInfo).GetValue(sourceData, null);
            }
            return obj;
        }

        internal static void SetValueList(object obj, Dictionary<AutoEditor.EditRow, object> valuesToSet)
        {
            AutoEditor autoEditor = new AutoEditor(obj, null);
            foreach (var item in valuesToSet)
            {
                autoEditor.SetValue(item.Key, item.Value);
            }
        }

        private void SetValue(EditRow? row, object value)
        {
            if (row == null) return;
            if (SourceData == null) return;
            object? obj = null;
            if (row.ObjectIndex != null)
            {
                var va = ((FieldInfo)row.MemberInfo).GetValue(SourceData) as List<ValueListEntry>;
                if (va != null)
                {
                    var entry = va[row.ObjectIndex.Value];
                    entry.Value = (string)value;
                }
            }
            else
            if (row.MemberInfo is FieldInfo)
            {
                FieldInfo fieldInfo = (FieldInfo)row.MemberInfo;
                obj = ParseTo(fieldInfo.FieldType, value);
                if (obj != null && !fieldInfo.IsLiteral && !fieldInfo.IsInitOnly)
                {
                    InvokeOnChanging();
                    fieldInfo.SetValue(SourceData, obj);
                    InvokeOnChanged();
                }
            }
            else if (row.MemberInfo is PropertyInfo)
            {
                PropertyInfo propertyInfo = (PropertyInfo)row.MemberInfo;
                obj = ParseTo(propertyInfo.PropertyType, value);
                if (obj != null)
                {
                    InvokeOnChanging();
                    try
                    {
                        propertyInfo.SetValue(SourceData, obj, null);
                    }
                    catch (ArgumentException)
                    {
                    }
                    InvokeOnChanged();
                }
            }
        }

        private void InvokeOnChanged()
        {
            if (m_UpdateRecursion == 0)
            {
                if (Parent != null)
                {
                    Parent.InvokeOnChanged();
                }
                else
                {
                    GetEventHandler("OnChanged")?.Invoke(SourceData, new EventArgs());
                    OnChanged?.Invoke(this);
                }
            }
        }

        private EventHandler? GetEventHandler(string memberName)
        {
            if (SourceData == null) return null;
            EventHandler? result = null;
            MemberInfo[] member = SourceData.GetType().GetMember(memberName);
            if (member != null && member.Length != 0 && member[0] != null)
            {
                if (member[0] is FieldInfo)
                {
                    FieldInfo fieldInfo = (FieldInfo)member[0];
                    object? o = fieldInfo.GetValue(SourceData);
                    if (o is EventHandler)
                    {
                        result = o as EventHandler;
                    }
                    else if (o is Action)
                    {
                        result = delegate
                        {
                            (o as Action)?.Invoke();
                        };
                    }
                }
                else if (member[0] is MethodInfo)
                {
                    MethodInfo o = (MethodInfo)member[0];
                    result = delegate (object? a, EventArgs b)
                    {
                        o.Invoke(SourceData, new object?[] { a, b });
                    };
                }
            }
            return result;
        }

        private void InvokeOnChanging()
        {
            if (m_UpdateRecursion == 0)
            {
                if (Parent != null)
                {
                    Parent.InvokeOnChanging();
                }
                else
                {
                    GetEventHandler("OnChanging")?.Invoke(SourceData, new EventArgs());
                    OnChanging?.Invoke(this);
                }
            }
        }

        private static object? ParseTo(Type type, object newValue)
        {
            try
            {
                if (type == typeof(bool) && newValue is bool)
                {
                    return newValue;
                }
                if (type.IsEnum && newValue is string)
                {
                    return ((string)newValue).Length > 0 ? Enum.Parse(type, (string)newValue) : null;
                }
                if (type == typeof(string) && newValue is string)
                {
                    return newValue;
                }
                if (newValue is string
                    && (newValue as string == "" || newValue as string == "-")
                    && typeof(double).IsAssignableFrom(type))
                {
                    return 0;
                }
                if ((type.IsPrimitive || type.IsEnum) && type == newValue.GetType())
                {
                    return newValue;
                }
                if (type == typeof(Color) && newValue is Color)
                {
                    return newValue;
                }
                MemberInfo[] member = type.GetMember("Parse");
                if (member != null && member.Length != 0 && member[0] is MethodInfo)
                {
                    MethodInfo methodInfo = (MethodInfo)member[0];
                    object[] parameters = { newValue };
                    return methodInfo.Invoke(null, parameters);
                }
            }
            catch { }
            return null;
        }

        internal static Dictionary<EditRow, object> GetValueList(object obj, List<EditRow> namesToExtract)
        {
            var dictionary = new Dictionary<EditRow, object>();
            foreach (var item in namesToExtract)
            {
                object? value = GetValue(obj, item);
                if (value != null)
                {
                    dictionary.Add(item, value);
                }
            }
            return dictionary;
        }

        private static string[]? EnumValues(object sourceData, EditRow? row)
        {
            if (row == null) return new string[0];
            string[]? result = null;
            object? value = GetValue(sourceData, row);
            if (value is Enum)
            {
                result = Enum.GetNames(row.Type);
            }
            return result;
        }

        private int MaxItemWidth(ComboBox combobox)
        {
            int num = combobox.DropDownWidth;
            foreach (object item in combobox.Items)
            {
                num = Math.Max(num, TextRenderer.MeasureText(item.ToString(), combobox.Font).Width);
            }
            return num;
        }
    }


    [TestClass]
    public class AutoEditorTest
    {
        class TestClass : AutoEditorBase
        {
            [AutoEditor.Values(new string[] { "a", "b", "c" })]
            public string TestString { get; set; } = "a";
            [AutoEditor.DisplayName("integer")]
            [AutoEditor.DisplayOrder(1)]
            public int TestInt { get; set; } = 42;

            [AutoEditor.SubEditor]
            public List<AutoEditor.ValueListEntry> TestList { get; set; } = new List<AutoEditor.ValueListEntry>
            {
                new AutoEditor.ValueListEntry { Name = "i1", Value = "v1", Order = 1 },
                new AutoEditor.ValueListEntry { Name = "i2", Value = "v2", Order = 2 }
            };

            [AutoEditor.Hidden]
            public string Hidden { get; set; } = "Hidden";
        }

        class TestValues : AutoEditor.ValuesAttributeInterface
        {
            public IEnumerable<string> GetValues()
            {
                return new string[] { "a", "b", "c" };
            }
        }


        [TestMethod]
        public void TestAutoEditor()
        {
            //fixme: test
        }

        [TestMethod]
        public void TestAttributes()
        {
            //fixme: test these

            Attribute att;
            att = new AutoEditor.ValuesAttribute(new string[] { "a", "b", "c" });
            att = new AutoEditor.ValuesAttribute(typeof(System.Windows.Forms.AnchorStyles));
            att = new AutoEditor.ValuesAttribute(typeof(TestValues));
            att = new AutoEditor.DisplayNameAttribute("bob");
            att = new AutoEditor.DisplayOrderAttribute(42);
            att = new AutoEditor.SubEditorAttribute(closeOnClick: true);
            att = new AutoEditor.HiddenAttribute();
            att = new AutoEditor.DisabledAttribute();
            att = new AutoEditor.PushButtonAttribute("test");
        }

        [TestMethod]
        [DataRow("FftBandpassHPF3dB", "Fft Bandpass HPF 3dB")]
        [DataRow("bob6dB", "bob 6dB")]
        [DataRow("My2MHzTest", "My 2MHz Test")]
        [DataRow("Signal3dBRange", "Signal 3dB Range")]
        [DataRow("XMLHTTPReader2D", "XMLHTTP Reader 2D")]
        [DataRow("m_SampleRate4200Hz", "Sample Rate 4200Hz")]
        [DataRow("m_SampleRate10kHz", "Sample Rate 10kHz")]
        [DataRow("PadLeftWithFirst", "Pad Left With First")]
        [DataRow("TriggerValue", "Trigger Value")]
        [DataRow("HPF", "HPF")]
        [DataRow("PreTriggerSample", "Pre Trigger Sample")]
        public void TestDisplayNameFormatting(string input, string expected)
        {
            var result = AutoEditor.PrettyName(input);
            Assert.AreEqual(expected, result);
        }
    }
}