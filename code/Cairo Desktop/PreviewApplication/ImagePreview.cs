﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace PreviewControls
{
    public class ImagePreview:PictureBox,IPreviewControl
    {
        public ImagePreview()
        {
            this.SizeMode = PictureBoxSizeMode.Zoom;
        }
        #region IPreview Members

        public void Preview(string path)
        {
            this.Image = System.Drawing.Image.FromFile(path);
        }

        #endregion
    }
}
