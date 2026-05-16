using System.Collections;
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
        public int PreferredHeight => LayoutPanel?.GetRowHeights().Sum() ?? 0;
        // tracks (memberInfo, source) -> last-seen IList count so the panel can rebuild when an array length changes
        private Dictionary<(MemberInfo, object), int> m_ArrayLengths = new();

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
            if (SourceData is AutoEditorBase ab)
            {
                ab.UpdateControls -= OnUpdateControlsRequested;
            }
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
            if (SourceData is AutoEditorBase oldBase)
            {
                oldBase.UpdateControls -= OnUpdateControlsRequested;
            }
            if (LayoutPanel != null)
            {
                LayoutPanel.Controls.Clear();
                Controls.Remove(LayoutPanel);
            }
            if (sourceData != null)
            {
                SourceData = sourceData;
                // Subscribe before constructing AutoEditor so our length-mismatch check runs
                // first when UpdateControls fires; we can rebuild before the static walker
                // tries to read stale ObjectIndex values against a now-shorter list.
                if (sourceData is AutoEditorBase ab)
                {
                    ab.UpdateControls -= OnUpdateControlsRequested;
                    ab.UpdateControls += OnUpdateControlsRequested;
                }
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

            if (SourceData != null)
            {
                m_ArrayLengths.Clear();
                var rows = new List<AutoEditor.EditRow>();
                CollectRows(SourceData, parentOrder: null, hostGroupName: null, visited: new HashSet<object>(), rows);

                var sorted = rows
                    .OrderBy(r => r.ParentDisplayOrder ?? r.DisplayOrder)
                    .ThenBy(r => r.ParentDisplayOrder.HasValue ? r.DisplayOrder : double.MinValue)
                    .ThenBy(r => r.ObjectIndex ?? -1)
                    .ThenBy(r => r.DisplayText)
                    .ToList();

                int prevPrimary = int.MinValue;
                (int, int) prevKey = (int.MinValue, int.MinValue);
                foreach (var row in sorted)
                {
                    int primary = (int)(row.ParentDisplayOrder ?? row.DisplayOrder);
                    int secondary = row.ParentDisplayOrder.HasValue ? (int)row.DisplayOrder : int.MinValue;
                    if (primary != prevPrimary)
                    {
                        string? hostGroup = sorted
                            .Where(x => (int)(x.ParentDisplayOrder ?? x.DisplayOrder) == primary)
                            .Select(x => x.HostGroupName)
                            .FirstOrDefault(g => !string.IsNullOrEmpty(g));
                        if (!string.IsNullOrEmpty(hostGroup)) AddGroupNameRow(hostGroup!);
                        prevPrimary = primary;
                        prevKey = (int.MinValue, int.MinValue);
                    }
                    var key = (primary, secondary);
                    if (key != prevKey)
                    {
                        string? groupName = sorted
                            .Where(x =>
                            {
                                int xp = (int)(x.ParentDisplayOrder ?? x.DisplayOrder);
                                int xs = x.ParentDisplayOrder.HasValue ? (int)x.DisplayOrder : int.MinValue;
                                return xp == primary && xs == secondary;
                            })
                            .FirstOrDefault(x => !string.IsNullOrEmpty(x.GroupName))
                            ?.GroupName;
                        if (!string.IsNullOrEmpty(groupName)) AddGroupNameRow(groupName!);
                        prevKey = key;
                    }
                    GenerateControl(LayoutPanel, row);
                }

                AddFinalRow();
                LayoutPanel?.ResumeLayout();
                Controls.Add(LayoutPanel);
                m_StartValues = AutoEditor.GetValueList(SourceData, rows);
            }
        }

        // Walks members of `source` and emits EditRows. Recurses for [InlineClass]; expands arrays decorated
        // with [ArrayEditor]. parentOrder/hostGroupName apply to all rows emitted from this call when source != SourceData.
        private void CollectRows(object source, double? parentOrder, string? hostGroupName, HashSet<object> visited, List<AutoEditor.EditRow> rows)
        {
            bool isNested = parentOrder.HasValue;
            foreach (MemberInfo memberInfo in source.GetType().GetMembers())
            {
                Type? memberType = AutoEditor.MemberType(memberInfo);
                if (memberType == null) continue;

                // [InlineClass] is allowed on otherwise-Hidden host fields; the host itself
                // never renders as a row, so Hidden is moot for it. Check it before the Hidden filter.
                if (AutoEditor.IsInlineClass(memberInfo))
                {
                    object? nested = memberInfo is FieldInfo fi ? fi.GetValue(source)
                                  : ((PropertyInfo)memberInfo).GetValue(source, null);
                    if (nested != null && !visited.Contains(nested))
                    {
                        var hostOrder = AutoEditor.DisplayOrder(memberInfo);
                        visited.Add(nested);
                        CollectRows(
                            nested,
                            parentOrder: parentOrder ?? hostOrder.order,
                            hostGroupName: hostGroupName ?? hostOrder.name,
                            visited,
                            rows);
                        visited.Remove(nested);
                    }
                    continue;
                }

                if (AutoEditor.IsHidden(memberInfo)) continue;

                // existing back-compat path: List<ValueListEntry> always inlines as named rows
                if (memberInfo is FieldInfo vleField && vleField.FieldType == typeof(List<AutoEditor.ValueListEntry>))
                {
                    var va = vleField.GetValue(source) as List<AutoEditor.ValueListEntry>;
                    if (va != null)
                    {
                        m_ArrayLengths[(memberInfo, source)] = va.Count;
                        for (int i = 0; i < va.Count; i++)
                        {
                            rows.Add(new AutoEditor.EditRow(memberInfo, typeof(string))
                            {
                                DisplayText = va[i].Name,
                                ObjectIndex = i,
                                DisplayOrder = va[i].Order,
                                NestedSource = isNested ? source : null,
                                ParentDisplayOrder = parentOrder,
                                HostGroupName = hostGroupName,
                            });
                        }
                    }
                    continue;
                }

                if (!(memberInfo is FieldInfo || memberInfo is PropertyInfo)) continue;

                var memberOrder = AutoEditor.DisplayOrder(memberInfo);

                // [ArrayEditor] -- expand inline rows or emit a single SubForm button
                var arrayAttr = AutoEditor.ArrayEditor(memberInfo);
                if (arrayAttr != null && typeof(IList).IsAssignableFrom(memberType))
                {
                    Type elementType = AutoEditor.ElementType(memberType) ?? typeof(object);
                    object? container = memberInfo is FieldInfo afi ? afi.GetValue(source)
                                     : ((PropertyInfo)memberInfo).GetValue(source, null);

                    if (arrayAttr.Mode == AutoEditor.ArrayEditorAttribute.DisplayMode.SubForm)
                    {
                        rows.Add(new AutoEditor.EditRow(memberInfo, memberType)
                        {
                            DisplayText = AutoEditor.DisplayName(memberInfo),
                            DisplayOrder = memberOrder.order,
                            GroupName = memberOrder.name,
                            NestedSource = isNested ? source : null,
                            ParentDisplayOrder = parentOrder,
                            HostGroupName = hostGroupName,
                            OpenArraySubForm = true,
                        });
                        continue;
                    }

                    // Inline mode
                    if (container is IList list)
                    {
                        m_ArrayLengths[(memberInfo, source)] = list.Count;
                        bool elementIsScalar = AutoEditor.IsScalarElementType(elementType);
                        for (int i = 0; i < list.Count; i++)
                        {
                            rows.Add(new AutoEditor.EditRow(memberInfo, elementType)
                            {
                                DisplayText = string.Format(arrayAttr.ItemLabelFormat, i),
                                ObjectIndex = i,
                                // Children of inline arrays share the host's slot. Top-level arrays sort
                                // by element order (0..N) inside the host's group: use memberOrder for primary,
                                // and the element index for secondary so labels still group correctly.
                                DisplayOrder = elementIsScalar ? memberOrder.order : memberOrder.order,
                                GroupName = i == 0 ? memberOrder.name : null,
                                NestedSource = isNested ? source : null,
                                ParentDisplayOrder = parentOrder,
                                HostGroupName = hostGroupName,
                                OpenElementSubForm = !elementIsScalar,
                            });
                        }
                    }
                    continue;
                }

                // ordinary scalar / subeditor / pushbutton / enum / bool row
                rows.Add(new AutoEditor.EditRow(memberInfo, memberType)
                {
                    DisplayText = AutoEditor.DisplayName(memberInfo),
                    ObjectIndex = null,
                    DisplayOrder = memberOrder.order,
                    GroupName = memberOrder.name,
                    NestedSource = isNested ? source : null,
                    ParentDisplayOrder = parentOrder,
                    HostGroupName = hostGroupName,
                });
            }
        }

        // Walk current source and detect any IList/ValueListEntry length that no longer matches what we cached.
        // Returns true if a full rebuild is needed.
        private bool ArrayLengthsChanged()
        {
            if (SourceData == null || m_ArrayLengths.Count == 0) return false;
            foreach (var kv in m_ArrayLengths)
            {
                var (member, src) = kv.Key;
                object? container = member is FieldInfo fi ? fi.GetValue(src)
                                 : member is PropertyInfo pi ? pi.GetValue(src, null) : null;
                int count = container is IList list ? list.Count
                          : container is List<AutoEditor.ValueListEntry> vle ? vle.Count
                          : -1;
                if (count != kv.Value) return true;
            }
            return false;
        }

        private void OnUpdateControlsRequested()
        {
            if (ArrayLengthsChanged())
            {
                this.BeginInvokeIfRequired(() => Generate(SourceData));
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

        private static Panel MakeKickPanel(TextBox textBox, AutoEditor.EditRow row, AutoEditor.RangeAttribute range)
        {
            const int buttonWidth = 24;
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = Math.Max(textBox.PreferredHeight, 23),
                Tag = row,
            };
            Button down = new Button { Text = "-", Width = buttonWidth, Dock = DockStyle.Left, TabStop = false };
            Button up = new Button { Text = "+", Width = buttonWidth, Dock = DockStyle.Right, TabStop = false };
            down.Enabled = textBox.Enabled;
            up.Enabled = textBox.Enabled;

            void kick(double direction)
            {
                double value;
                if (!double.TryParse(textBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out value))
                {
                    value = range.Min;
                }
                value = Math.Max(range.Min, Math.Min(range.Max, value + direction * range.Step));
                string text = IsIntegerType(row.Type) ? ((long)Math.Round(value)).ToString() : value.ToString("G15");
                if (textBox.Text != text)
                {
                    textBox.Text = text;
                }
            }
            down.Click += (s, e) => kick(-1);
            up.Click += (s, e) => kick(+1);

            panel.Controls.Add(textBox);
            panel.Controls.Add(up);
            panel.Controls.Add(down);
            return panel;
        }

        private static bool IsIntegerType(Type t)
        {
            return t == typeof(byte) || t == typeof(sbyte)
                || t == typeof(short) || t == typeof(ushort)
                || t == typeof(int) || t == typeof(uint)
                || t == typeof(long) || t == typeof(ulong);
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
                if (row.OpenArraySubForm)
                {
                    var arrayAttr = AutoEditor.ArrayEditor(row.MemberInfo);
                    Button button = new Button
                    {
                        Dock = DockStyle.Top,
                        AutoSize = true,
                        Tag = row,
                        Text = arrayAttr?.ButtonCaption ?? "..."
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (row.OpenElementSubForm)
                {
                    Button button = new Button
                    {
                        Dock = DockStyle.Top,
                        AutoSize = true,
                        Tag = row,
                        Text = "..."
                    };
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    tableLayout.Controls.Add(button, 1, tableLayout.RowCount);
                    button.Enabled = AutoEditor.IsEnabled(row.MemberInfo);
                }
                else if (AutoEditor.IsSubEditor(row.MemberInfo))
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
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        Tag = row,
                    };
                    if (AutoEditor.IsPassword(row.MemberInfo)) { textBox.PasswordChar = '*'; }
                    textBox.Enabled = AutoEditor.IsEnabled(row.MemberInfo);

                    AutoEditor.RangeAttribute? range = (row.Type == typeof(string)) ? null : AutoEditor.Range(row.MemberInfo);
                    tableLayout.Controls.Add(control, 0, ++tableLayout.RowCount);
                    if (range != null)
                    {
                        tableLayout.Controls.Add(MakeKickPanel(textBox, row, range), 1, tableLayout.RowCount);
                    }
                    else
                    {
                        tableLayout.Controls.Add(textBox, 1, tableLayout.RowCount);
                    }
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
