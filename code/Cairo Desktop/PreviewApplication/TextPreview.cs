using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace PreviewControls
{
    public class TextPreview:RichTextBox, IPreviewControl
    {
        public TextPreview()
        {
            this.Multiline = true;
            this.ReadOnly = true;
        }
        #region IPreview Members

        public void Preview(string path)
        {
            StreamReader sr = new StreamReader(path);
            string content=sr.ReadToEnd();
            this.Text = content;
        }

        #endregion
    }
}
