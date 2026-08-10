namespace DDF___Program_Language_Editor
{
    partial class MainForm
    {
        /// <summary>
        /// Variabile di progettazione necessaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Pulire le risorse in uso.
        /// </summary>
        /// <param name="disposing">ha valore true se le risorse gestite devono essere eliminate, false in caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Codice generato da Progettazione Windows Form

        /// <summary>
        /// Metodo necessario per il supporto della finestra di progettazione. Non modificare
        /// il contenuto del metodo con l'editor di codice.
        /// </summary>
        private void InitializeComponent()
        {
            this.richTextBoxMainEditor = new System.Windows.Forms.RichTextBox();
            this.richTextBoxLineNumbers = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // richTextBoxMainEditor
            // 
            this.richTextBoxMainEditor.AcceptsTab = true;
            this.richTextBoxMainEditor.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.richTextBoxMainEditor.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxMainEditor.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBoxMainEditor.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.richTextBoxMainEditor.Location = new System.Drawing.Point(59, 3);
            this.richTextBoxMainEditor.Name = "richTextBoxMainEditor";
            this.richTextBoxMainEditor.Size = new System.Drawing.Size(965, 787);
            this.richTextBoxMainEditor.TabIndex = 0;
            this.richTextBoxMainEditor.TabStop = false;
            this.richTextBoxMainEditor.Text = "";
            this.richTextBoxMainEditor.VScroll += new System.EventHandler(this.richTextBoxMainEditor_VScroll);
            this.richTextBoxMainEditor.FontChanged += new System.EventHandler(this.richTextBoxMainEditor_FontChanged);
            this.richTextBoxMainEditor.TextChanged += new System.EventHandler(this.richTextBox_TextChanged);
            this.richTextBoxMainEditor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.richTextBoxMainEditor_KeyDown);
            // 
            // richTextBoxLineNumbers
            // 
            this.richTextBoxLineNumbers.AcceptsTab = true;
            this.richTextBoxLineNumbers.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.richTextBoxLineNumbers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxLineNumbers.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBoxLineNumbers.ForeColor = System.Drawing.Color.Silver;
            this.richTextBoxLineNumbers.Location = new System.Drawing.Point(3, 3);
            this.richTextBoxLineNumbers.Name = "richTextBoxLineNumbers";
            this.richTextBoxLineNumbers.Size = new System.Drawing.Size(50, 787);
            this.richTextBoxLineNumbers.TabIndex = 1;
            this.richTextBoxLineNumbers.TabStop = false;
            this.richTextBoxLineNumbers.Text = "";
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.Controls.Add(this.richTextBoxLineNumbers);
            this.panel1.Controls.Add(this.richTextBoxMainEditor);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1027, 793);
            this.panel1.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1027, 793);
            this.Controls.Add(this.panel1);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DDF - Program Language Editor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBoxMainEditor;
        private System.Windows.Forms.RichTextBox richTextBoxLineNumbers;
        private System.Windows.Forms.Panel panel1;
    }
}

