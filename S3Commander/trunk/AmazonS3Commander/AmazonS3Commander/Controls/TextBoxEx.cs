﻿using System;
using System.Windows.Forms;

namespace AmazonS3Commander.Controls
{
    public class TextBoxEx : TextBox
    {
        private const int WM_PASTE = 0x0302;


        public event EventHandler Pasted;


        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE)
            {
                OnPasted();
            }
            base.WndProc(ref m);
        }

        protected virtual void OnPasted()
        {
            var pasted = Pasted;
            if (pasted != null) pasted(this, EventArgs.Empty);
        }
    }
}
