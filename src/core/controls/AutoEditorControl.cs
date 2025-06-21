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
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            LayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            LayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
                        var order = AutoEditor.DisplayOrder(memberInfo);
                        rows.Add(new AutoEditor.EditRow()
                        {
                            MemberInfo = memberInfo,
                            DisplayText = AutoEditor.DisplayName(memberInfo),
                            Type = memberInfo is FieldInfo ? ((FieldInfo)memberInfo).FieldType : ((PropertyInfo)memberInfo).PropertyType,
                            ObjectIndex = null,
                            DisplayOrder = order.order,
                            GroupName = order.name,
                        });
                    }
                }

                double prevDisplayOrder = double.NaN;
                rows.OrderBy(row => row.DisplayOrder).ThenBy(row => row.DisplayText)
                    .ForEach(row =>
                    {
                        if ((int)row.DisplayOrder != (int)prevDisplayOrder)
                        {
                            string? groupName = rows
                                .Where(x => (int)x.DisplayOrder == (int)row.DisplayOrder)
                                .FirstOrDefault(x => x.GroupName != null, new AutoEditor.EditRow())
                                .GroupName;
                            if (groupName != null && groupName.Length > 0)
                            {
                                AddGroupNameRow(groupName);
                            }
                            prevDisplayOrder = row.DisplayOrder;
                        }
                        GenerateControl(LayoutPanel, row);
                    });

                AddFinalRow();
                LayoutPanel?.ResumeLayout();
                Controls.Add(LayoutPanel);
                if (SourceData != null)
                {
                    m_StartValues = AutoEditor.GetValueList(SourceData, rows);
                }
            }
        }

        private void AddGroupNameRow(string groupName)
        {
            Label groupLabel = new Label
            {
                Text = groupName,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                MinimumSize = new Size(0, 24),
            };
            groupLabel.UseMnemonic = false;
            LayoutPanel?.Controls.Add(groupLabel, 0, ++LayoutPanel.RowCount);
            LayoutPanel?.SetColumnSpan(groupLabel, 2);
        }

        private void AddFinalRow()
        {
            Panel panel = new Panel
            {
                AutoSize = true
            };
            LayoutPanel?.Controls.Add(panel, 1, ++LayoutPanel.RowCount);
            LayoutPanel?.Controls.Add(panel, 0, LayoutPanel.RowCount);
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
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 24),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            try
            {
                if (AutoEditor.IsSubEditor(row.MemberInfo))
                {
                    Button control2 = new Button
                    {
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top,
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
                        Dock = DockStyle.Top
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
                    Dock = DockStyle.Top,
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
