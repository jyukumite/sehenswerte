using System.Reflection;

namespace SehensWerte.Controls
{
    public class AutoEditorControl : UserControl
    {
        private object? SourceData;
        private AutoEditor? m_Editor;
        public Action<AutoEditor>? OnChange;
        private Dictionary<AutoEditor.EditRow, object> m_StartValues = new Dictionary<AutoEditor.EditRow, object>();
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

            List<string> names = new List<string>();
            if (SourceData != null)
            {
                var rows = new List<AutoEditor.EditRow>();
                foreach (MemberInfo memberInfo in SourceData.GetType().GetMembers())
                {
                    if ((memberInfo as FieldInfo)?.FieldType == typeof(List<AutoEditor.ValueListEntry>))
                    {
                        var va = ((FieldInfo)memberInfo).GetValue(SourceData) as List<AutoEditor.ValueListEntry>;
                        if (va != null)
                        {
                            for (int loop = 0; loop < va.Count; loop++)
                            {
                                rows.Add(new AutoEditor.EditRow()
                                {
                                    MemberInfo = memberInfo,
                                    DisplayText = va[loop].Name,
                                    Type = typeof(string),
                                    ObjectIndex = loop,
                                    DisplayOrder = va[loop].Order,
                                });
                            }
                        }
                    }
                    else if (memberInfo is FieldInfo || memberInfo is PropertyInfo)
                    {
                        rows.Add(new AutoEditor.EditRow()
                        {
                            MemberInfo = memberInfo,
                            DisplayText = AutoEditor.DisplayName(memberInfo),
                            Type = memberInfo is FieldInfo ? ((FieldInfo)memberInfo).FieldType : ((PropertyInfo)memberInfo).PropertyType,
                            ObjectIndex = null,
                            DisplayOrder = AutoEditor.DisplayOrder(memberInfo),

                        });
                    }
                }

                rows.OrderBy(x => x.DisplayOrder).ThenBy(x => x.DisplayText).ForEach(v => GenerateControl(LayoutPanel, v));

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
                    m_StartValues = AutoEditor.GetValueList(SourceData, rows);
                }
            }
        }

        private static void GenerateControl(TableLayoutPanel tableLayout, AutoEditor.EditRow row)
        {
            if (AutoEditor.IsHidden(row.MemberInfo)
                || row.Type == typeof(Delegate)
                || (row.MemberInfo is FieldInfo && (((FieldInfo)row.MemberInfo).IsLiteral
                || ((FieldInfo)row.MemberInfo).IsInitOnly)))
            {
                return;
            }

            Label control = new Label
            {
                Text = row.DisplayText,
                Dock = DockStyle.Fill,
                TextAlign = (ContentAlignment)16
            };
            try
            {
                if (AutoEditor.IsSubEditor(row.MemberInfo))
                {
                    Button control2 = new Button
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Tag = row,
                        Text = (AutoEditor.PushButtonCaption(row.MemberInfo) ?? "...")
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(control2, 1, tableLayout.RowCount);
                }
                else if (AutoEditor.Values(row.MemberInfo) != null)
                {
                    ComboBox comboBox = new ComboBox
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(comboBox, 1, tableLayout.RowCount);
                    ComboBox.ObjectCollection items = comboBox.Items;
                    object[]? items2 = AutoEditor.Values(row.MemberInfo);
                    items.AddRange(items2 ?? new object[0]);
                    comboBox.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (row.Type == typeof(byte)
                    || row.Type == typeof(int)
                    || row.Type == typeof(long)
                    || row.Type == typeof(ulong)
                    || row.Type == typeof(uint)
                    || row.Type == typeof(short)
                    || row.Type == typeof(ushort)
                    || row.Type == typeof(string)
                    || row.Type == typeof(float)
                    || row.Type == typeof(double))
                {
                    TextBox textBox = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Tag = row,
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(textBox, 1, tableLayout.RowCount);
                    textBox.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (row.Type.IsSubclassOf(typeof(Delegate)) && AutoEditor.IsPushButton(row.MemberInfo))
                {
                    Button button = new Button
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Fill,
                        Text = AutoEditor.PushButtonCaption(row.MemberInfo)
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if ((Type?)row.Type == typeof(bool) && AutoEditor.IsPushButton(row.MemberInfo))
                {
                    Button button = new Button
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Fill,
                        Text = AutoEditor.PushButtonCaption(row.MemberInfo)
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if ((Type?)row.Type == typeof(bool))
                {
                    CheckBox checkBox = new CheckBox
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Left,
                        CheckAlign = (ContentAlignment)1
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(checkBox, 1, tableLayout.RowCount);
                    checkBox.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (row.Type.BaseType == typeof(Enum))
                {
                    ComboBox comboBox = new ComboBox
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(comboBox, 1, tableLayout.RowCount);
                    comboBox.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (!(row.MemberInfo.DeclaringType?.Name == typeof(AutoEditorBase).Name))
                {
                    if (!(row.Type == typeof(Color)))
                    {
                        throw new Exception($"Unknown field/property type {row.DisplayText} {row.MemberInfo.Name} {row.Type.Name}");
                    }
                    Panel panel = new Panel
                    {
                        AutoSize = true,
                        Tag = row,
                        Dock = DockStyle.Fill
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(panel, 1, tableLayout.RowCount);
                    panel.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
            }
            catch (Exception ex)
            {
                TextBox errorControl = new TextBox
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Tag = row,
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
