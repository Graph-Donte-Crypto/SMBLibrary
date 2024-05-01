using System;
using System.Collections;
using System.Security.Principal;

namespace SMBServer
{
    partial class ServerUI : IDisposable
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        public void Dispose()
        {
            if (components != null)
            {
                components.Dispose();
            }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.chkSMB1 = new();
            this.chkSMB2 = new();

            this.chkSMB1.Checked = true;
            this.chkSMB1.OnCheckedChanged += new System.EventHandler(this.chkSMB1_CheckedChanged);

            this.chkSMB2.Checked = true;
            this.chkSMB2.OnCheckedChanged += new System.EventHandler(this.chkSMB2_CheckedChanged);
        }

        #endregion

        public CheckBox chkSMB1;
        public CheckBox chkSMB2;
    }

    public class ComboBox
    {
        public IList DataSource { get; set; }
        public int SelectedIndex { get; set; }
        public object SelectedValue => DataSource[SelectedIndex];
    }
    public class RadioButton
    {
        public bool Checked { get;set; }
    }

    public class CheckBox
    {
        private bool _Checked;
        public bool Checked { 
            get => _Checked; 
            set {
                _Checked = value;
                OnCheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OnCheckedChanged;
    }


}

