
using SehensWerte.Controls;

namespace SehensWerte
{
    partial class TestForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Log = new LogControl();
            Split = new SehensWerte.Controls.SplitContainer();
            Scope = new SehensControl();
            ButtonGenerate = new Button();
            AutoEdit = new AutoEditorControl();
            ((System.ComponentModel.ISupportInitialize)Split).BeginInit();
            Split.Panel1.SuspendLayout();
            Split.Panel2.SuspendLayout();
            Split.SuspendLayout();
            SuspendLayout();
            // 
            // Log
            // 
            Log.BackColor = Color.Transparent;
            Log.CompressLogFile = false;
            Log.Dock = DockStyle.Bottom;
            Log.FilterString = "";
            Log.FilterType = Files.CsvLog.Priority.Info;
            Log.ItemLimit = 5000;
            Log.Location = new Point(0, 537);
            Log.LogFolder = "";
            Log.Margin = new Padding(4, 5, 4, 5);
            Log.Name = "Log";
            Log.ScrollIndex = 0;
            Log.Size = new Size(1259, 151);
            Log.TabIndex = 0;
            Log.TextBackColor = Color.White;
            // 
            // Split
            // 
            Split.Collapsed = false;
            Split.CollapsingPanel = SehensWerte.Controls.SplitContainer.ControlledPanel.Panel1;
            Split.Dock = DockStyle.Fill;
            Split.FixedPanel = FixedPanel.Panel1;
            Split.Location = new Point(0, 0);
            Split.Margin = new Padding(2);
            Split.Name = "Split";
            // 
            // Split.Panel1
            // 
            Split.Panel1.Controls.Add(Scope);
            Split.Panel1MinSize = 0;
            // 
            // Split.Panel2
            // 
            Split.Panel2.AutoScroll = true;
            Split.Panel2.Controls.Add(AutoEdit);
            Split.Panel2.Controls.Add(ButtonGenerate);
            Split.Size = new Size(1259, 537);
            Split.SplitterDistance = 910;
            Split.SplitterWidth = 3;
            Split.TabIndex = 4;
            // 
            // Scope
            // 
            Scope.BackgroundColour = Color.White;
            Scope.CursorMode = SehensWerte.Controls.Sehens.Skin.Cursors.CrossHair;
            Scope.Dock = DockStyle.Fill;
            Scope.ForegroundColour = Color.Black;
            Scope.HighQualityRender = true;
            Scope.HoverLabelColour = Color.FromArgb(128, 255, 255, 0);
            Scope.Location = new Point(0, 0);
            Scope.Margin = new Padding(2);
            Scope.Name = "Scope";
            Scope.PaintBoxRateLimitedRefresh = true;
            Scope.PaintBoxShowStats = false;
            Scope.PanValue = 0D;
            Scope.ScopeBoxZoomPanBarsVisible = true;
            Scope.ShowAxisLabels = true;
            Scope.ShowHoverInfo = true;
            Scope.ShowHoverValue = false;
            Scope.ShowTraceContextLabels = true;
            Scope.ShowTraceFeatures = true;
            Scope.ShowTraceLabels = SehensWerte.Controls.Sehens.Skin.TraceLabels.Embedded;
            Scope.ShowTraceStatistics = SehensWerte.Controls.Sehens.Skin.TraceStatistics.None;
            Scope.SimpleUi = false;
            Scope.Size = new Size(910, 537);
            Scope.StopUpdates = false;
            Scope.TabIndex = 3;
            Scope.TraceAutoRange = true;
            Scope.TraceListVisible = true;
            Scope.ZoomValue = 1D;
            // 
            // ButtonGenerate
            // 
            ButtonGenerate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonGenerate.Location = new Point(8, 509);
            ButtonGenerate.Margin = new Padding(2);
            ButtonGenerate.Name = "ButtonGenerate";
            ButtonGenerate.Size = new Size(78, 20);
            ButtonGenerate.TabIndex = 12;
            ButtonGenerate.Text = "Generate";
            ButtonGenerate.UseVisualStyleBackColor = true;
            ButtonGenerate.Click += ButtonGenerate_Click;
            // 
            // AutoEdit
            // 
            AutoEdit.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            AutoEdit.Location = new Point(8, 12);
            AutoEdit.Name = "AutoEdit";
            AutoEdit.Size = new Size(326, 492);
            AutoEdit.TabIndex = 13;
            // 
            // TestForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1259, 688);
            Controls.Add(Split);
            Controls.Add(Log);
            Margin = new Padding(4);
            Name = "TestForm";
            Text = "SehensWerte";
            Split.Panel1.ResumeLayout(false);
            Split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)Split).EndInit();
            Split.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private LogControl Log;
        private SehensWerte.Controls.SplitContainer Split;
        private SehensControl Scope;
        private Button ButtonGenerate;
        private AutoEditorControl AutoEdit;
    }
}
