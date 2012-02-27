using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DocExp.Interfaces;
using Microsoft.Office.Interop.Excel;

namespace DocExp.PreviewControls
{
    public class ExcelPreview : WebBrowser, IPreview
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
                Workbook doc = ac.Workbooks.Open(fileName, m,  readOnly,
                     m,  m,  m,  m,  m,  m,  m,
                     m,  m,  m,  m,  m);
                // Create a temp file to save the HTML file to.           
                string tempFileName = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache) + g.ToString() + ".html";
                // Cast these items to object.  The methods we're calling  
                // only take object types in their method parameters.      
                object newFileName = (object)tempFileName;
                // We will be saving this file as HTML format.              
                object fileType = (object)Microsoft.Office.Interop.Excel.XlFileFormat.xlHtml;
                // Save the file.                 
                doc.SaveAs( newFileName,  fileType,
                     m,  m,  m,  m,XlSaveAsAccessMode.xlExclusive , m,  m,  m,
                     m,  m);
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
