namespace Cairo.WindowsHooksWrapper
{
    using System.Runtime.InteropServices;
    using System;

    /// <summary>
    /// Notify icon data structure type
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class NOTIFYICONDATA
    {
        [MarshalAs(UnmanagedType.U4)]
        public uint cbSize;
        [MarshalAs(UnmanagedType.I4, SizeConst = 4)]
        public int hWnd;
        [MarshalAs(UnmanagedType.U4, SizeConst = 4)]
        public uint uID;
        [MarshalAs(UnmanagedType.U4, SizeConst = 4)]
        public uint uFlags;
        [MarshalAs(UnmanagedType.U4, SizeConst = 4)]
        public uint uCallbackMessage;
        [MarshalAs(UnmanagedType.I4, SizeConst = 4)]
        public int hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }
}
