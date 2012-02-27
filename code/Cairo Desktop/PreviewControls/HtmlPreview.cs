using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DocExp.Interfaces;

namespace DocExp.PreviewControls
{
    public class HtmlPreview:WebBrowser,IPreview
    {
        #region IPreview Members

        public void Preview(string path)
        {
            this.Url= new Uri(path);
        }

        #endregion
    }
}
