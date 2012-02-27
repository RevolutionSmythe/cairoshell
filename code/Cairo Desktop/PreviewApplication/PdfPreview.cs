using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PreviewControls
{
    public class PdfPreview : AxAcroPDFLib.AxAcroPDF, IPreviewControl
    {
        #region IPreview Members

        public void Preview(string path)
        {
            this.src = path;
        }

        #endregion
    }
}
