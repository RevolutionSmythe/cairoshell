using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CairoDesktop.SupportingClasses
{
    /// <summary>
    /// Specifies keys that must be pressed in combination with the key specified.
    /// </summary>
    [Flags ()]
    public enum KeyModifiers
    {
        /// <summary>
        /// No Key.
        /// </summary>
        None = 0,
        /// <summary>
        /// Either ALT key must be held down.
        /// </summary>
        Alt = 1,
        /// <summary>
        /// Either CTRL key must be held down.
        /// </summary>
        Control = 2,
        /// <summary>
        /// Either SHIFT key must be held down.
        /// </summary>
        Shift = 4,
        /// <summary>
        /// Either WINDOWS key was held down. These keys are labeled with the Microsoft® Windows® logo.
        /// </summary>
        Windows = 8
    }

    public class KeyboardRegistration
    {
        /// <summary>
        /// Registers a hotkey
        /// </summary>
        /// <param name="hWnd">Handle to the window to receive WM_HOTKEY messages</param>
        /// <param name="id">The hotkey identifier returned by the GlobalAddAtom</param>
        /// <param name="fsModifiers">Combination to be checked along with the hotkey</param>
        /// <param name="vk">Virtual key code for the key</param>
        /// <returns>True if succeeded</returns>
        [DllImport ("user32.dll")]
        public static extern bool RegisterHotKey (IntPtr hWnd, int id, KeyModifiers fsModifiers, System.Windows.Forms.Keys vk);

        /// <summary>
        /// Unregisters a hotkey
        /// </summary>
        /// <param name="hWnd">Handle to the window which registered</param>
        /// <param name="id">Hotkey id</param>
        /// <returns>True if succeeded</returns>
        [DllImport ("user32.dll")]
        public static extern bool UnregisterHotKey (IntPtr hWnd, int id);
    }
}
