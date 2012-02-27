using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocExp.Interfaces;
using WMPLib;

namespace DocExp.PreviewControls
{
    public class MediaPreview:AxWMPLib.AxWindowsMediaPlayer,IPreview
    {
        #region IPreview Members

        public void Preview(string path)
        {
            IWMPMedia media = this.newMedia(path);
            this.currentPlaylist.clear();
            this.currentPlaylist.appendItem(media);
            this.Ctlcontrols.play();
        }

        #endregion
    }
}
