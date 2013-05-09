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
using CairoDesktop.Interop;

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
            Windows = new ObservableCollection<ExtendedSystemWindow>();
            Windows = GetAllToplevelWindows();
            SetMinimizedMetrics();
            int msg = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            NativeMethods.SendMessage(NativeMethods.GetDesktopWindow(), 0x0400, IntPtr.Zero, IntPtr.Zero);
            //SendMessage(new IntPtr(0xffff), msg, IntPtr.Zero, IntPtr.Zero);
        }

        private static void SetMinimizedMetrics()
        {
            NativeMethods.MinimizedMetrics mm = new NativeMethods.MinimizedMetrics
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MinimizedMetrics))
            };

            IntPtr mmPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.MinimizedMetrics)));

            try
            {
                Marshal.StructureToPtr(mm, mmPtr, true);
                NativeMethods.SystemParametersInfo(NativeMethods.SPI.SPI_GETMINIMIZEDMETRICS, mm.cbSize, mmPtr, NativeMethods.SPIF.None);

                mm.iArrange |= NativeMethods.MinimizedMetricsArrangement.Hide;
                Marshal.StructureToPtr(mm, mmPtr, true);
                NativeMethods.SystemParametersInfo(NativeMethods.SPI.SPI_SETMINIMIZEDMETRICS, mm.cbSize, mmPtr, NativeMethods.SPIF.None);
            }
            finally
            {
                Marshal.DestroyStructure(mmPtr, typeof(NativeMethods.MinimizedMetrics));
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
                switch (msg.WParam.ToInt32())
                {
                    case HSHELL_WINDOWCREATED:
                    case HSHELL_WINDOWDESTROYED:
                    case HSHELL_WINDOWREPLACING:
                    case HSHELL_WINDOWREPLACED:
                        Windows = GetAllToplevelWindows();
                        Debug.WriteLine("Replaced windows " + msg.WParam.ToInt32());
                        FireWindowsChangedEvent();
                        break;
                    /*case 32772:
                        Windows = GetAllToplevelWindows();
                        Debug.WriteLine("Window changed " + msg.WParam.ToInt32());
                        FireWindowsChangedEvent();
                        break;*/

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
                        NativeMethods.SHELLHOOKINFO winHandle = (NativeMethods.SHELLHOOKINFO)Marshal.PtrToStructure(msg.LParam, typeof(NativeMethods.SHELLHOOKINFO));
                        winHandle.rc.top = 0;
                        winHandle.rc.left = 0;
                        winHandle.rc.bottom = 100;
                        winHandle.rc.right = 100;
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
            lock (_windowsLock)
            {
                ObservableCollection<ExtendedSystemWindow> replacedWindows = new ObservableCollection<ExtendedSystemWindow>();
                var allWindows = SystemWindow.AllToplevelWindows.Where((w) => w.Visible && w.Title != "").
                    ToList().ConvertAll<ExtendedSystemWindow>((w) => new ExtendedSystemWindow(w.HWnd));

                foreach (var w in allWindows)
                {
                    ExtendedSystemWindow existingWindow = null;
                    if ((existingWindow = Windows.FirstOrDefault((esw) => esw.HWnd == w.HWnd)) != null)
                        w.CopyFrom(existingWindow);
                    replacedWindows.Add(w);
                }

                return replacedWindows;
            }
        }

        public static ObservableCollection<ExtendedSystemWindow> Windows
        {
            get;
            set;
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
                System.Drawing.Icon ico = Imaging.ExtractIcon(this.Process.MainModule.FileName, HWnd);
                _cachedIcon = ico;
                return ico;
            }
        }

        public bool RestoreMaximized
        {
            get;
            set;
        }

        public ExtendedSystemWindow(IntPtr hWnd)
            : base(hWnd)
        {
        }

        public void BringWindowToFront()
        {
            NativeMethods.BringWindowToTop(HWnd);
        }

        public void Dispose()
        {
            _cachedIcon.Dispose();
        }

        public void CopyFrom(ExtendedSystemWindow wnd)
        {
            this.RestoreMaximized = wnd.RestoreMaximized;
        }
    }
}
