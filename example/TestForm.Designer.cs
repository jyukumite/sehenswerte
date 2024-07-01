
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
            this.Log = new SehensWerte.Controls.LogControl();
            this.Split = new SehensWerte.Controls.SplitContainer();
            this.Scope = new SehensWerte.Controls.SehensControl();
            this.label12 = new System.Windows.Forms.Label();
            this.ToneWaveform = new System.Windows.Forms.ComboBox();
            this.ToneUseSin = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.NoisePan = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.Samples = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.SamplesPerSecond = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.ButtonGenerate = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.TonePan = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.ToneTwist = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ToneFrequency = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.ToneAmplitude = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.NoiseAmplitude = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.Split)).BeginInit();
            this.Split.Panel1.SuspendLayout();
            this.Split.Panel2.SuspendLayout();
            this.Split.SuspendLayout();
            this.SuspendLayout();
            // 
            // Log
            // 
            this.Log.BackColor = System.Drawing.Color.Transparent;
            this.Log.CompressLogFile = false;
            this.Log.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.Log.FilterString = "";
            this.Log.FilterType = SehensWerte.Files.CsvLog.Priority.Info;
            this.Log.ItemLimit = 5000;
            this.Log.Location = new System.Drawing.Point(0, 894);
            this.Log.LogFolder = "";
            this.Log.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Log.Name = "Log";
            this.Log.ScrollIndex = 0;
            this.Log.Size = new System.Drawing.Size(1798, 252);
            this.Log.TabIndex = 0;
            this.Log.TextBackColor = System.Drawing.Color.White;
            // 
            // Split
            // 
            this.Split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Split.CollapsingPanel = SehensWerte.Controls.SplitContainer.ControlledPanel.Panel1;
            this.Split.Location = new System.Drawing.Point(0, 0);
            this.Split.Name = "Split";
            // 
            // Split.Panel1
            // 
            this.Split.Panel1.Controls.Add(this.Scope);
            // 
            // Split.Panel2
            // 
            this.Split.Panel2.AutoScroll = true;
            this.Split.Panel2.Controls.Add(this.label12);
            this.Split.Panel2.Controls.Add(this.ToneWaveform);
            this.Split.Panel2.Controls.Add(this.ToneUseSin);
            this.Split.Panel2.Controls.Add(this.label11);
            this.Split.Panel2.Controls.Add(this.NoisePan);
            this.Split.Panel2.Controls.Add(this.label10);
            this.Split.Panel2.Controls.Add(this.Samples);
            this.Split.Panel2.Controls.Add(this.label8);
            this.Split.Panel2.Controls.Add(this.SamplesPerSecond);
            this.Split.Panel2.Controls.Add(this.label9);
            this.Split.Panel2.Controls.Add(this.ButtonGenerate);
            this.Split.Panel2.Controls.Add(this.label6);
            this.Split.Panel2.Controls.Add(this.TonePan);
            this.Split.Panel2.Controls.Add(this.label7);
            this.Split.Panel2.Controls.Add(this.ToneTwist);
            this.Split.Panel2.Controls.Add(this.label5);
            this.Split.Panel2.Controls.Add(this.ToneFrequency);
            this.Split.Panel2.Controls.Add(this.label3);
            this.Split.Panel2.Controls.Add(this.ToneAmplitude);
            this.Split.Panel2.Controls.Add(this.label4);
            this.Split.Panel2.Controls.Add(this.label2);
            this.Split.Panel2.Controls.Add(this.NoiseAmplitude);
            this.Split.Panel2.Controls.Add(this.label1);
            this.Split.Size = new System.Drawing.Size(1798, 894);
            this.Split.SplitterDistance = 1301;
            this.Split.TabIndex = 4;
            // 
            // Scope
            // 
            this.Scope.BackgroundColour = System.Drawing.Color.White;
            this.Scope.CursorMode = SehensWerte.Controls.Sehens.Skin.Cursors.CrossHair;
            this.Scope.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Scope.ForegroundColour = System.Drawing.Color.Black;
            this.Scope.HoverLabelColour = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(0)))));
            this.Scope.Location = new System.Drawing.Point(0, 0);
            this.Scope.Name = "Scope";
            this.Scope.PaintBoxRateLimitedRefresh = true;
            this.Scope.PaintBoxShowStats = false;
            this.Scope.PanValue = 0D;
            this.Scope.ScopeBoxZoomPanBarsVisible = true;
            this.Scope.ShowAxisLabels = true;
            this.Scope.ShowHoverInfo = true;
            this.Scope.ShowHoverValue = false;
            this.Scope.ShowTraceContextLabels = true;
            this.Scope.ShowTraceFeatures = true;
            this.Scope.SimpleUi = false;
            this.Scope.Size = new System.Drawing.Size(1301, 894);
            this.Scope.StopUpdates = false;
            this.Scope.TabIndex = 3;
            this.Scope.TraceAutoRange = true;
            this.Scope.TraceListVisible = true;
            this.Scope.ZoomValue = 1D;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(113, 327);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(94, 25);
            this.label12.TabIndex = 22;
            this.label12.Text = "Waveform";
            // 
            // ToneWaveform
            // 
            this.ToneWaveform.FormattingEnabled = true;
            this.ToneWaveform.Location = new System.Drawing.Point(113, 360);
            this.ToneWaveform.Name = "ToneWaveform";
            this.ToneWaveform.Size = new System.Drawing.Size(182, 33);
            this.ToneWaveform.TabIndex = 21;
            // 
            // ToneUseSin
            // 
            this.ToneUseSin.AutoSize = true;
            this.ToneUseSin.Location = new System.Drawing.Point(341, 291);
            this.ToneUseSin.Name = "ToneUseSin";
            this.ToneUseSin.Size = new System.Drawing.Size(141, 29);
            this.ToneUseSin.TabIndex = 20;
            this.ToneUseSin.Text = "Use Math.Sin";
            this.ToneUseSin.UseVisualStyleBackColor = true;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(227, 108);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(40, 25);
            this.label11.TabIndex = 19;
            this.label11.Text = "Pan";
            // 
            // NoisePan
            // 
            this.NoisePan.Location = new System.Drawing.Point(227, 139);
            this.NoisePan.Name = "NoisePan";
            this.NoisePan.Size = new System.Drawing.Size(108, 31);
            this.NoisePan.TabIndex = 18;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(227, 32);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(79, 25);
            this.label10.TabIndex = 17;
            this.label10.Text = "Samples";
            // 
            // Samples
            // 
            this.Samples.Location = new System.Drawing.Point(227, 63);
            this.Samples.Name = "Samples";
            this.Samples.Size = new System.Drawing.Size(108, 31);
            this.Samples.TabIndex = 16;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(113, 32);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(162, 25);
            this.label8.TabIndex = 15;
            this.label8.Text = "SamplesPerSecond";
            // 
            // SamplesPerSecond
            // 
            this.SamplesPerSecond.Location = new System.Drawing.Point(113, 63);
            this.SamplesPerSecond.Name = "SamplesPerSecond";
            this.SamplesPerSecond.Size = new System.Drawing.Size(108, 31);
            this.SamplesPerSecond.TabIndex = 14;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(23, 32);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(89, 25);
            this.label9.TabIndex = 13;
            this.label9.Text = "Dynamics";
            // 
            // ButtonGenerate
            // 
            this.ButtonGenerate.Location = new System.Drawing.Point(92, 492);
            this.ButtonGenerate.Name = "ButtonGenerate";
            this.ButtonGenerate.Size = new System.Drawing.Size(112, 34);
            this.ButtonGenerate.TabIndex = 12;
            this.ButtonGenerate.Text = "Generate";
            this.ButtonGenerate.UseVisualStyleBackColor = true;
            this.ButtonGenerate.Click += new System.EventHandler(this.ButtonGenerate_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(227, 258);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(40, 25);
            this.label6.TabIndex = 11;
            this.label6.Text = "Pan";
            // 
            // TonePan
            // 
            this.TonePan.Location = new System.Drawing.Point(227, 289);
            this.TonePan.Name = "TonePan";
            this.TonePan.Size = new System.Drawing.Size(108, 31);
            this.TonePan.TabIndex = 10;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(113, 258);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(51, 25);
            this.label7.TabIndex = 9;
            this.label7.Text = "Twist";
            // 
            // ToneTwist
            // 
            this.ToneTwist.Location = new System.Drawing.Point(113, 289);
            this.ToneTwist.Name = "ToneTwist";
            this.ToneTwist.Size = new System.Drawing.Size(108, 31);
            this.ToneTwist.TabIndex = 8;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(227, 188);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(93, 25);
            this.label5.TabIndex = 7;
            this.label5.Text = "Frequency";
            // 
            // ToneFrequency
            // 
            this.ToneFrequency.Location = new System.Drawing.Point(227, 219);
            this.ToneFrequency.Name = "ToneFrequency";
            this.ToneFrequency.Size = new System.Drawing.Size(108, 31);
            this.ToneFrequency.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(113, 188);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(95, 25);
            this.label3.TabIndex = 5;
            this.label3.Text = "Amplitude";
            // 
            // ToneAmplitude
            // 
            this.ToneAmplitude.Location = new System.Drawing.Point(113, 219);
            this.ToneAmplitude.Name = "ToneAmplitude";
            this.ToneAmplitude.Size = new System.Drawing.Size(108, 31);
            this.ToneAmplitude.TabIndex = 4;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(23, 188);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(49, 25);
            this.label4.TabIndex = 3;
            this.label4.Text = "Tone";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(113, 108);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 25);
            this.label2.TabIndex = 2;
            this.label2.Text = "Amplitude";
            // 
            // NoiseAmplitude
            // 
            this.NoiseAmplitude.Location = new System.Drawing.Point(113, 139);
            this.NoiseAmplitude.Name = "NoiseAmplitude";
            this.NoiseAmplitude.Size = new System.Drawing.Size(108, 31);
            this.NoiseAmplitude.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(23, 108);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Noise";
            // 
            // TestForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1798, 1146);
            this.Controls.Add(this.Split);
            this.Controls.Add(this.Log);
            this.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            this.Name = "TestForm";
            this.Text = "SehensWerte";
            this.Split.Panel1.ResumeLayout(false);
            this.Split.Panel2.ResumeLayout(false);
            this.Split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Split)).EndInit();
            this.Split.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private LogControl Log;
        private SehensWerte.Controls.SplitContainer Split;
        private SehensControl Scope;
        private Label label6;
        private TextBox TonePan;
        private Label label7;
        private TextBox ToneTwist;
        private Label label5;
        private TextBox ToneFrequency;
        private Label label3;
        private TextBox ToneAmplitude;
        private Label label4;
        private Label label2;
        private TextBox NoiseAmplitude;
        private Label label1;
        private Button ButtonGenerate;
        private Label label8;
        private TextBox SamplesPerSecond;
        private Label label9;
        private Label label10;
        private TextBox Samples;
        private Label label11;
        private TextBox NoisePan;
        private CheckBox ToneUseSin;
        private Label label12;
        private ComboBox ToneWaveform;
    }
}
