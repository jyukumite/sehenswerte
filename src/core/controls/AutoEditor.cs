using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System;
using System.Collections;
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
        private CommitMode m_CommitMode = CommitMode.Immediate;
        private bool m_ReadOnly;
        private bool m_Updating;
        private static int m_UpdateRecursion;

        public enum CommitMode
        {
            Immediate,   // text controls commit on every TextChanged (default, original behaviour)
            OnValidated, // text controls commit on focus-leave (Validated) or Enter
        }

        internal class EditRow
        {
            public MemberInfo MemberInfo;
            public string? DisplayText;
            public int? ObjectIndex;
            public double DisplayOrder;
            public string? GroupName;
            public Type Type;

            // [InlineClass] children: resolve MemberInfo against this instead of SourceData.
            public object? NestedSource;
            // [InlineClass] children: ancestor chain, innermost-out, excluding SourceData.
            public object[]? OwnerChain;
            // [InlineClass] children: sort under the host field's DisplayOrder.
            public double? ParentDisplayOrder;
            // [InlineClass] children: host field's groupName, emitted once at block entry.
            public string? HostGroupName;
            // [ArrayEditor] support.
            public bool OpenArraySubForm;
            public bool OpenElementSubForm;

            public EditRow(MemberInfo memberInfo, Type type)
            {
                MemberInfo = memberInfo;
                Type = type;
            }
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

        // Inlines the members of a nested class directly into the parent panel at the
        // host field's [DisplayOrder] slot. Child rows keep their own ordering/grouping
        // within that slot. Alternative to [SubEditor], which uses a button + popup form.
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class InlineClassAttribute : Attribute
        {
        }

        // Generates a per-element editor for an IList/array field. Inline mode emits one
        // row per element directly into the parent panel; SubForm mode emits a single
        // button that opens a popup AutoEditorForm whose contents are the inline rows.
        // Element types that AutoEditor renders as scalars (primitive/enum/bool/string)
        // become a control per row; class-typed elements become a button per row that
        // opens a per-element AutoEditorForm.
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class ArrayEditorAttribute : Attribute
        {
            public enum DisplayMode { Inline, SubForm }
            public DisplayMode Mode;
            public string ItemLabelFormat; // {0} = index
            public string? ButtonCaption;  // SubForm mode button text
            public ArrayEditorAttribute(
                DisplayMode mode = DisplayMode.Inline,
                string itemLabelFormat = "[{0}]",
                string? buttonCaption = null)
            {
                Mode = mode;
                ItemLabelFormat = itemLabelFormat;
                ButtonCaption = buttonCaption;
            }
        }

        public class ValueListEntry // for List<ValueArrayEntry>
        {
            public string Name = "";
            public string Value = "";
            public int Order;
            public object? Tag;
        }

        // Wraps an IList<T> for ArrayEditor SubForm mode: the popup AutoEditorForm
        // generates inline rows from the [ArrayEditor(Inline)] field on this wrapper.
        // Items is assigned via reflection in the SubForm launch path.
#pragma warning disable CS0649 //Field is never assigned to, and will always have its default value
        internal class ArraySubFormHost<T>
        {
            [ArrayEditor(ArrayEditorAttribute.DisplayMode.Inline)]
            public IList<T>? Items;
        }
#pragma warning restore CS0649

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class HiddenAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class DisabledAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class PasswordAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class PushButtonAttribute : Attribute
        {
            public string Caption;
            public PushButtonAttribute(string caption) { Caption = caption; }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        public class RangeAttribute : Attribute
        {
            public double Min;
            public double Max;
            public double Step;
            public RangeAttribute(double min, double max, double step)
            {
                Min = min;
                Max = max;
                Step = step;
            }
        }

        public interface ValuesAttributeInterface
        {
            public IEnumerable<string> GetValues();
        }

        public AutoEditor(object source, Control.ControlCollection? controls,
                          CommitMode commitMode = CommitMode.Immediate, bool readOnly = false)
        {
            Controls = controls;
            m_CommitMode = commitMode;
            m_ReadOnly = readOnly;
            if (source is AutoEditor)
            {
                SourceData = ((AutoEditor)source).SourceData;
                Parent = (AutoEditor)source;
                m_CommitMode = Parent.m_CommitMode; // child editors (nested controls) inherit
                m_ReadOnly = Parent.m_ReadOnly;
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

        internal static bool IsInlineClass(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(InlineClassAttribute), inherit: false).Length != 0;
        }

        internal static ArrayEditorAttribute? ArrayEditor(MemberInfo info)
        {
            var attrs = info.GetCustomAttributes(typeof(ArrayEditorAttribute), inherit: false);
            return attrs.Length == 0 ? null : (ArrayEditorAttribute)attrs[0];
        }

        internal static Type? MemberType(MemberInfo info)
        {
            return info is FieldInfo f ? f.FieldType
                 : info is PropertyInfo p ? p.PropertyType
                 : null;
        }

        internal static Type? ElementType(Type listType)
        {
            if (listType.IsArray) return listType.GetElementType();
            if (listType.IsGenericType) return listType.GetGenericArguments()[0];
            return null;
        }

        // Element types that GenerateControl renders as a scalar control rather than
        // a sub-editor button. Class-typed elements fall through to per-element button rows.
        internal static bool IsScalarElementType(Type t)
        {
            return t == typeof(byte) || t == typeof(sbyte)
                || t == typeof(short) || t == typeof(ushort)
                || t == typeof(int) || t == typeof(uint)
                || t == typeof(long) || t == typeof(ulong)
                || t == typeof(float) || t == typeof(double)
                || t == typeof(string) || t == typeof(bool)
                || t.IsEnum;
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

        internal static bool IsPassword(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(PasswordAttribute), inherit: false).Length != 0;
        }

        internal static RangeAttribute? Range(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(RangeAttribute), inherit: false);
            return (customAttributes.Length == 0) ? null : (customAttributes[0] as RangeAttribute);
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
                    if (!m_ReadOnly)
                    {
                        ((ComboBox)item).SelectedIndexChanged += TextChanged;
                        ((ComboBox)item).TextChanged += TextChanged;
                    }
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
                    if (!m_ReadOnly)
                    {
                        ((ListBox)item).SelectedIndexChanged += TextChanged;
                        ((ListBox)item).TextChanged += TextChanged;
                    }
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
            if (m_ReadOnly) return; // no commit wiring at all - the editor is a viewer
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
                else if (m_CommitMode == CommitMode.OnValidated && item is TextBoxBase)
                {
                    item.Validated += TextChanged; // focus-leave commit
                    item.KeyDown += TextKeyDown;   // Enter commit
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
            control.ExceptionToMessagebox(() =>
            {
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
                    EditRow? row = control.Tag as EditRow;
                    if (row?.OpenArraySubForm == true && value is IList listForForm)
                    {
                        Type elementType = ElementType(row.Type) ?? typeof(object);
                        Type hostType = typeof(ArraySubFormHost<>).MakeGenericType(elementType);
                        object host = Activator.CreateInstance(hostType)!;
                        hostType.GetField("Items")!.SetValue(host, listForForm);
                        // Popup creates its own AutoEditor with no Parent link, so changes
                        // inside it can't propagate up via InvokeOnChanged. Fire ours on OK.
                        if (new AutoEditorForm().ShowDialog(control.Text, row.DisplayText ?? "", host))
                        {
                            InvokeOnChanged();
                        }
                    }
                    else if (row?.OpenElementSubForm == true)
                    {
                        if (new AutoEditorForm().ShowDialog(control.Text, row.DisplayText ?? "", value))
                        {
                            InvokeOnChanged();
                        }
                    }
                    else
                    {
                        object[]? array = row?.MemberInfo
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
                            bool ok = new AutoEditorForm().ShowDialog(control.Text, (control.Tag as EditRow)?.DisplayText ?? "", value);
                            if (closeOnClick)
                            {
                                autoEditorForm?.ButtonOK_Click(autoEditorForm, new EventArgs());
                            }
                            else if (ok)
                            {
                                // Popup creates its own AutoEditor with no Parent link, so changes
                                // inside it can't propagate up via InvokeOnChanged. Fire ours on OK.
                                InvokeOnChanged();
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
            }, "Edit setting");
        }

        private Form? ParentForm(Control control)
        {
            return (control == null) ? null
                   : (control is Form) ? (Form)control
                   : ParentForm(control.Parent);
        }

        private void TextKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && sender is TextBoxBase box && !box.Multiline)
            {
                TextChanged(sender, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true; // kill the ding
            }
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
            object source = row.NestedSource ?? sourceData;
            object? obj = null;
            if (row.ObjectIndex != null)
            {
                object? container = row.MemberInfo is FieldInfo f
                    ? f.GetValue(source)
                    : row.MemberInfo is PropertyInfo p ? p.GetValue(source, null) : null;
                int idx = row.ObjectIndex.Value;
                if (container is List<ValueListEntry> vle)
                {
                    return (idx >= 0 && idx < vle.Count) ? vle[idx].Value : null;
                }
                if (container is IList list)
                {
                    return (idx >= 0 && idx < list.Count) ? list[idx] : null;
                }
                return null;
            }
            else if (row.MemberInfo is FieldInfo)
            {
                obj = ((FieldInfo)row.MemberInfo).GetValue(source);
            }
            else if (row.MemberInfo is PropertyInfo)
            {
                obj = ((PropertyInfo)row.MemberInfo).GetValue(source, null);
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
            object source = row.NestedSource ?? SourceData;
            object? obj = null;
            if (row.ObjectIndex != null)
            {
                object? container = row.MemberInfo is FieldInfo cf
                    ? cf.GetValue(source)
                    : row.MemberInfo is PropertyInfo cp ? cp.GetValue(source, null) : null;
                int idx = row.ObjectIndex.Value;
                if (container is List<ValueListEntry> vle)
                {
                    if (idx < 0 || idx >= vle.Count) return;
                    var entry = vle[idx];
                    entry.Value = (string)value;
                }
                else if (container is IList list)
                {
                    if (idx < 0 || idx >= list.Count) return;
                    object? parsed = ParseTo(row.Type, value);
                    if (parsed != null)
                    {
                        InvokeOnChanging();
                        list[idx] = parsed;
                        InvokeOnChanged(row.OwnerChain);
                    }
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
                    fieldInfo.SetValue(source, obj);
                    InvokeOnChanged(row.OwnerChain);
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
                        propertyInfo.SetValue(source, obj, null);
                    }
                    catch (ArgumentException)
                    {
                    }
                    InvokeOnChanged(row.OwnerChain);
                }
            }
        }

        private void InvokeOnChanged(object[]? ownerChain = null)
        {
            if (m_UpdateRecursion == 0)
            {
                if (Parent != null)
                {
                    Parent.InvokeOnChanged(ownerChain);
                }
                else
                {
                    if (ownerChain != null)
                    {
                        foreach (var o in ownerChain)
                        {
                            (o as AutoEditorBase)?.OnChanged?.Invoke();
                        }
                    }
                    if (SourceData is AutoEditorBase sourceBase
                        && (ownerChain == null || Array.IndexOf(ownerChain, SourceData) < 0))
                    {
                        sourceBase.OnChanged?.Invoke();
                    }
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

        class ArrayInlineHost
        {
            [AutoEditor.ArrayEditor(AutoEditor.ArrayEditorAttribute.DisplayMode.Inline, "item {0}")]
            public double[] Items = new double[3];
        }

        class ArraySubFormHost
        {
            [AutoEditor.ArrayEditor(AutoEditor.ArrayEditorAttribute.DisplayMode.SubForm)]
            public List<int> Items = new();
        }

        class InlineClassHost
        {
            [AutoEditor.InlineClass]
            [AutoEditor.DisplayOrder(5)]
            public NestedTestData Inner = new NestedTestData();
        }

        // plain (non-AutoEditorBase) child + plain wrapper: the shape the Bluetooth
        // panel uses to render several protocol packets in one editor via [InlineClass]
        class PlainInlineChild
        {
            public int Value = 7;
            public string Label = "hello";
        }

        class PlainInlineWrapper
        {
            [AutoEditor.InlineClass, AutoEditor.DisplayOrder(0, "First")]
            public PlainInlineChild First = new PlainInlineChild();
            [AutoEditor.InlineClass, AutoEditor.DisplayOrder(1, "Second")]
            public PlainInlineChild Second = new PlainInlineChild();
        }


        [TestMethod]
        public void TestAutoEditor()
        {
            //fixme: test
        }

        class NestedTestData : AutoEditorBase
        {
            [AutoEditor.DisplayOrder(1)]
            public double Nested1 = 1.5;
            [AutoEditor.DisplayOrder(2)]
            public string Nested2 = "hello";
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
            att = new AutoEditor.RangeAttribute(0, 100, 1);
            att = new AutoEditor.ArrayEditorAttribute();
            att = new AutoEditor.ArrayEditorAttribute(AutoEditor.ArrayEditorAttribute.DisplayMode.SubForm, "[{0}]", "Edit...");
            att = new AutoEditor.InlineClassAttribute();
        }

        [TestMethod]
        public void TestArrayEditorAttributeDefaults()
        {
            var attr = new AutoEditor.ArrayEditorAttribute();
            Assert.AreEqual(AutoEditor.ArrayEditorAttribute.DisplayMode.Inline, attr.Mode);
            Assert.AreEqual("[{0}]", attr.ItemLabelFormat);
            Assert.IsNull(attr.ButtonCaption);
        }

        [TestMethod]
        public void TestArrayEditorAccessorFindsAttribute()
        {
            var member = typeof(ArrayInlineHost).GetField(nameof(ArrayInlineHost.Items))!;
            var attr = AutoEditor.ArrayEditor(member);
            Assert.IsNotNull(attr);
            Assert.AreEqual(AutoEditor.ArrayEditorAttribute.DisplayMode.Inline, attr!.Mode);
            Assert.AreEqual("item {0}", attr.ItemLabelFormat);

            var plainMember = typeof(TestClass).GetProperty(nameof(TestClass.TestString))!;
            Assert.IsNull(AutoEditor.ArrayEditor(plainMember));
        }

        [TestMethod]
        public void TestIsInlineClass()
        {
            var inlined = typeof(InlineClassHost).GetField(nameof(InlineClassHost.Inner))!;
            Assert.IsTrue(AutoEditor.IsInlineClass(inlined));

            var plain = typeof(TestClass).GetProperty(nameof(TestClass.TestInt))!;
            Assert.IsFalse(AutoEditor.IsInlineClass(plain));
        }

        [TestMethod]
        public void TestElementType()
        {
            Assert.AreEqual(typeof(double), AutoEditor.ElementType(typeof(double[])));
            Assert.AreEqual(typeof(string), AutoEditor.ElementType(typeof(List<string>)));
            Assert.AreEqual(typeof(int), AutoEditor.ElementType(typeof(IList<int>)));
            // Non-generic IList has no element type info; helper returns null.
            Assert.IsNull(AutoEditor.ElementType(typeof(System.Collections.ArrayList)));
            Assert.IsNull(AutoEditor.ElementType(typeof(string)));
        }

        [TestMethod]
        public void TestMemberType()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.TestInt))!;
            Assert.AreEqual(typeof(int), AutoEditor.MemberType(prop));

            var field = typeof(ArrayInlineHost).GetField(nameof(ArrayInlineHost.Items))!;
            Assert.AreEqual(typeof(double[]), AutoEditor.MemberType(field));
        }

        [TestMethod]
        public void TestIsScalarElementType()
        {
            Assert.IsTrue(AutoEditor.IsScalarElementType(typeof(int)));
            Assert.IsTrue(AutoEditor.IsScalarElementType(typeof(double)));
            Assert.IsTrue(AutoEditor.IsScalarElementType(typeof(string)));
            Assert.IsTrue(AutoEditor.IsScalarElementType(typeof(bool)));
            Assert.IsTrue(AutoEditor.IsScalarElementType(typeof(System.Windows.Forms.AnchorStyles))); // enum
            Assert.IsFalse(AutoEditor.IsScalarElementType(typeof(NestedTestData)));
            Assert.IsFalse(AutoEditor.IsScalarElementType(typeof(object)));
        }

        [TestMethod]
        public void TestArraySubFormHostHasInlineAttribute()
        {
            // The wrapper used at runtime to host an array in a popup form must carry
            // [ArrayEditor(Inline)] on its Items field so AutoEditorControl expands it inline.
            var member = typeof(AutoEditor.ArraySubFormHost<double>)
                .GetField(nameof(AutoEditor.ArraySubFormHost<double>.Items))!;
            var attr = AutoEditor.ArrayEditor(member);
            Assert.IsNotNull(attr);
            Assert.AreEqual(AutoEditor.ArrayEditorAttribute.DisplayMode.Inline, attr!.Mode);
        }

#pragma warning disable CS0649 // fields are written via reflection by AutoEditor
        class CommitTestData
        {
            public int Number = 42;
            public string Name = "x";
            public bool Flag;
            public System.Windows.Forms.AnchorStyles Anchor = System.Windows.Forms.AnchorStyles.Left;
            [AutoEditor.PushButton("go")]
            public bool Go;
        }
#pragma warning restore CS0649

        private static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control sub in AllControls(child))
                {
                    yield return sub;
                }
            }
        }

        private static Control? FindControl(Control root, string memberName, Type controlType)
        {
            return AllControls(root).FirstOrDefault(c =>
                controlType.IsAssignableFrom(c.GetType())
                && (c.Tag as AutoEditor.EditRow)?.MemberInfo.Name == memberName);
        }

        [TestMethod]
        public void TestCommitModeDefaults()
        {
            var control = new AutoEditorControl();
            Assert.AreEqual(AutoEditor.CommitMode.Immediate, control.CommitMode);
            Assert.IsFalse(control.ReadOnly);
        }

        [TestMethod]
        public void TestImmediateCommit()
        {
            var data = new CommitTestData();
            var control = new AutoEditorControl();
            control.Generate(data);
            var box = (TextBox)FindControl(control, nameof(CommitTestData.Number), typeof(TextBox))!;
            box.Text = "7";
            Assert.AreEqual(7, data.Number); // default mode commits per keystroke
        }

        [TestMethod]
        public void TestOnValidatedDefersCommit()
        {
            var data = new CommitTestData();
            var control = new AutoEditorControl { CommitMode = AutoEditor.CommitMode.OnValidated };
            control.Generate(data);
            var box = (TextBox)FindControl(control, nameof(CommitTestData.Number), typeof(TextBox))!;
            box.Text = "7";
            Assert.AreEqual(42, data.Number); // deferred until Validated/Enter
        }

        [TestMethod]
        public void TestReadOnlyRendering()
        {
            var data = new CommitTestData();
            var control = new AutoEditorControl { ReadOnly = true };
            control.Generate(data);
            var numberBox = (TextBox)FindControl(control, nameof(CommitTestData.Number), typeof(TextBox))!;
            Assert.IsTrue(numberBox.ReadOnly);
            Assert.IsFalse(AllControls(control).Any(c => c is ComboBox)); // enum renders as read-only TextBox
            var enumBox = (TextBox)FindControl(control, nameof(CommitTestData.Anchor), typeof(TextBox))!;
            Assert.IsTrue(enumBox.ReadOnly);
            var checkBox = (CheckBox)FindControl(control, nameof(CommitTestData.Flag), typeof(CheckBox))!;
            Assert.IsFalse(checkBox.AutoCheck);
            var goButton = (Button)FindControl(control, nameof(CommitTestData.Go), typeof(Button))!;
            Assert.IsFalse(goButton.Enabled);
        }

        [TestMethod]
        public void TestReadOnlySuppressesCommitAndUpdates()
        {
            var data = new CommitTestData();
            var control = new AutoEditorControl { ReadOnly = true };
            control.Generate(data);
            var box = (TextBox)FindControl(control, nameof(CommitTestData.Number), typeof(TextBox))!;
            box.Text = "7";
            Assert.AreEqual(42, data.Number); // no commit wiring at all

            data.Number = 9;
            control.UpdateControls(); // public refresh for non-AutoEditorBase sources
            Assert.AreEqual("9", box.Text);
        }

        [TestMethod]
        public void TestPlainInlineClassWrapper()
        {
            // one editor over a plain wrapper of plain [InlineClass] children (the
            // Bluetooth panel's packet-column shape) - renders and refreshes in place
            var wrapper = new PlainInlineWrapper();
            var control = new AutoEditorControl();
            control.Generate(wrapper);
            var boxes = AllControls(control)
                .OfType<TextBox>()
                .Where(t => (t.Tag as AutoEditor.EditRow)?.MemberInfo.Name == nameof(PlainInlineChild.Value))
                .ToList();
            Assert.AreEqual(2, boxes.Count); // First.Value and Second.Value both rendered
            Assert.IsTrue(boxes.All(b => b.Text == "7"));

            wrapper.First.Value = 11; // mutate the nested instance in place
            control.UpdateControls();
            Assert.IsTrue(boxes.Any(b => b.Text == "11"));
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