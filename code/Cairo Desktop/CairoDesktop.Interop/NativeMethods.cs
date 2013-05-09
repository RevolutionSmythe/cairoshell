using System.Windows.Input;
namespace CairoDesktop.Interop
{
    using System;
    using System.Drawing;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Container class for Win32 Native methods used within the desktop application (e.g. shutdown, sleep, et al).
    /// </summary>
    public class NativeMethods
    {
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const uint TOKENADJUSTPRIVILEGES = 0x00000020;
        private const uint TOKENQUERY = 0x00000008;
        public const int HWND_TOPMOST = -1; // 0xffff 
        public const int HWND_BOTTOMMOST = 1;
        public const int SWP_NOSIZE = 1; // 0x0001  
        public const int SWP_NOMOVE = 2; // 0x0002  
        public const int SWP_NOZORDER = 4; // 0x0004  
        public const int SWP_NOACTIVATE = 16; // 0x0010  
        public const int SWP_SHOWWINDOW = 64; // 0x0040  
        public const int SWP_HIDEWINDOW = 128; // 0x0080  
        public const int SWP_DRAWFRAME = 32; // 0x0020 

        /// <summary>
        /// Calls the shutdown method on the Win32 API.
        /// </summary>
        public static void Shutdown()
        {
            AdjustTokenPrivilegesForShutdown();
            ExitWindowsEx((uint)(ExitWindows.Shutdown | ExitWindows.ForceIfHung), 0x0);
        }

        /// <summary>
        /// Calls the reboot method on the Win32 API.
        /// </summary>
        public static void Reboot()
        {
            AdjustTokenPrivilegesForShutdown();
            ExitWindowsEx((uint)(ExitWindows.Reboot | ExitWindows.ForceIfHung), 0x0);
        }

        /// <summary>
        /// Calls the logoff method on the Win32 API.
        /// </summary>
        public static void Logoff()
        {
            ExitWindowsEx((uint)ExitWindows.Logoff, 0x0);
        }

        /// <summary>
        /// Calls the Sleep method on the Win32 Power Profile API.
        /// </summary>
        public static void Sleep()
        {
            SetSuspendState(false, false, false);
        }

        public static void PostWindowsMessage(IntPtr hWnd, uint callback, uint uid, uint messageId)
        {
            PostMessage(hWnd, callback, uid, messageId);
        }

        public static IntPtr FindWindow(string className)
        {
            return FindWindow(className, string.Empty);
        }

        public static RECT GetWindowRectangle(IntPtr windowHandle)
        {
            RECT ret = new RECT();
            GetWindowRect(windowHandle, out ret);
            
            return ret;
        }

        // Handling the close splash screen event
        [DllImport("kernel32.dll")]
        public static extern Int32 OpenEvent(Int32 DesiredAccess, bool InheritHandle, string Name);

        // OpenEvent DesiredAccess defines
        public const int EVENT_MODIFY_STATE = 0x00000002;

        [DllImport("kernel32.dll")]
        public static extern Int32 SetEvent(Int32 Handle);

        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(Int32 Handle);

        [DllImport("user32.dll")]
        public static extern Int32 SetShellWindow(IntPtr hWnd);

        [DllImport("USER32.dll")]
        extern public static bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

        [DllImport("SHELL32", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("USER32")]
        public static extern int GetSystemMetrics(int Index);

        [DllImport("User32.dll", ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string msg);

        #region Private Methods 

        [DllImport("user32.dll")]
        private static extern uint SendMessageTimeout(IntPtr hWnd, uint messageId, uint wparam, uint lparam, uint timeoutFlags, uint timeout, ref uint retval);

        [DllImport("user32.dll")]
        private static extern uint GetClassLong(IntPtr handle, int longClass);

        [DllImport("user32.dll")]
        public static extern bool RegisterShellHookWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetTaskmanWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        public static extern bool DeregisterShellHookWindow(IntPtr hWnd);

        [DllImport("Shell32.dll")]
        public static extern bool RegisterShellHook(IntPtr hWnd, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, IntPtr pvParam, SPIF fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int RegisterClassEx(ref WNDCLASSEX pcWndClassEx);

        [DllImport("user32.dll")]
        public static extern bool UnregisterClass(string lpClassname, IntPtr hInstance);

        [DllImport("user32.dll")]
        public static extern IntPtr GetModuleHandle(string filename);

        [DllImport("user32.dll")]
        public static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hwndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lParam);

        /// <summary>
        /// Registers a hotkey
        /// </summary>
        /// <param name="hWnd">Handle to the window to receive WM_HOTKEY messages</param>
        /// <param name="id">The hotkey identifier returned by the GlobalAddAtom</param>
        /// <param name="fsModifiers">Combination to be checked along with the hotkey</param>
        /// <param name="vk">Virtual key code for the key</param>
        /// <returns>True if succeeded</returns>
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, KeyModifiers fsModifiers, System.Windows.Forms.Keys vk);

        /// <summary>
        /// Unregisters a hotkey
        /// </summary>
        /// <param name="hWnd">Handle to the window which registered</param>
        /// <param name="id">Hotkey id</param>
        /// <returns>True if succeeded</returns>
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static uint GetIconForWindow(IntPtr HWnd)
        {
            uint hIco = 0;
            SendMessageTimeout(HWnd, 127, 2, 0, 2, 200, ref hIco);
            int GCL_HICON = -14;
            if (hIco == 0)
            {
                hIco = GetClassLong(HWnd, GCL_HICON);
            }

            return hIco;
        }
       
        /// <summary>
        /// Adjusts the current process's token privileges to allow it to shut down or reboot the machine.
        /// Throws an ApplicationException if an error is encountered.
        /// </summary>
        private static void AdjustTokenPrivilegesForShutdown()
        {
            IntPtr procHandle = System.Diagnostics.Process.GetCurrentProcess().Handle;
            IntPtr tokenHandle = IntPtr.Zero;

            bool tokenOpenResult = OpenProcessToken(procHandle, TOKENADJUSTPRIVILEGES | TOKENQUERY, out tokenHandle);
            if (!tokenOpenResult)
            {
                throw new ApplicationException("Error attempting to open process token to raise level for shutdown.\nWin32 Error Code: " + Marshal.GetLastWin32Error());
            }

            long pluid = new long();
            bool privLookupResult = LookupPrivilegeValue(null, "SeShutdownPrivilege", ref pluid);
            if (!privLookupResult)
            {
                throw new ApplicationException("Error attempting to lookup value for shutdown privilege.\n Win32 Error Code: " + Marshal.GetLastWin32Error());
            }

            TOKEN_PRIVILEGES newPriv = new TOKEN_PRIVILEGES();
            newPriv.Luid = pluid;
            newPriv.PrivilegeCount = 1;
            newPriv.Attributes = 0x00000002;

            bool tokenPrivResult = AdjustTokenPrivileges(tokenHandle, false, ref newPriv, 0, IntPtr.Zero, IntPtr.Zero);
            if (!tokenPrivResult)
            {
                throw new ApplicationException("Error attempting to adjust the token privileges to allow shutdown.\n Win32 Error Code: " + Marshal.GetLastWin32Error());
            }
        }

        #region P/Invokes
        [DllImport("user32.dll")]
        private static extern bool ExitWindowsEx(uint flags, uint reason);

        // There is a method for this in System.Windows.Forms, however it calls the same p/invoke and I would prefer not to reference that lib
        [DllImport("powrprof.dll")]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint callback, uint wParam, uint lParam);

        [DllImport("user32.dll", SetLastError=true)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        #endregion
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public RECT(int left_, int top_, int right_, int bottom_)
            {
                left = left_;
                top = top_;
                right = right_;
                bottom = bottom_;
            }

            public int Height { get { return bottom - top; } }
            public int Width { get { return right - left; } }
            public Size Size { get { return new Size(Width, Height); } }

            public Point Location { get { return new Point(left, top); } }

            // Handy method for converting to a System.Drawing.Rectangle
            public Rectangle ToRectangle()
            { return Rectangle.FromLTRB(left, top, right, bottom); }

            public static RECT FromRectangle(Rectangle rectangle)
            {
                return new RECT(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            }

            public override int GetHashCode()
            {
                return left ^ ((top << 13) | (top >> 0x13))
                  ^ ((Width << 0x1a) | (Width >> 6))
                  ^ ((Height << 7) | (Height >> 0x19));
            }

            #region Operator overloads

            public static implicit operator Rectangle(RECT rect)
            {
                return rect.ToRectangle();
            }

            public static implicit operator RECT(Rectangle rect)
            {
                return FromRectangle(rect);
            }

            #endregion
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        public enum ABMsg : int
        {
            ABM_NEW = 0,
            ABM_REMOVE,
            ABM_QUERYPOS,
            ABM_SETPOS,
            ABM_GETSTATE,
            ABM_GETTASKBARPOS,
            ABM_ACTIVATE,
            ABM_GETAUTOHIDEBAR,
            ABM_SETAUTOHIDEBAR,
            ABM_WINDOWPOSCHANGED,
            ABM_SETSTATE
        }

        public enum ABNotify : int
        {
            ABN_STATECHANGE = 0,
            ABN_POSCHANGED,
            ABN_FULLSCREENAPP,
            ABN_WINDOWARRANGE
        }

        public enum ABEdge : int
        {
            ABE_LEFT = 0,
            ABE_TOP,
            ABE_RIGHT,
            ABE_BOTTOM
        }

        [Flags]
        public enum SPIF
        {
            None = 0x00,
            SPIF_UPDATEINIFILE = 0x01,  // Writes the new system-wide parameter setting to the user profile.
            SPIF_SENDCHANGE = 0x02,  // Broadcasts the WM_SETTINGCHANGE message after updating the user profile.
            SPIF_SENDWININICHANGE = 0x02   // Same as SPIF_SENDCHANGE.
        }

        public enum SPI : uint
        {
            SPI_GETMINIMIZEDMETRICS = 0x002B,
            SPI_SETMINIMIZEDMETRICS = 0x002C
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MinimizedMetrics
        {
            public uint cbSize;
            public int iWidth;
            public int iHorzGap;
            public int iVertGap;
            public MinimizedMetricsArrangement iArrange;
        }

        [Flags]
        public enum MinimizedMetricsArrangement
        {
            BottomLeft = 0,
            BottomRight = 1,
            TopLeft = 2,
            TopRight = 3,
            Left = 0,
            Right = 0,
            Up = 4,
            Down = 4,
            Hide = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHELLHOOKINFO
        {
            public IntPtr hwnd;
            public RECT rc;
        }

        /// <summary>
        /// Specifies keys that must be pressed in combination with the key specified.
        /// </summary>
        [Flags()]
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WNDCLASSEX
        {
            public int cbSize; // Size in bytes of the WNDCLASSEX structure
            public int style;	// Class style
            public WndProcDelegate lpfnWndProc;// Pointer to the classes Window Procedure
            public int cbClsExtra;// Number of extra bytes to allocate for class
            public int cbWndExtra;// Number of extra bytes to allocate for window
            public IntPtr hInstance;// Applications instance handle Class
            public IntPtr hIcon;// Handle to the classes icon
            public IntPtr hCursor;// Handle to the classes cursor
            public IntPtr hbrBackground;// Handle to the classes background brush
            public string lpszMenuName;// Resource name of class menu
            public string lpszClassName;// Name of the Window Class
            public IntPtr hIconSm;// Handle to the classes small icon
        }
    }
}
