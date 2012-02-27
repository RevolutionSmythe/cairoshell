using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocExp.Interfaces;
using System.Windows.Forms;
using System.IO;
using DocExp.Attributes;
using DocExp.Enums;

namespace DocExp.Actions
{
    [ActionAttributes(ActionName = "Preview", IsDefaultAction = true, ActionsGroupTypes = new GroupTypes[] { GroupTypes.File })]
    public class Preview : DocExp.AbstractClasses.Action
    {

        public override void DoAction(string path, FileType parFileType, frmMain frm)
        {
            System.Threading.Thread tt = new System.Threading.Thread(delegate()
                            {
                                try
            {
                if (FileExists(path))
                {
                    if (parFileType.PreviewType == null)
                    {
                        ShowError("Preview not available");
                    }
                    else
                    {
                        Type t = parFileType.PreviewType;
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
                        IPreview p = (IPreview)Activator.CreateInstance(t);
                        pnl.Controls.Add((Control)p);
                        ((Control)p).Dock = DockStyle.Fill;

                        f.Load += delegate(object sender, EventArgs e)
                        {
                            p.Preview(path);
                        };
                        Application.Run(f);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("An error occured while loading preview control");
            }
                            });
            tt.SetApartmentState(System.Threading.ApartmentState.STA);
            tt.Start();
        }
    }
}
