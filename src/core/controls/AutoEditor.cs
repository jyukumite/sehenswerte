using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System;

namespace SehensWerte.Controls
{
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
            public int DisplayOrder;
            public Type Type;
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class ValuesAttribute : Attribute
        {
            public string[] Values;
            public ValuesAttribute() : this(new string[0]) { }
            public ValuesAttribute(string[] values) { Values = values; }
            public ValuesAttribute(Type type)
            {
                string name = nameof(ValuesAttributeInterface.GetValues);
                object? obj = Activator.CreateInstance(type);
                var array = type?.GetMethod(name)?.Invoke(obj, null);
                Values = array == null ? new string[0] : ((IEnumerable<string>)array).ToArray();
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
            public int DisplayOrder;
            public DisplayOrderAttribute() : this(0) { }
            public DisplayOrderAttribute(int displayOrder) { DisplayOrder = displayOrder; }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class SubEditorAttribute : Attribute
        {
            public bool CloseOnClick;
            public SubEditorAttribute(bool closeOnClick = false) { CloseOnClick = closeOnClick; }
        }

        public class ValueListEntry // for List<ValueArrayEntry>
        {
            public string Name;
            public string Value;
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
            IEnumerable<string> GetValues();
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
            return (customAttributes.Length == 0) ? info.Name : (customAttributes[0] as DisplayNameAttribute)?.DisplayName ?? info.Name;
        }

        internal static int DisplayOrder(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(DisplayOrderAttribute), inherit: false);
            return (customAttributes.Length == 0) ? 0 : (customAttributes[0] as DisplayOrderAttribute)?.DisplayOrder ?? 0;
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
                        comboBox.SelectedIndex =comboBox.Items.IndexOf(value.ToString());
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
}
