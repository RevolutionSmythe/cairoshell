using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace PreviewControls
{
    public class FileType
    {
        private Type _PreviewType;

        public Type PreviewType
        {
            get { return _PreviewType; }
            set { _PreviewType = value; }
        }
    }

    public interface IPreviewControl
    {
        void Preview(string path);
    }

    public interface IPreview
    {
        void Preview(object path, object caller, System.Windows.Point? pos);
        void ChangePreview(object path, object caller, System.Windows.Point? pos);
        void ForcePreviewOpen();
        void PreviewHide();

        void PreviewClose();
    }

    public class Preview
    {
        public static Dictionary<string, Type> AdditionalPaths = new Dictionary<string, Type>();
        public static object OpenPreview = null;
        public static void PreviewFile(string path, object file, object caller, System.Windows.Point? pos)
        {
            Type t = GetPreivewType(path);
            PreviewFile(path, file, t, caller, pos);
        }

        private static Type GetPreivewType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            Type t = null;
            switch (ext)
            {
                case ".pdf":
                    t = typeof(PdfPreview);
                    break;
                case ".mp3":
                case ".wav":
                case ".wma":
                case ".mid":
                case ".mpg":
                case ".avi":
                case ".wmv":
                    t = typeof(MediaPreview);
                    break;
                case ".html":
                case ".php":
                case ".htm":
                case ".xps":
                    t = typeof(HtmlPreview);
                    break;
                case ".txt":
                case ".rtf":
                case ".ini":
                case ".nfo":
                    t = typeof(TextPreview);
                    break;
                case ".docx":
                case ".doc":
                    t = typeof(WordPreview);
                    break;
                case ".xlsx":
                case ".xls":
                    t = typeof(ExcelPreview);
                    break;
                case ".ppt":
                case ".pptx":
                case ".ppsx":
                case ".pps":
                    t = typeof(PowerPointPreview);
                    break;
                case "":
                    //Folder!
                    //t = typeof(FolderPreview);
                    if (AdditionalPaths.ContainsKey(ext))
                        t = AdditionalPaths[ext];
                    else if (AdditionalPaths.ContainsKey("*"))
                        t = AdditionalPaths["*"];
                    break;
                default:
                    if (AdditionalPaths.ContainsKey(ext))
                        t = AdditionalPaths[ext];
                    else if (AdditionalPaths.ContainsKey("*"))
                        t = AdditionalPaths["*"];
                    break;
            }
            return t;
        }

        private static void PreviewFile(string path, object file, Type PreviewType, object caller, System.Windows.Point? pos)
        {
            System.Threading.Thread tt = new System.Threading.Thread(delegate()
                            {
                                try
            {
                if (file != null || File.Exists(path) || Directory.Exists(path))
                {
                    if (PreviewType == null)
                    {
                        MessageBox.Show("Preview not available");
                    }
                    else
                    {
                        Type t = PreviewType;
                        if (t.GetInterface("IPreviewControl") != null)
                        {
                            Form f = new Form();
                            Panel pnl = new Panel();
                            pnl.Dock = System.Windows.Forms.DockStyle.Fill;
                            pnl.Location = new System.Drawing.Point(0, 0);
                            pnl.Name = "pnlPreview";
                            pnl.Size = new System.Drawing.Size(96, 77);
                            pnl.TabIndex = 1;
                            f.Controls.Add(pnl);
                            if (pnl.Controls.Count > 0)
                            {
                                Control pOld = pnl.Controls[0];
                                pOld.Dispose();
                            }
                            pnl.Controls.Clear();
                            IPreviewControl p = (IPreviewControl)Activator.CreateInstance(t);
                            pnl.Controls.Add((Control)p);
                            ((Control)p).Dock = DockStyle.Fill;

                            f.Load += delegate(object sender, EventArgs e)
                            {
                                p.Preview(path);
                            };
                            f.StartPosition = FormStartPosition.CenterScreen;
                            f.ControlBox = false;
                            f.Text = "";
                            //f.SetDesktopLocation(
                            //f.FormBorderStyle = FormBorderStyle.None;
                            f.PreviewKeyDown += delegate(object sender, PreviewKeyDownEventArgs e)
                            {
                                if (e.KeyCode == Keys.Space)
                                    f.Close();
                            };
                            ((Control)p).PreviewKeyDown += delegate(object sender, PreviewKeyDownEventArgs e)
                            {
                                if (e.KeyCode == Keys.Space)
                                    f.Close();
                            };
                            f.FormClosing += delegate(object sender, FormClosingEventArgs e)
                            {
                                if(OpenPreview == f)
                                    OpenPreview = null;
                            };
                            OpenPreview = f;
                            Application.Run(f);
                        }
                        else
                        {
                            IPreview p = (IPreview)Activator.CreateInstance(t);
                            OpenPreview = p;
                            p.Preview(file, caller, pos);
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("An error occured while loading preview control");
            }
                            });
            tt.SetApartmentState(System.Threading.ApartmentState.STA);
            tt.Start();
        }

        public static void PreviewFileChanged(string path, object file, object caller, System.Windows.Point? pos)
        {
            if (OpenPreview is IPreview && GetPreivewType(path).GetInterface("IPreview") != null)
                ((IPreview)OpenPreview).ChangePreview(path, caller, pos);
            else if (OpenPreview != null)
            {
                PreviewClose();
                Preview.PreviewFile(path, file, caller, pos);
            }
        }

        public static void ForcePreviewOpen()
        {
            if (OpenPreview is IPreview)
                ((IPreview)OpenPreview).ForcePreviewOpen();
        }

        public static void PreviewHide()
        {
            if (OpenPreview is IPreview)
                ((IPreview)OpenPreview).PreviewHide();
            else if (OpenPreview != null && !((Form)OpenPreview).TopLevel)
                ((Form)OpenPreview).BeginInvoke(new Action(delegate() { ((Form)OpenPreview).Close(); }));
        }

        public static void PreviewClose()
        {
            if (OpenPreview is IPreview)
                ((IPreview)OpenPreview).PreviewClose();
            else if (OpenPreview != null)
                ((Form)OpenPreview).BeginInvoke(new Action(delegate() { ((Form)OpenPreview).Close(); }));
        }
    }
}
