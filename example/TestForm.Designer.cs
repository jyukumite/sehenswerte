
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
            bool tabs = false;

            Log = new LogControl();
            Split = new SehensWerte.Controls.SplitContainer();
            Tabs = new System.Windows.Forms.TabControl();
            TabPageScope = new System.Windows.Forms.TabPage();
            Scope = new SehensControl();
            ButtonGenerate = new Button();
            AutoEdit = new AutoEditorControl();
            ((System.ComponentModel.ISupportInitialize)Split).BeginInit();
            Split.Panel1.SuspendLayout();
            Split.Panel2.SuspendLayout();
            Split.SuspendLayout();
            Tabs.SuspendLayout();
            TabPageScope.SuspendLayout();
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
            Log.Location = new Point(0, 894);
            Log.LogFolder = "";
            Log.Margin = new Padding(4, 5, 4, 5);
            Log.Name = "Log";
            Log.ScrollIndex = 0;
            Log.Size = new Size(1798, 252);
            Log.TabIndex = 0;
            Log.TextBackColor = Color.White;
            // 
            // Split
            // 
            Split.Dock = DockStyle.Fill;
            Split.CollapsingPanel = SehensWerte.Controls.SplitContainer.ControlledPanel.Panel2;
            Split.Location = new Point(0, 0);
            Split.Margin = new System.Windows.Forms.Padding(6);
            Split.Size = new Size(1798, 894);
            Split.Panel1MinSize = 300;
            //Split.Panel2MinSize = 100;
            Split.TabIndex = 4;
            Split.Name = "Split";
            Split.SplitterDistance = 1300;
            // 
            // Split.Panel1
            // 
            Split.Panel1.Controls.Add(tabs ? Tabs : Scope);
            // 
            // Split.Panel2
            // 
            Split.Panel2.AutoScroll = true;
            Split.Panel2.Controls.Add(AutoEdit);
            Split.Panel2.Controls.Add(ButtonGenerate);
            // 
            // Tabs
            // 
            if (tabs)
            {
                Tabs.Controls.Add(this.TabPageScope);
            }
            Tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            Tabs.Location = new System.Drawing.Point(0, 0);
            Tabs.Name = "Tabs";
            Tabs.SelectedIndex = 0;
            Tabs.Size = new System.Drawing.Size(1200, 557);
            Tabs.TabIndex = 2;
            // 
            // TabPageFliteScope
            // 
            if (tabs)
            {
                TabPageScope.Controls.Add(this.Scope);
            }
            TabPageScope.Location = new System.Drawing.Point(8, 46);
            TabPageScope.Name = "TabPageScope";
            TabPageScope.Size = new System.Drawing.Size(1184, 503);
            TabPageScope.TabIndex = 0;
            TabPageScope.Text = "Scope";
            TabPageScope.UseVisualStyleBackColor = true;
            // 
            // Scope
            // 
            Scope.BackgroundColour = Color.White;
            Scope.CursorMode = SehensWerte.Controls.Sehens.Skin.Cursors.CrossHair;
            Scope.Dock = DockStyle.Fill;
            Scope.ForegroundColour = Color.Black;

            Scope.HoverLabelColour = Color.FromArgb(128, 255, 255, 0);
            Scope.Location = new Point(0, 0);
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
            Scope.SimpleUi = false;
            Scope.Size = new Size(1301, 894);
            Scope.StopUpdates = false;
            Scope.TabIndex = 3;
            Scope.TraceAutoRange = true;
            Scope.TraceListVisible = true;
            Scope.ZoomValue = 1D;
            // 
            // AutoEdit
            // 
            AutoEdit.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            AutoEdit.Location = new Point(9, 12);
            AutoEdit.Name = "AutoEdit";
            AutoEdit.Size = new Size(470, 835);
            AutoEdit.TabIndex = 13;
            // 
            // ButtonGenerate
            // 
            ButtonGenerate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonGenerate.Location = new Point(8, 853);
            ButtonGenerate.Name = "ButtonGenerate";
            ButtonGenerate.Size = new Size(112, 34);
            ButtonGenerate.TabIndex = 12;
            ButtonGenerate.Text = "Generate";
            ButtonGenerate.UseVisualStyleBackColor = true;
            ButtonGenerate.Click += ButtonGenerate_Click;
            // 
            // TestForm
            // 
            //AutoScaleDimensions = new SizeF(7F, 15F);
            //AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1798, 1146);
            Controls.Add(Split);
            Controls.Add(Log);
            Margin = new Padding(5, 6, 5, 6);
            Name = "TestForm";
            Text = "SehensWerte";
            Split.Panel1.ResumeLayout(false);
            Split.Panel2.ResumeLayout(false);
            Tabs.ResumeLayout(false);
            TabPageScope.ResumeLayout(false);
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
        private System.Windows.Forms.TabControl Tabs;
        private System.Windows.Forms.TabPage TabPageScope;
    }
}
