namespace Xplorer
{
    partial class Autoupdate
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
            this.label1 = new System.Windows.Forms.Label();
            this.labelVersion = new System.Windows.Forms.Label();
            this.labelCurrVer = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxChanges = new System.Windows.Forms.TextBox();
            this.buttonClose = new System.Windows.Forms.Button();
            this.buttonInstall = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(177, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "A new version of Xplore is available:";
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(196, 13);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(52, 13);
            this.labelVersion.TabIndex = 1;
            this.labelVersion.Text = "Xplore v2";
            // 
            // labelCurrVer
            // 
            this.labelCurrVer.AutoSize = true;
            this.labelCurrVer.Location = new System.Drawing.Point(110, 35);
            this.labelCurrVer.Name = "labelCurrVer";
            this.labelCurrVer.Size = new System.Drawing.Size(52, 13);
            this.labelCurrVer.TabIndex = 1;
            this.labelCurrVer.Text = "Xplore v1";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Current version is:";
            // 
            // textBoxChanges
            // 
            this.textBoxChanges.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.textBoxChanges.Location = new System.Drawing.Point(12, 54);
            this.textBoxChanges.Multiline = true;
            this.textBoxChanges.Name = "textBoxChanges";
            this.textBoxChanges.ReadOnly = true;
            this.textBoxChanges.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxChanges.Size = new System.Drawing.Size(613, 259);
            this.textBoxChanges.TabIndex = 2;
            this.textBoxChanges.TabStop = false;
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(548, 319);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(75, 23);
            this.buttonClose.TabIndex = 3;
            this.buttonClose.Text = "&Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // buttonInstall
            // 
            this.buttonInstall.Location = new System.Drawing.Point(429, 319);
            this.buttonInstall.Name = "buttonInstall";
            this.buttonInstall.Size = new System.Drawing.Size(113, 23);
            this.buttonInstall.TabIndex = 3;
            this.buttonInstall.Text = "&Install New Version";
            this.buttonInstall.UseVisualStyleBackColor = true;
            this.buttonInstall.Click += new System.EventHandler(this.buttonInstall_Click);
            // 
            // Autoupdate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(635, 351);
            this.Controls.Add(this.buttonInstall);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.textBoxChanges);
            this.Controls.Add(this.labelCurrVer);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Autoupdate";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Check for Updates";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.Label labelCurrVer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxChanges;
        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.Button buttonInstall;
    }
}