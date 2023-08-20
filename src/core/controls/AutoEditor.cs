using System.Reflection;

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

            if (SourceData is AutoEditorBase)
            {
                AutoEditorBase obj = (AutoEditorBase)SourceData;
                obj.UpdateControls = (Action)Delegate.Combine(obj.UpdateControls, (Action)delegate
                {
                });
            }
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
                    string[]? array = Values(SourceData, item.Name);
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
                    string[]? array = Values(SourceData, item.Name);
                    if (array != null)
                    {
                        ((ListBox)item).Items.Clear();
                        ((ListBox)item).Items.AddRange(array);
                    }
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
                if (item.Controls.Count > 0)
                {
                    new AutoEditor(this, item.Controls);
                }
            }
            UpdateControls();
            if (SourceData != null && SourceData is AutoEditorBase)
            {
                AutoEditorBase obj = (AutoEditorBase)SourceData;
                obj.UpdateControls = (Action)Delegate.Combine(obj.UpdateControls, new Action(UpdateControls));
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
                    object? value = GetValue(sourceData, checkBox.Name);
                    if (value != null && checkBox.Checked != (bool)value)
                    {
                        checkBox.Checked = (bool)value;
                    }
                }
                else if (control is RadioButton)
                {
                    RadioButton radioButton = (RadioButton)control;
                    object? value = GetValue(sourceData, radioButton.Name);
                    if (value != null && radioButton.Checked != (bool)value)
                    {
                        radioButton.Checked = (bool)value;
                    }
                }
                else if (control is CheckedListBox)
                {
                    CheckedListBox checkedListBox = (CheckedListBox)control;
                    for (int i = 0; i < checkedListBox.Items.Count; i++)
                    {
                        object? value = GetValue(sourceData, checkedListBox.Items[i]?.ToString() ?? "");
                        if (value != null)
                        {
                            CheckState checkState = (((bool)value) ? CheckState.Checked : CheckState.Unchecked);
                            if (checkedListBox.GetItemCheckState(i) != checkState)
                            {
                                checkedListBox.SetItemCheckState(i, ((bool)value) ? CheckState.Checked : CheckState.Unchecked);
                            }
                        }
                    }
                }
                else if (control is Panel)
                {
                    Panel panel = (Panel)control;
                    object? value = GetValue(sourceData, panel.Name);
                    if (value != null && value is Color)
                    {
                        panel.BackColor = (Color)value;
                    }
                }
                else if (!(control is Button))
                {
                    object? value = GetValue(sourceData, control.Name);
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
            object? value = GetValue(SourceData, control.Name);
            if (value is Color && control is Panel)
            {
                ColorDialog colorDialog = new ColorDialog();
                colorDialog.Color = (Color)value;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    SetValue(control.Name, colorDialog.Color);
                    control.BackColor = colorDialog.Color;
                }
            }
            else if (control is Button && value != null)
            {
                object[]? array = SourceData.GetType().GetMember(control.Name)[0].GetCustomAttributes(typeof(AutoEditor.SubEditorAttribute), inherit: false);
                if (array != null && array.Length != 0)
                {
                    // sub editor
                    AutoEditorForm? autoEditorForm = ParentForm(control) as AutoEditorForm;
                    bool closeOnClick = ((array[0] as AutoEditor.SubEditorAttribute)?.CloseOnClick ?? false) && autoEditorForm != null;
                    if (closeOnClick && autoEditorForm != null)
                    {
                        autoEditorForm.Visible = false;
                    }
                    new AutoEditorForm().ShowDialog(control.Text, control.Name, value);
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
                    SetValue(control.Name, true);
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
                    SetValue(((Control)sender).Name, ((CheckBox)sender).Checked);
                }
                else if (sender is RadioButton)
                {
                    SetValue(((Control)sender).Name, ((RadioButton)sender).Checked);
                }
                else
                {
                    SetValue(((Control)sender).Name, ((Control)sender).Text);
                }
            }
        }

        private static object? GetValue(object sourceData, string name)
        {
            object? obj = null;
            MemberInfo[] member = sourceData.GetType().GetMember(name);
            if (member != null && member.Length != 0)
            {
                if (member[0] is FieldInfo)
                {
                    obj = ((FieldInfo)member[0]).GetValue(sourceData);
                }
                else if (member[0] is PropertyInfo)
                {
                    obj = ((PropertyInfo)member[0]).GetValue(sourceData, null);
                }
            }
            if (obj == null)
            {
                int index;
                string newName;
                Array? array = ArrayIndex(sourceData, name, out index, out newName);
                if (array != null && array.Length > index)
                {
                    obj = array.GetValue(index);
                }
            }
            return obj;
        }

        public static void SetValueList(object obj, Dictionary<string, object> valuesToSet)
        {
            AutoEditor autoEditor = new AutoEditor(obj, null);
            foreach (KeyValuePair<string, object> item in valuesToSet)
            {
                autoEditor.SetValue(item.Key, item.Value);
            }
        }

        private static Array? ArrayIndex(object sourceData, string name, out int index, out string newName)
        {
            index = 0;
            Array? result = null;
            newName = name;
            if (name.Length > 1 && char.IsDigit(name[name.Length - 1]))
            {
                newName = name;
                while (newName.Length > 1 && char.IsDigit(newName[newName.Length - 1]))
                {
                    newName = newName.Substring(0, newName.Length - 1);
                }
                object? value = GetValue(sourceData, newName);
                if (value != null && int.TryParse(name.Substring(newName.Length), out index))
                {
                    result = value as Array;
                }
            }
            return result;
        }

        private void SetValue(string name, object value)
        {
            if (SourceData == null) return;
            MemberInfo[] member = SourceData.GetType().GetMember(name);
            object? obj = null;
            if (member != null && member.Length != 0)
            {
                if (member[0] is FieldInfo)
                {
                    FieldInfo fieldInfo = (FieldInfo)member[0];
                    obj = ParseTo(fieldInfo.FieldType, value);
                    if (obj != null && !fieldInfo.IsLiteral && !fieldInfo.IsInitOnly)
                    {
                        InvokeOnChanging();
                        fieldInfo.SetValue(SourceData, obj);
                        InvokeOnChanged();
                    }
                }
                else if (member[0] is PropertyInfo)
                {
                    PropertyInfo propertyInfo = (PropertyInfo)member[0];
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
            else
            {
                int index;
                string newName;
                Array? array = ArrayIndex(SourceData, name, out index, out newName);
                if (array != null && array.Length > index)
                {
                    Type? elementType = array.GetType().GetElementType();
                    if (elementType != null)
                    {
                        obj = ParseTo(elementType, value);
                    }
                    if (obj != null)
                    {
                        InvokeOnChanging();
                        array.SetValue(obj, index);
                        InvokeOnChanged();
                    }
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

        private static object? ParseTo(Type type, object value)
        {
            try
            {
                if (type == typeof(bool) && value is bool)
                {
                    return value;
                }
                if (type.IsEnum && value is string)
                {
                    return ((string)value).Length > 0 ? Enum.Parse(type, (string)value) : null;
                }
                if (type == typeof(string) && value is string)
                {
                    return value;
                }
                if (value is string
                    && (value as string == "" || value as string == "-")
                    && typeof(double).IsAssignableFrom(type))
                {
                    return 0;
                }
                if ((type.IsPrimitive || type.IsEnum) && type == value.GetType())
                {
                    return value;
                }
                if (type == typeof(Color) && value is Color)
                {
                    return value;
                }
                MemberInfo[] member = type.GetMember("Parse");
                if (member != null && member.Length != 0 && member[0] is MethodInfo)
                {
                    MethodInfo methodInfo = (MethodInfo)member[0];
                    object[] parameters = { value };
                    return methodInfo.Invoke(null, parameters);
                }
            }
            catch { }
            return null;
        }

        public static Dictionary<string, object> GetValueList(object obj, List<string> namesToExtract)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            foreach (string item in namesToExtract)
            {
                object? value = GetValue(obj, item);
                if (value != null)
                {
                    dictionary.Add(item, value);
                }
            }
            return dictionary;
        }

        private static string[]? Values(object sourceData, string name)
        {
            string[]? result = null;
            object? value = GetValue(sourceData, name);
            Type? type = (value is Enum) ? value.GetType() : null;

            MemberInfo[] member = sourceData.GetType().GetMember(name);
            if (member.Length == 0)
            {
                int index;
                string newName;
                Array? array = ArrayIndex(sourceData, name, out index, out newName);
                if (array != null)
                {
                    type = (array.GetValue(0) is Enum) ? array.GetValue(0)?.GetType() : null;
                    member = sourceData.GetType().GetMember(newName);
                }
            }
            if (member.Length != 0 && type != null)
            {
                result = Enum.GetNames(type);
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
