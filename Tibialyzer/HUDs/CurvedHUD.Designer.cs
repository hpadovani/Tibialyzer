﻿namespace Tibialyzer {
    partial class CurvedHUD {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.hpManaBar = new Tibialyzer.CurveBar();
            this.SuspendLayout();
            // 
            // hpManaBar
            // 
            this.hpManaBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hpManaBar.Location = new System.Drawing.Point(0, 0);
            this.hpManaBar.Name = "hpManaBar";
            this.hpManaBar.Size = new System.Drawing.Size(300, 300);
            this.hpManaBar.TabIndex = 0;
            // 
            // CurvedHUD
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 300);
            this.Controls.Add(this.hpManaBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Tibialyzer (Curved HUD)";
            this.Text = "Tibialyzer (Curved HUD)";
            this.ResumeLayout(false);

        }

        #endregion

        private CurveBar hpManaBar;
    }
}