using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocExp.Interfaces;
using Microsoft.Office.Interop.Word;
using System.Windows.Forms;

namespace DocExp.PreviewControls
{
    public class WordPreview : WebBrowser, IPreview
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
            try
            {
                // First, create a new Microsoft.Office.Interop.Word.ApplicationClass.    
                ac = new ApplicationClass();
                // Now we open the document.          
                Document doc = ac.Documents.Open(ref oldFileName, ref m, ref readOnly,
                    ref m, ref m, ref m, ref m, ref m, ref m, ref m,
                    ref m, ref m, ref m, ref m, ref m, ref m);
                // Create a temp file to save the HTML file to.           
                string tempFileName =Environment.GetFolderPath(Environment.SpecialFolder.InternetCache) + g.ToString() + ".html";
                // Cast these items to object.  The methods we're calling  
                // only take object types in their method parameters.      
                object newFileName = (object)tempFileName;
                // We will be saving this file as HTML format.              
                object fileType = (object)WdSaveFormat.wdFormatHTML;
                // Save the file.                 
                doc.SaveAs(ref newFileName, ref fileType,
                    ref m, ref m, ref m, ref m, ref m, ref m, ref m,
                    ref m, ref m, ref m, ref m, ref m, ref m, ref m);
            }
            finally
            {
                // Make sure we close the application class.      
                if (ac != null)
                    ac.Quit(ref readOnly, ref m, ref m);
            }
        }
    }
}
