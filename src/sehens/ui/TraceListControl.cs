using SehensWerte.Files;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms.VisualStyles;

namespace SehensWerte.Controls.Sehens
{
    public class TraceListControl : UserControl
    {
        private SehensControl Scope;
        private Action<CsvLog.Entry> OnLog;

        private Button ButtonAllOff;
        private Button ButtonAutoAll;
        private Button ButtonSave;
        private Button ButtonSort;
        private Button ButtonLoad;
        private Button ButtonMenu;

        private ToolTip ToolTip;

        private Panel PanelTop;
        private Label LabelFilterBy;
        private TextBox TextBoxFilter;
        private UserControl PaintBox;
        private VScrollBar VerticalScrollbar;

        private Font TextFont = new Font("Courier New", 8f);
        private Font WarningFont = new Font("Courier New", 10f);

        private TraceView[] AllTraces = new TraceView[0];
        private TraceView[] FilteredTraces = new TraceView[0];
        private TraceView[] DisplayedTraces = new TraceView[0];
        private int VisibleRows => PaintBox.Height / LineHeight;

        private int m_ScrollPosition;
        private bool m_ScrollbarUpdateRequired = true;
        private int m_UpdateSemaphore;
        private string m_LastToolTip;

        private const int LineHeight = 17;
        private const float TextLeft = 16f;

        public TraceListControl(SehensControl scope, Action<CsvLog.Entry> onLog)
        {
            OnLog = onLog;
            Scope = scope;
            Scope.OnViewListChanged += (s) => TraceListChanged();
            m_LastToolTip = "";

            ToolTip = new ToolTip();
            ButtonSave = new Button();
            ButtonLoad = new Button();
            ButtonAllOff = new Button();
            ButtonAutoAll = new Button();
            ButtonSort = new Button();
            LabelFilterBy = new Label();
            TextBoxFilter = new TextBox();
            ButtonMenu = new Button();
            PaintBox = new UserControl();
            VerticalScrollbar = new VScrollBar();
            PanelTop = new Panel();

            PanelTop.SuspendLayout();
            SuspendLayout();
            PanelTop.BorderStyle = BorderStyle.FixedSingle;
            PanelTop.Controls.Add(VerticalScrollbar);
            PanelTop.Controls.Add(PaintBox);
            PanelTop.Location = new Point(0, 0);
            PanelTop.Size = new Size(140, base.ClientSize.Width);
            PanelTop.TabIndex = 18;
            PaintBox.TabIndex = 14;
            PaintBox.TabStop = false;
            PaintBox.Click += PaintBox_Click;
            PaintBox.Paint += PaintBox_Paint;
            PaintBox.MouseMove += PaintBox_MouseMove;
            PaintBox.Resize += (o, e) =>
            {
                m_ScrollbarUpdateRequired = true;
                if (!base.IsHandleCreated) return;
                PaintBox.Invalidate();
            };
            PaintBox.MouseWheel += (o, e) =>
            {
                m_ScrollPosition -= e.Delta / 120 * SystemInformation.MouseWheelScrollLines;
                m_ScrollbarUpdateRequired = true;
                PaintBox.Invalidate();
            };
            VerticalScrollbar.Dock = DockStyle.Right;
            VerticalScrollbar.Location = new Point(118, 0);
            VerticalScrollbar.Size = new Size(20, 192);
            VerticalScrollbar.TabIndex = 15;
            VerticalScrollbar.Scroll += (o, e) =>
            {
                m_ScrollPosition = e.NewValue;
                if (!base.IsHandleCreated) return;
                this.BeginInvokeIfRequired(
                        () => PaintBox.Invalidate(),
                        (e) => OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception))
                    );
            };
            ButtonSave.Dock = DockStyle.Bottom;
            ButtonSave.Location = new Point(6, 225);
            ButtonSave.Size = new Size(131, 23);
            ButtonSave.TabIndex = 75;
            ButtonSave.Text = "Save";
            ButtonSave.UseVisualStyleBackColor = true;
            ButtonSave.Click += (o, e) => { ImportExport.SaveStateDialog(Scope); };
            ButtonLoad.Dock = DockStyle.Bottom;
            ButtonLoad.Location = new Point(6, 200);
            ButtonLoad.Size = new Size(131, 23);
            ButtonLoad.TabIndex = 80;
            ButtonLoad.Text = "Load";
            ButtonLoad.UseVisualStyleBackColor = true;
            ButtonLoad.Click += (o, e) => { ImportExport.LoadStateDialog(Scope); };
            ButtonAllOff.Dock = DockStyle.Bottom;
            ButtonAllOff.Location = new Point(65, 0);
            ButtonAllOff.Size = new Size(72, 23);
            ButtonAllOff.TabIndex = 73;
            ButtonAllOff.Text = "Switch Visible";
            ButtonAllOff.UseVisualStyleBackColor = true;
            ButtonAllOff.Click += (o, e) => { ButtonAllOnOff_Click(); };
            ButtonAutoAll.Dock = DockStyle.Bottom;
            ButtonAutoAll.Size = new Size(57, 23);
            ButtonAutoAll.TabIndex = 74;
            ButtonAutoAll.Text = "Auto All";
            ButtonAutoAll.UseVisualStyleBackColor = true;
            ButtonAutoAll.Click += (o, e) => { Scope.AutoRangeAll(); };
            ButtonSort.Dock = DockStyle.Bottom;
            ButtonSort.Location = new Point(65, 52);
            ButtonSort.Size = new Size(72, 23);
            ButtonSort.TabIndex = 77;
            ButtonSort.Text = "Sort";
            ButtonSort.UseVisualStyleBackColor = true;
            ButtonSort.Click += (o, e) => { Scope.SortViewGroups(); };
            LabelFilterBy.Dock = DockStyle.Bottom;
            LabelFilterBy.Location = new Point(3, 97);
            LabelFilterBy.Size = new Size(78, 13);
            LabelFilterBy.TabIndex = 78;
            LabelFilterBy.Text = "Filter by (regex)";
            TextBoxFilter.Dock = DockStyle.Bottom;
            TextBoxFilter.Location = new Point(3, 113);
            TextBoxFilter.Size = new Size(131, 20);
            TextBoxFilter.TabIndex = 79;
            TextBoxFilter.TextChanged += (o, e) =>
            {
                ButtonAllOff.Text = "Change Visible";
                TraceListChanged();
            };
            TextBoxFilter.KeyPress += (o, e) =>
            {
                if (e.KeyChar == '\r')
                {
                    ButtonAllOnOff_Click();
                }
            };
            ButtonMenu.Dock = DockStyle.Bottom;
            ButtonMenu.Location = new Point(6, 29);
            ButtonMenu.Size = new Size(57, 62);
            ButtonMenu.TabIndex = 81;
            ButtonMenu.Text = "Menu";
            ButtonMenu.UseVisualStyleBackColor = true;
            ButtonMenu.Click += (o, e) => { Scope.ShowContextMenu(); };

            base.Controls.Add(LabelFilterBy);
            base.Controls.Add(TextBoxFilter);
            base.Controls.Add(ButtonSort);
            base.Controls.Add(ButtonAutoAll);
            base.Controls.Add(ButtonAllOff);
            base.Controls.Add(ButtonMenu);
            base.Controls.Add(ButtonLoad);
            base.Controls.Add(ButtonSave);
            base.Controls.Add(PanelTop);

            base.AutoScaleDimensions = new SizeF(6f, 13f);
            base.AutoScaleMode = AutoScaleMode.Font;
            base.Size = new Size(143, 450);

            PanelTop.ResumeLayout(performLayout: false);
            ResumeLayout(performLayout: false);

            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(PaintBox, true, null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TextFont.Dispose();
                WarningFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private void TraceListChanged()
        {
            if (Interlocked.Exchange(ref m_UpdateSemaphore, 1) == 0 && base.IsHandleCreated)
            {
                this.BeginInvokeIfRequired(
                        () => PaintBox.Invalidate(),
                        (e) => OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception))
                    );
            }
        }

        private void RefilterTraceList()
        {
            m_UpdateSemaphore = 0;
            AllTraces = Scope.AllViews;
            string text = TextBoxFilter.Text;
            if (text.Length == 0)
            {
                FilteredTraces = AllTraces;
            }
            else
            {
                try
                {
                    Regex regex = new Regex(text, RegexOptions.IgnoreCase);
                    FilteredTraces = AllTraces.Where(x => regex.IsMatch(x.DecoratedName)).ToArray();
                }
                catch (Exception)
                {
                    FilteredTraces = AllTraces;
                }
            }
            m_ScrollbarUpdateRequired = true;
            if (base.IsHandleCreated)
            {
                this.BeginInvokeIfRequired(
                    () => PaintBox.Invalidate(),
                    (e) => OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception)));
            }
        }

        private void PaintBox_Paint(object? sender, PaintEventArgs e)
        {
            PanelTop.Height = LabelFilterBy.Top - 4;
            PanelTop.Width = base.ClientSize.Width;
            PaintBox.Width = VerticalScrollbar.Left;
            PaintBox.Height = PanelTop.ClientSize.Height;
            if (ButtonLoad.Visible != !Scope.SimpleUi)
            {
                ButtonLoad.Visible = !Scope.SimpleUi;
                ButtonSave.Visible = !Scope.SimpleUi;
                ButtonMenu.Visible = !Scope.SimpleUi;
            }
            if (m_UpdateSemaphore != 0)
            {
                RefilterTraceList();
            }
            if (m_ScrollbarUpdateRequired)
            {
                m_ScrollbarUpdateRequired = false;
                int length = FilteredTraces.Length;
                int overflow = length - VisibleRows;
                if (m_ScrollPosition > overflow) m_ScrollPosition = overflow;
                if (m_ScrollPosition < 0) m_ScrollPosition = 0;

                VerticalScrollbar.SuspendLayout();
                if (length > 0)
                {
                    VerticalScrollbar.Maximum = length - 1;
                    VerticalScrollbar.LargeChange = VisibleRows;
                    VerticalScrollbar.Value = ((m_ScrollPosition >= length) ? (length - 1) : m_ScrollPosition);
                }
                else
                {
                    VerticalScrollbar.Maximum = 0;
                    VerticalScrollbar.LargeChange = 1;
                    VerticalScrollbar.Value = 0;
                }
                VerticalScrollbar.ResumeLayout();
            }

            int width = PaintBox.Width;
            int height = PaintBox.Height;
            e.Graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height)); // overwritten by individual painters anyway
            DisplayedTraces = FilteredTraces.Skip(m_ScrollPosition).Take(VisibleRows).ToArray();

            PaintRows(e.Graphics, width, VisibleRows);
            PaintWarnings(e.Graphics, width, height, FilteredTraces.Length != AllTraces.Length);
        }

        private void PaintRows(Graphics graphics, int PaintBoxWidth, int count)
        {
            for (int loop = 0; loop < count && loop < DisplayedTraces.Length; loop++)
            {
                int top = loop * LineHeight;
                int bottom = top + LineHeight - 1;
                int mid = top + (LineHeight - TextFont.Height) / 2 - 1;
                if (DisplayedTraces[loop] != null)
                {
                    string decoratedName = DisplayedTraces[loop].DecoratedName;
                    if (DisplayedTraces[loop].Selected)
                    {
                        using var brush = new SolidBrush(Scope.ActiveSkin.SelectedContextColour);
                        graphics.FillRectangle(rect: new Rectangle(0, top, PaintBoxWidth, bottom - top), brush: brush);
                    }

                    using Pen light2 = new Pen(Color.FromKnownColor(KnownColor.ControlLightLight));
                    using Pen light1 = new Pen(Color.FromKnownColor(KnownColor.ControlLight));
                    using Pen dark1 = new Pen(Color.FromKnownColor(KnownColor.ControlDark));
                    using Pen dark2 = new Pen(Color.FromKnownColor(KnownColor.ControlDarkDark));
                    graphics.DrawLine(light2, 0, top, PaintBoxWidth, top);
                    graphics.DrawLine(light1, 0, top + 1, PaintBoxWidth, top + 1);
                    graphics.DrawLine(dark1, 0, bottom - 1, PaintBoxWidth, bottom - 1);
                    graphics.DrawLine(dark2, 0, bottom, PaintBoxWidth, bottom);

                    CheckBoxRenderer.DrawCheckBox(graphics, new Point(1, mid),
                        DisplayedTraces[loop].Visible ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
                    using SolidBrush traceColour = new SolidBrush(DisplayedTraces[loop].Colour);
                    graphics.DrawString(decoratedName, TextFont, traceColour, TextLeft, mid - 1);
                }
            }
        }

        private void PaintWarnings(Graphics graphics, int width, int height, bool filtered)
        {
            string text = (filtered ? " (Filter)" : "");
            if (text.Length > 0)
            {
                SizeF sizeF = graphics.MeasureString(text, WarningFont);
                using var pen = new Pen(Color.FromArgb(128, Scope.ActiveSkin.WarningFontColour));
                using var brush = new SolidBrush(pen.Color);
                graphics.DrawString(text, WarningFont, brush, width - (int)sizeF.Width - 3, height - (int)sizeF.Height - 3);
            }
        }

        private TraceView? ClickToTrace(EventArgs e)
        {
            int row = ((MouseEventArgs)e).Y / LineHeight;
            return DisplayedTraces == null || row < 0 || row >= DisplayedTraces.Length ? null : DisplayedTraces[row];
        }

        private void PaintBox_Click(object? sender, EventArgs e)
        {
            PaintBox.Focus();
            TraceView? traceView = ClickToTrace(e);
            if (traceView == null) return;

            MouseEventArgs args = (MouseEventArgs)e;
            if (args.X < TextLeft)
            {
                traceView.Visible = !traceView.Visible;
            }
            else
            {
                if (args.Button == MouseButtons.Left)
                {
                    traceView.Colour = Scope.ActiveSkin.ColourNext(traceView.Colour);
                }
                else if (args.Button == MouseButtons.Right)
                {
                    traceView.Colour = Scope.ActiveSkin.DefaultPenColour;
                }
                RefilterTraceList();
                Scope.Invalidate();
            }
        }

        private void PaintBox_MouseMove(object? sender, MouseEventArgs e)
        {
            TraceView? traceView = ClickToTrace(e);
            if (traceView == null)
            {
                m_LastToolTip = "";
                ToolTip.RemoveAll();
            }
            else
            {
                string decoratedName = traceView.DecoratedName;
                if (m_LastToolTip != decoratedName)
                {
                    m_LastToolTip = decoratedName;
                    ToolTip.SetToolTip(PaintBox, m_LastToolTip);
                }
            }
        }

        private void ButtonAllOnOff_Click()
        {
            TraceView[] array = (TextBoxFilter.Text.Length > 0) ? FilteredTraces : AllTraces;
            bool visible = array.Any(x => x.Visible);
            Scope.BeginUpdate();
            foreach (var f in array)
            {
                f.Visible = !visible;
            }
            Scope.EndUpdate();
        }
    }
}
