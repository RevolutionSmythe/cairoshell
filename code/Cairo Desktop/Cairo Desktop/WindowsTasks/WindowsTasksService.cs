using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows;
using System.Collections.ObjectModel;
using DR = System.Drawing;
using ManagedWinapi.Windows;
using System.Security.Principal;

namespace CairoDesktop
{
    public class WindowsTasksService : DependencyObject
    {
        private static object _windowsLock = new object();
        public static event EventHandler WindowsChanged = null;

        public static int WM_SHELLHOOKMESSAGE = -1;
        public const int WH_SHELL = 10;

        public const int HSHELL_WINDOWCREATED = 1;
        public const int HSHELL_WINDOWDESTROYED = 2;
        public const int HSHELL_ACTIVATESHELLWINDOW = 3;

        //Windows NT
        public const int HSHELL_WINDOWACTIVATED = 4;
        public const int HSHELL_GETMINRECT = 5;
        public const int HSHELL_REDRAW = 6;
        public const int HSHELL_TASKMAN = 7;
        public const int HSHELL_LANGUAGE = 8;
        public const int HSHELL_SYSMENU = 9;
        public const int HSHELL_ENDTASK = 10;
        //Windows 2000
        public const int HSHELL_ACCESSIBILITYSTATE = 11;
        public const int HSHELL_APPCOMMAND = 12;

        //Windows XP
        public const int HSHELL_WINDOWREPLACED = 13;
        public const int HSHELL_WINDOWREPLACING = 14;

        public const int HSHELL_HIGHBIT = 0x8000;
        public const int HSHELL_FLASH = (HSHELL_REDRAW | HSHELL_HIGHBIT);
        public const int HSHELL_RUDEAPPACTIVATED = (HSHELL_WINDOWACTIVATED | HSHELL_HIGHBIT);

        static WindowsTasksService()
        {
            Windows = GetAllToplevelWindows();
            SetMinimizedMetrics();
            int msg = RegisterWindowMessage("TaskbarCreated");
            SendMessage(GetDesktopWindow(), 0x0400, IntPtr.Zero, IntPtr.Zero);
            //SendMessage(new IntPtr(0xffff), msg, IntPtr.Zero, IntPtr.Zero);
        }

        private static void SetMinimizedMetrics()
        {
            MinimizedMetrics mm = new MinimizedMetrics
            {
                cbSize = (uint)Marshal.SizeOf(typeof(MinimizedMetrics))
            };

            IntPtr mmPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MinimizedMetrics)));

            try
            {
                Marshal.StructureToPtr(mm, mmPtr, true);
                SystemParametersInfo(SPI.SPI_GETMINIMIZEDMETRICS, mm.cbSize, mmPtr, SPIF.None);
                
                mm.iArrange |= MinimizedMetricsArrangement.Hide;
                Marshal.StructureToPtr(mm, mmPtr, true);
                SystemParametersInfo(SPI.SPI_SETMINIMIZEDMETRICS, mm.cbSize, mmPtr, SPIF.None);
            }
            finally
            {
            	Marshal.DestroyStructure(mmPtr, typeof(MinimizedMetrics));
                Marshal.FreeHGlobal(mmPtr);
            }
        }

        public static void ShellWinProc(System.Windows.Forms.Message msg)
        {
            try
            {
                /*var win = Windows.FirstOrDefault((w) => w.HWnd == msg.LParam);
                if (win == null && msg.LParam == IntPtr.Zero)
                    win = SystemWindow.DesktopWindow;*/
                lock (_windowsLock)
                {
                    switch (msg.WParam.ToInt32())
                    {
                        case HSHELL_WINDOWCREATED:
                        case HSHELL_WINDOWDESTROYED:
                        case HSHELL_WINDOWREPLACING:
                        case HSHELL_WINDOWREPLACED:
                            Windows = GetAllToplevelWindows();
                            FireWindowsChangedEvent();
                            break;

                        /*case HSHELL_WINDOWACTIVATED:
                        case HSHELL_RUDEAPPACTIVATED:
                            Trace.WriteLine("Activated: " + msg.LParam.ToString());

                            if (msg.LParam == IntPtr.Zero)
                            {
                                break;
                            }

                            foreach (var aWin in this.Windows)
                            {
                                if(aWin.State == ApplicationWindow.WindowState.Active)
                                    aWin.State = ApplicationWindow.WindowState.Inactive;
                            }

                            if (this.Windows.Contains(win))
                            {
                                GetRealWindow (msg, ref win);
                                win.State = ApplicationWindow.WindowState.Active;
                            }
                            else
                            {
                                win.State = ApplicationWindow.WindowState.Active;
                                if (win.Title != "")
                                    Windows.Add (win);
                            }
                            break;

                        case HSHELL_FLASH:
                            Trace.WriteLine("Flashing window: " + msg.LParam.ToString());
                            if (this.Windows.Contains(win))
                            {
                                GetRealWindow (msg, ref win);
                                win.State = ApplicationWindow.WindowState.Flashing;
                            }
                            else
                            {
                                win.State = ApplicationWindow.WindowState.Flashing;
                                if (win.Title != "")
                                    Windows.Add (win);
                            }
                            break;
                            */
                        case HSHELL_ACTIVATESHELLWINDOW:
                            Trace.WriteLine("Activeate shell window called.");
                            break;

                        case HSHELL_ENDTASK:
                            Trace.WriteLine("EndTask called.");
                            break;

                        case HSHELL_GETMINRECT:
                            Trace.WriteLine("GetMinRect called.");
                            SHELLHOOKINFO winHandle = (SHELLHOOKINFO)Marshal.PtrToStructure(msg.LParam, typeof(SHELLHOOKINFO));
                            winHandle.rc.Top = 0;
                            winHandle.rc.Left = 0;
                            winHandle.rc.Bottom = 100;
                            winHandle.rc.Right = 100;
                            Marshal.StructureToPtr(winHandle, msg.LParam, true);
                            msg.Result = winHandle.hwnd;
                            break;

                        case HSHELL_REDRAW:
                            Trace.WriteLine("Redraw called.");
                            break;

                        // TaskMan needs to return true if we provide our own task manager to prevent explorers.
                        // case HSHELL_TASKMAN:
                        //     Trace.WriteLine("TaskMan Message received.");
                        //     break;

                        default:
                            Trace.WriteLine("Unknown called. " + msg.Msg.ToString());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception: " + ex.ToString());
                Debugger.Break();
            }
        }

        private static void FireWindowsChangedEvent()
        {
            if (WindowsChanged != null)
                WindowsChanged(null, EventArgs.Empty);
        }

        private static ObservableCollection<ExtendedSystemWindow> GetAllToplevelWindows()
        {
            return new ObservableCollection<ExtendedSystemWindow>(SystemWindow.AllToplevelWindows.Where((w) => w.Visible && w.Title != "").
                ToList().ConvertAll < ExtendedSystemWindow>((w) => new ExtendedSystemWindow(w.HWnd)));
        }

        [DllImport("user32.dll")]
        public static extern bool RegisterShellHookWindow (IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int RegisterWindowMessage (string message);

        [DllImport("user32.dll")]
        public static extern bool SetTaskmanWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage (IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        public static extern bool DeregisterShellHookWindow (IntPtr hWnd);

        [DllImport("Shell32.dll")]
        public static extern bool RegisterShellHook (IntPtr hWnd, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow ();

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo (SPI uiAction, uint uiParam, IntPtr pvParam, SPIF fWinIni);

        public static ObservableCollection<ExtendedSystemWindow> Windows
        {
            get;
            set;
        }

        [Flags]
        public enum SPIF
        {
           None =          0x00,
           SPIF_UPDATEINIFILE =    0x01,  // Writes the new system-wide parameter setting to the user profile.
           SPIF_SENDCHANGE =       0x02,  // Broadcasts the WM_SETTINGCHANGE message after updating the user profile.
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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Top;
            public int Left;
            public int Bottom;
            public int Right;

            public RECT(int left_, int top_, int right_, int bottom_)
            {
                Left = left_;
                Top = top_;
                Right = right_;
                Bottom = bottom_;
            }

            public int Height { get { return Bottom - Top; } }
            public int Width { get { return Right - Left; } }
            public DR.Size Size { get { return new DR.Size(Width, Height); } }

            public Point Location { get { return new Point(Left, Top); } }

            // Handy method for converting to a System.Drawing.Rectangle
            public DR.Rectangle ToRectangle()
            { return DR.Rectangle.FromLTRB(Left, Top, Right, Bottom); }

            public static RECT FromRectangle(DR.Rectangle rectangle)
            {
                return new RECT(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            }

            public override int GetHashCode()
            {
                return Left ^ ((Top << 13) | (Top >> 0x13))
                  ^ ((Width << 0x1a) | (Width >> 6))
                  ^ ((Height << 7) | (Height >> 0x19));
            }

            #region Operator overloads

            public static implicit operator DR.Rectangle(RECT rect)
            {
                return rect.ToRectangle();
            }

            public static implicit operator RECT(DR.Rectangle rect)
            {
                return FromRectangle(rect);
            }

            #endregion

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
	        private struct SHELLHOOKINFO
            {
		        public IntPtr hwnd;
		        public RECT rc;
            }
    }

    public class ExtendedSystemWindow : SystemWindow, IDisposable
    {
        private System.Drawing.Icon _cachedIcon = null;

        public System.Drawing.Icon Icon
        {
            get
            {
                if (this.HWnd == IntPtr.Zero)
                    return null;
                if (_cachedIcon != null)
                    return _cachedIcon;
                System.Drawing.Icon ico = null;

                try
                {
                    ico = Etier.IconHelper.IconReader.GetFileIcon(this.Process.MainModule.FileName, Etier.IconHelper.IconReader.IconSize.Large, false);
                    return ico;
                }
                catch { }
                Debug.Write("Failed to get permissions to access process, falling back to small ico only.");
                uint iconHandle = GetIconForWindow();

                try
                {
                    ico = System.Drawing.Icon.FromHandle(new IntPtr(iconHandle));
                }
                catch (Exception)
                {
                    ico = null;
                }
                _cachedIcon = ico;
                return ico;
            }
        }

        [DllImport("user32.dll")]
        private static extern uint SendMessageTimeout(IntPtr hWnd, uint messageId, uint wparam, uint lparam, uint timeoutFlags, uint timeout, ref uint retval);

        [DllImport("user32.dll")]
        private static extern uint GetClassLong(IntPtr handle, int longClass);

        private uint GetIconForWindow()
        {
            uint hIco = 0;
            SendMessageTimeout(this.HWnd, 127, 2, 0, 2, 200, ref hIco);
            int GCL_HICON = -14;
            if (hIco == 0)
            {
                hIco = GetClassLong(this.HWnd, GCL_HICON);
            }

            return hIco;
        }

        public ExtendedSystemWindow(IntPtr hWnd)
            : base(hWnd)
        {
        }

        [DllImport("user32.dll", SetLastError=true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        public void BringWindowToFront()
        {
            BringWindowToTop(HWnd);
        }

        public void Dispose()
        {
            _cachedIcon.Dispose();
        }
    }
}
