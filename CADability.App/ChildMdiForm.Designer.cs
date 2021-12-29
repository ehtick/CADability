namespace CADability.App
{
    partial class ChildMdiForm
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
            this.cadControl1 = new CADability.Forms.CadControl();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // cadControl1
            // 
            this.cadControl1.CreateMainMenu = false;
            this.cadControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cadControl1.Location = new System.Drawing.Point(0, 0);
            this.cadControl1.Name = "cadControl1";
            this.cadControl1.ProgressAction = null;
            this.cadControl1.PropertiesExplorerVisible = true;
            this.cadControl1.Size = new System.Drawing.Size(800, 450);
            this.cadControl1.TabIndex = 0;
            this.cadControl1.ToolbarsVisible = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(296, 256);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // ChildMdiForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.cadControl1);
            this.KeyPreview = true;
            this.Name = "ChildMdiForm";
            this.Text = "ChildMdiForm";
            this.Load += new System.EventHandler(this.ChildMdiForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private Forms.CadControl cadControl1;
        private System.Windows.Forms.Button button1;
    }
}