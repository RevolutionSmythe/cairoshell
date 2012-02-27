using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CairoDesktop;
using CairoDesktop.SupportingClasses;
using System.Windows.Forms;
using CoreAudioApi;

namespace CairoDesktop.Sound
{
    public class SoundAPI
    {
        private const int VolumeDown = 250;
        private const int VolumeUp = 251;
        private const int VolumeMute = 252;
        private const int Windows = 253;
        private MMDevice m_device;
        private bool m_bUpdate = true;

        public void Initialize (IntPtr handle)
        {
            MMDeviceEnumerator devEnum = new MMDeviceEnumerator ();
            m_device = devEnum.GetDefaultAudioEndpoint (EDataFlow.eRender, ERole.eMultimedia);
            KeyboardRegistration.RegisterHotKey (handle, VolumeDown, KeyModifiers.None, Keys.VolumeDown);
            KeyboardRegistration.RegisterHotKey (handle, VolumeUp, KeyModifiers.None, Keys.VolumeUp);
            KeyboardRegistration.RegisterHotKey (handle, VolumeMute, KeyModifiers.None, Keys.VolumeMute);
        }

        public void HandleKeypress (int param)
        {
            float volume = m_device.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
            switch (param)
            {
                case VolumeDown:
                    if ((volume - 1) / 100 < 0)
                        volume = 1;
                    m_device.AudioEndpointVolume.MasterVolumeLevelScalar = (volume - 1) / 100;
                    break;
                case VolumeUp:
                    if ((volume + 1) / 100 > 100)
                        volume = 99;
                    m_device.AudioEndpointVolume.MasterVolumeLevelScalar = (volume + 1) / 100;
                    break;
                case VolumeMute:
                    m_device.AudioEndpointVolume.Mute = !m_device.AudioEndpointVolume.Mute;
                    break;
                case Windows:
                    break;
                default:
                    break;
            }
        }
    }
}
