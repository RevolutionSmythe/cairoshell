using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Office.Interop.PowerPoint;
using System.Windows.Forms;

namespace PreviewControls
{
    public class PowerPointPreview:WebBrowser, IPreviewControl
    {
        #region IPreview Members

        public void Preview(string path)
        {
            Guid g = Guid.NewGuid();
            ConvertDocument(g, path);
            this.Url = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache) + g.ToString() + ".html");
        }

        #endregion

        void ConvertDocument(Guid g, string fileName)
        {
            object m = System.Reflection.Missing.Value;
            object oldFileName = (object)fileName;
            object readOnly = (object)false;
            ApplicationClass ac = null;
            System.Globalization.CultureInfo oldCI = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                // First, create a new Microsoft.Office.Interop.Word.ApplicationClass.    
                ac = new ApplicationClass();

                // Now we open the document.          
                Presentation doc = ac.Presentations.Open(fileName , Microsoft.Office.Core.MsoTriState.msoFalse, Microsoft.Office.Core.MsoTriState.msoFalse, Microsoft.Office.Core.MsoTriState.msoFalse);
                     
                // Create a temp file to save the HTML file to.           
                string tempFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), g.ToString() + ".html");
                // Cast these items to object.  The methods we're calling  
                // only take object types in their method parameters.      
                        
                
                // Save the file.                 
                doc.SaveAs(tempFileName, Microsoft.Office.Interop.PowerPoint.PpSaveAsFileType.ppSaveAsHTML, Microsoft.Office.Core.MsoTriState.msoFalse);
                     
            }
            finally
            {
                // Make sure we close the application class.      
                if (ac != null)
                    ac.Quit();
                System.Threading.Thread.CurrentThread.CurrentCulture = oldCI;
            }
        }
    }
}
