using System.Reflection;

namespace SehensWerte.Controls
{
    public class AutoEditorControl : UserControl
    {
        private object? SourceData;
        private AutoEditor? m_Editor;
        public Action<AutoEditor>? OnChange;
        private Dictionary<string, object> m_StartValues = new Dictionary<string, object>();
        internal TableLayoutPanel? LayoutPanel;

        public AutoEditorControl()
        {
            SuspendLayout();
            base.Name = "AutoEditorControl";
            ResumeLayout(performLayout: false);
            PerformLayout();
        }

        internal void RemoveDelegates()
        {
            m_Editor?.RemoveDelegates();
        }

        internal void Revert()
        {
            if (SourceData != null && m_Editor != null)
            {
                OnChanging(m_Editor);
                AutoEditor.SetValueList(SourceData, m_StartValues);
                OnChanged(m_Editor);
            }
        }

        public void Generate(object? sourceData)
        {
            if (LayoutPanel != null)
            {
                LayoutPanel.Controls.Clear();
                Controls.Remove(LayoutPanel);
            }
            if (sourceData != null)
            {
                SourceData = sourceData;
                GenerateControls();
                m_Editor = new AutoEditor(sourceData, Controls);
                m_Editor.OnChanging = (Action<AutoEditor>)Delegate.Combine(m_Editor.OnChanging, new Action<AutoEditor>(OnChanging));
                m_Editor.OnChanged = (Action<AutoEditor>)Delegate.Combine(m_Editor.OnChanged, new Action<AutoEditor>(OnChanged));
            }
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
            LayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                AutoScroll = true
            };
            LayoutPanel.SuspendLayout();
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
            List<KeyValuePair<string, int>> order = new List<KeyValuePair<string, int>>();
            List<string> names = new List<string>();
            if (SourceData != null)
            {
                foreach (MemberInfo memberInfo in SourceData.GetType().GetMembers())
                {
                    order.Add(new KeyValuePair<string, int>(memberInfo.Name, AutoEditor.DisplayOrder(memberInfo)));
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
                        GenerateControl(LayoutPanel, memberInfo, ((FieldInfo)memberInfo).FieldType);
                        names.Add(memberInfo.Name);
                    }
                    else if (memberInfo is PropertyInfo)
                    {
                        GenerateControl(LayoutPanel, memberInfo, ((PropertyInfo)memberInfo).PropertyType);
                        names.Add(memberInfo.Name);
                    }
                }
            }
            Panel panel = new Panel
            {
                AutoSize = true
            };
            LayoutPanel.Controls.Add(panel, 1, ++LayoutPanel.RowCount);
            LayoutPanel.Controls.Add(panel, 0, LayoutPanel.RowCount);
            LayoutPanel.ResumeLayout();
            Controls.Add(LayoutPanel);
            if (SourceData != null)
            {
                m_StartValues = AutoEditor.GetValueList(SourceData, names);
            }
        }

        private static void GenerateControl(TableLayoutPanel tableLayout, MemberInfo member, Type type)
        {
            if (AutoEditor.IsHidden(member) || type == typeof(Delegate) || (member is FieldInfo && (((FieldInfo)member).IsLiteral || ((FieldInfo)member).IsInitOnly)))
            {
                return;
            }
            Label control = new Label
            {
                Text = AutoEditor.DisplayName(member),
                Dock = DockStyle.Fill,
                TextAlign = (ContentAlignment)16
            };
            try
            {
                if (AutoEditor.IsSubEditor(member))
                {
                    Button control2 = new Button
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Name = member.Name,
                        Text = (AutoEditor.PushButtonCaption(member) ?? "...")
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(control2, 1, tableLayout.RowCount);
                }
                else if (AutoEditor.Values(member) != null)
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
                    object[]? items2 = AutoEditor.Values(member);
                    items.AddRange(items2 ?? new object[0]);
                    comboBox.Enabled = AutoEditor.IsEnabled(member);
                }
                else if (type == typeof(byte) 
                    || type == typeof(int) 
                    || type == typeof(long) 
                    || type == typeof(ulong) 
                    || type == typeof(uint) 
                    || type == typeof(short) 
                    || type == typeof(ushort) 
                    || type == typeof(string) 
                    || type == typeof(float) 
                    || type == typeof(double))
                {
                    TextBox textBox = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Name = member.Name
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(textBox, 1, tableLayout.RowCount);
                    textBox.Enabled = AutoEditor.IsEnabled(member);
                }
                else if (type.IsSubclassOf(typeof(Delegate)) && AutoEditor.IsPushButton(member))
                {
                    Button button = new Button
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill,
                        Text = AutoEditor.PushButtonCaption(member)
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(member);
                }
                else if (type == typeof(bool) && AutoEditor.IsPushButton(member))
                {
                    Button button = new Button
                    {
                        AutoSize = true,
                        Name = member.Name,
                        Dock = DockStyle.Fill,
                        Text = AutoEditor.PushButtonCaption(member)
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(member);
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
                    checkBox.Enabled = AutoEditor.IsEnabled(member);
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
                    comboBox.Enabled = AutoEditor.IsEnabled(member);
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
                    panel.Enabled = AutoEditor.IsEnabled(member);
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
    }
}
