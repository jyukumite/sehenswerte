using System.Reflection;

namespace SehensWerte.Controls
{
    public class AutoEditorForm : Form
    {
        private DialogResult ResultButton;
        private object? SourceData;
        private AutoEditor? m_Editor;
        public Action<AutoEditor>? OnChange;
        private Dictionary<string, object> m_StartValues = new Dictionary<string, object>();
        private Button ButtonOK;
        private Button ButtonCancel;
        private Label LabelText;
        private Panel Panel;
        private Panel PreviewPanel;
        public string Title { set { Text = value; } }
        public string Prompt { set { LabelText.Text = value; } }
        public DialogResult Result => ResultButton;

        public interface ValuesAttributeInterface
        {
            IEnumerable<string> GetValues();
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

        public AutoEditorForm()
        {
            ButtonOK = new Button();
            LabelText = new Label();
            ButtonCancel = new Button();
            Panel = new Panel();
            PreviewPanel = new Panel();
            SuspendLayout();
            ButtonOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonOK.Location = new Point(91, 264);
            ButtonOK.Name = "ButtonOK";
            ButtonOK.Size = new Size(64, 24);
            ButtonOK.TabIndex = 1;
            ButtonOK.Text = "OK";
            ButtonOK.Click += ButtonOK_Click;
            LabelText.AutoSize = true;
            LabelText.Location = new Point(16, 8);
            LabelText.Name = "LabelText";
            LabelText.Size = new Size(54, 13);
            LabelText.TabIndex = 3;
            LabelText.Text = "InputForm";
            ButtonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ButtonCancel.DialogResult = DialogResult.Cancel;
            ButtonCancel.Location = new Point(252, 264);
            ButtonCancel.Name = "ButtonCancel";
            ButtonCancel.Size = new Size(64, 24);
            ButtonCancel.TabIndex = 2;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += ButtonCancel_Click;
            Panel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Panel.Location = new Point(12, 30);
            Panel.Name = "Panel";
            Panel.Size = new Size(377, 228);
            Panel.TabIndex = 0;
            PreviewPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            PreviewPanel.Location = new Point(380, 30);
            PreviewPanel.Name = "PreviewPanel";
            PreviewPanel.Size = new Size(377, 228);
            PreviewPanel.TabIndex = 0;
            PreviewPanel.Visible = false;
            base.ClientSize = new Size(401, 300);
            base.Controls.Add(Panel);
            base.Controls.Add(PreviewPanel);
            base.Controls.Add(ButtonCancel);
            base.Controls.Add(LabelText);
            base.Controls.Add(ButtonOK);
            base.KeyPreview = true;
            base.MaximizeBox = false;
            base.Name = "InputForm";
            base.SizeGripStyle = SizeGripStyle.Show;
            base.KeyPress += KeyPressed;
            ResumeLayout(performLayout: false);
            PerformLayout();
        }

        public static bool Show(string prompt, string title, object sourceData)
        {
            return new AutoEditorForm().ShowDialog(prompt, title, sourceData);
        }

        public bool ShowDialog(string prompt, string title, object sourceData)
        {
            Title = title;
            Prompt = prompt;
            SourceData = sourceData;
            GenerateControls();
            m_Editor = new AutoEditor(sourceData, Panel.Controls);
            m_Editor.OnChanging = (Action<AutoEditor>)Delegate.Combine(m_Editor.OnChanging, new Action<AutoEditor>(OnChanging));
            m_Editor.OnChanged = (Action<AutoEditor>)Delegate.Combine(m_Editor.OnChanged, new Action<AutoEditor>(OnChanged));
            this.MoveOnScreen();
            ShowDialog();
            return ResultButton == DialogResult.OK;
        }

        private void OnChanging(AutoEditor sender)
        {
        }

        private void OnChanged(AutoEditor sender)
        {
            OnChange?.Invoke(sender);
        }

        private void GenerateControls()
        {
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                AutoScroll = true
            };
            tableLayoutPanel.SuspendLayout();
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
            List<KeyValuePair<string, int>> order = new List<KeyValuePair<string, int>>();
            List<string> names = new List<string>();
            if (SourceData != null)
            {
                foreach (MemberInfo memberInfo in SourceData.GetType().GetMembers())
                {
                    order.Add(new KeyValuePair<string, int>(memberInfo.Name, DisplayOrder(memberInfo)));
                }
                order.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    return a.Value < b.Value ? -1
                            : (a.Value > b.Value ? 1
                            : a.Key.CompareTo(b.Key));
                });
                foreach (KeyValuePair<string, int> item in order)
                {
                    MemberInfo memberInfo = SourceData!.GetType().GetMember(item.Key)[0];
                    if (memberInfo is FieldInfo)
                    {
                        GenerateControl(tableLayoutPanel, memberInfo, ((FieldInfo)memberInfo).FieldType);
                        names.Add(memberInfo.Name);
                    }
                    else if (memberInfo is PropertyInfo)
                    {
                        GenerateControl(tableLayoutPanel, memberInfo, ((PropertyInfo)memberInfo).PropertyType);
                        names.Add(memberInfo.Name);
                    }
                }
            }
            Panel panel = new Panel
            {
                AutoSize = true
            };
            tableLayoutPanel.Controls.Add(panel, 1, ++tableLayoutPanel.RowCount);
            tableLayoutPanel.Controls.Add(panel, 0, tableLayoutPanel.RowCount);
            tableLayoutPanel.ResumeLayout();
            Panel.Controls.Add(tableLayoutPanel);
            panel.TabIndex = 0;
            base.Height = Math.Min(Screen.PrimaryScreen.WorkingArea.Height - 100, tableLayoutPanel.GetRowHeights().Sum() + 140);
            if (base.Bottom > Screen.PrimaryScreen.WorkingArea.Height)
            {
                base.Top = Math.Max(20, base.Top - (base.Bottom - Screen.PrimaryScreen.WorkingArea.Height) - 20);
            }
            if (SourceData != null)
            {
                m_StartValues = AutoEditor.GetValueList(SourceData, names);
            }
        }

        private static void GenerateControl(TableLayoutPanel tableLayout, MemberInfo member, Type type)
        {
            if (IsHidden(member) || type.IsSubclassOf(typeof(Delegate)) || type == typeof(Delegate) || (member is FieldInfo && (((FieldInfo)member).IsLiteral || ((FieldInfo)member).IsInitOnly)))
            {
                return;
            }
            Label control = new Label
            {
                Text = DisplayName(member),
                Dock = DockStyle.Fill,
                TextAlign = (ContentAlignment)16
            };
            try
            {
                if (IsSubEditor(member))
                {
                    Button control2 = new Button
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Name = member.Name,
                        Text = (PushButtonCaption(member) ?? "...")
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(control2, 1, tableLayout.RowCount);
                }
                else if (Values(member) != null)
                {
                    ComboBox comboBox = new ComboBox
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(comboBox, 1, tableLayout.RowCount);
                    ComboBox.ObjectCollection items = comboBox.Items;
                    object[] items2 = Values(member);
                    items.AddRange(items2);
                    comboBox.Enabled = IsEnabled(member);
                }
                else if (type == typeof(byte) || type == typeof(int) || type == typeof(long) || type == typeof(ulong) || type == typeof(uint) || type == typeof(string) || type == typeof(float) || type == typeof(double))
                {
                    TextBox textBox = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Name = member.Name
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(textBox, 1, tableLayout.RowCount);
                    textBox.Enabled = IsEnabled(member);
                }
                else if (type == typeof(bool) && IsPushButton(member))
                {
                    Button button = new Button
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill,
                        Text = PushButtonCaption(member)
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = IsEnabled(member);
                }
                else if (type == typeof(bool))
                {
                    CheckBox checkBox = new CheckBox
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Left,
                        CheckAlign = (ContentAlignment)1
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(checkBox, 1, tableLayout.RowCount);
                    checkBox.Enabled = IsEnabled(member);
                }
                else if (type.BaseType == typeof(Enum))
                {
                    ComboBox comboBox = new ComboBox
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(comboBox, 1, tableLayout.RowCount);
                    comboBox.Enabled = IsEnabled(member);
                }
                else if (!(member.DeclaringType?.Name == typeof(AutoEditorBase).Name))
                {
                    if (!(type == typeof(Color)))
                    {
                        throw new Exception("Unknown field/property type " + member.Name + " " + type.Name);
                    }
                    Panel panel = new Panel
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(panel, 1, tableLayout.RowCount);
                    panel.Enabled = IsEnabled(member);
                }
            }
            catch (Exception ex)
            {
                TextBox errorControl = new TextBox
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Name = member.Name,
                    ReadOnly = true
                };
                ToolTip toolTip = new ToolTip();
                toolTip.IsBalloon = true;
                toolTip.SetToolTip(errorControl, ex.ToString());
                tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                tableLayout.Controls.Add(errorControl, 1, tableLayout.RowCount);
            }
        }

        private static string[]? Values(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(ValuesAttribute), inherit: false);
            return ((customAttributes != null) && (customAttributes.Length != 0)) ? ((customAttributes[0] as ValuesAttribute)?.Values) : null;
        }

        private static string DisplayName(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(DisplayNameAttribute), inherit: false);
            return (customAttributes.Length == 0) ? info.Name : (customAttributes[0] as DisplayNameAttribute)?.DisplayName ?? info.Name;
        }

        private static int DisplayOrder(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(DisplayOrderAttribute), inherit: false);
            return (customAttributes.Length == 0) ? 0 : (customAttributes[0] as DisplayOrderAttribute)?.DisplayOrder ?? 0;
        }

        private static bool IsHidden(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(HiddenAttribute), inherit: false).Length != 0;
        }

        private static bool IsSubEditor(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(SubEditorAttribute), inherit: false).Length != 0;
        }

        private static bool IsPushButton(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(PushButtonAttribute), inherit: false).Length != 0;
        }

        private static string PushButtonCaption(MemberInfo info)
        {
            object[] customAttributes = info.GetCustomAttributes(typeof(PushButtonAttribute), inherit: false);
            return ((customAttributes == null) || (customAttributes.Length == 0)) ? "" : ((customAttributes[0] as PushButtonAttribute)?.Caption ?? "");
        }

        private static bool IsEnabled(MemberInfo info)
        {
            return info.GetCustomAttributes(typeof(DisabledAttribute), inherit: false).Length == 0;
        }

        private void KeyPressed(object? sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '\u001b':
                    ButtonCancel_Click(sender, e);
                    break;
                case '\r':
                    ButtonOK_Click(sender, e);
                    break;
            }
        }

        internal void ButtonOK_Click(object? sender, EventArgs e)
        {
            ResultButton = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            if (SourceData != null && m_Editor != null)
            {
                ResultButton = DialogResult.Cancel;
                OnChanging(m_Editor);
                AutoEditor.SetValueList(SourceData, m_StartValues);
                OnChanged(m_Editor);
                Close();
            }
        }
    }
}
