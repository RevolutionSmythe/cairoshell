using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PreviewControls
{
    public class HtmlPreview:WebBrowser,IPreviewControl
    {
        #region IPreview Members

        public void Preview(string path)
        {
            this.Url= new Uri(path);
        }

        #endregion
    }
}
