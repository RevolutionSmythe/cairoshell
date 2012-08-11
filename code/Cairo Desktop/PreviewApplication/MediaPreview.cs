using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WMPLib;

namespace PreviewControls
{
    public class MediaPreview : AxWMPLib.AxWindowsMediaPlayer, IPreviewControl
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
