﻿namespace CairoDesktop
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using Microsoft.Win32;
    using CairoDesktop.SupportingClasses;
    using System.Windows.Interop;

    /// <summary>
    /// Handles the startup of the application, including ensuring that only a single instance is running.
    /// </summary>
    public class Startup
    {
        private static System.Threading.Mutex cairoMutex;

        private static System.Windows.Window _parentWindow;

        public static MenuBar MenuBarWindow { get; set; }
        public static MenuBarShadow MenuBarShadowWindow { get; set; }
        public static Taskbar TaskbarWindow { get; set; }
        public static Desktop DesktopWindow { get; set; }


        /// <summary>
        /// The main entry point for the application
        /// </summary>
        [STAThread]
        public static void Main()
        {
            #region Single Instance Check
            bool ok;
            cairoMutex = new System.Threading.Mutex(true, "CairoShell", out ok);

            if (!ok)
            {
                // Another instance is already running.
                return;
            }
            #endregion

            #region some real shell code
            int hShellReadyEvent;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 5)
                hShellReadyEvent = NativeMethods.OpenEvent(NativeMethods.EVENT_MODIFY_STATE, true, @"Global\msgina: ShellReadyEvent");
            else
                hShellReadyEvent = NativeMethods.OpenEvent(NativeMethods.EVENT_MODIFY_STATE, false, "msgina: ShellReadyEvent");

            if (hShellReadyEvent != 0)
            {
                NativeMethods.SetEvent(hShellReadyEvent);
                NativeMethods.CloseHandle(hShellReadyEvent);
            }
            #endregion

            #region old code
            //if (!SingleInstanceCheck())
            //{
            //    return;
            //}

            // Causes crash?
            // If framework is not correct version then quit.
            //if (!FrameworkCheck())
            //{
            //    return;
            //}
            #endregion

            InitializeParentWindow();

            App app = new App();

            MenuBarWindow = new MenuBar() { Owner = _parentWindow };
            MenuBarWindow.Show();
            app.MainWindow = MenuBarWindow;

#if (ENABLEFIRSTRUN)
            FirstRun(app);
#endif

            if (Properties.Settings.Default.EnableDesktop)
            {
                //DesktopWindow = new Desktop() { Owner = _parentWindow };
                DesktopWindow = new Desktop() {};
                DesktopWindow.Show();
                WindowInteropHelper f = new WindowInteropHelper(DesktopWindow);
                int result = NativeMethods.SetShellWindow(f.Handle);
                DesktopWindow.ShowWindowBottomMost(f.Handle);
            }

            if (Properties.Settings.Default.EnableMenuBarShadow)
            {
                MenuBarShadowWindow = new MenuBarShadow() { Owner = _parentWindow };
                MenuBarShadowWindow.Show();
            }

            Int32 TOPMOST_FLAGS = 0x0001 | 0x0002;
            WindowInteropHelper taskbarWindowHelper = null;
            if (Properties.Settings.Default.EnableTaskbar)
            {
                TaskbarWindow = new Taskbar() { Owner = _parentWindow };
                TaskbarWindow.Show();
                taskbarWindowHelper = new WindowInteropHelper (TaskbarWindow);
                TaskBarPtr = taskbarWindowHelper.Handle;
                CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (taskbarWindowHelper.Handle, (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
            }
            WindowInteropHelper menuBarHelper = new WindowInteropHelper (MenuBarWindow);
            MenuBarPtr = menuBarHelper.Handle;
            CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (menuBarHelper.Handle, (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);

            CairoDesktop.WindowsTasks.NativeWindowEx _HookWin = new CairoDesktop.WindowsTasks.NativeWindowEx ();
            _HookWin.CreateHandle (new System.Windows.Forms.CreateParams ());
            CairoDesktop.WindowsTasks.WindowsTasksService.SetTaskmanWindow (_HookWin.Handle);
            //'Register to receive shell-related events
            //SetTaskmanWindow(hookWin.Handle)
            CairoDesktop.WindowsTasks.WindowsTasksService.RegisterShellHookWindow (_HookWin.Handle);

            //'Assume no error occurred
            WM_SHELLHOOKMESSAGE = CairoDesktop.WindowsTasks.WindowsTasksService.RegisterWindowMessage ("SHELLHOOK");
            _HookWin.MessageReceived += ShellWinProc;

            app.Run();

        }

        public static IntPtr MenuBarPtr = IntPtr.Zero;
        public static IntPtr TaskBarPtr = IntPtr.Zero;
        public static int WM_SHELLHOOKMESSAGE = -1;
        public static void ShellWinProc (System.Windows.Forms.Message msg)
        {
            if (msg.Msg == CairoDesktop.WindowsTasks.WindowsTasksService.WM_SHELLHOOKMESSAGE)
            {
                if (msg.LParam == IntPtr.Zero)
                    return;
                try
                {
                    switch (msg.WParam.ToInt32 ())
                    {
                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_WINDOWCREATED:

                            CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (MenuBarPtr,
                                (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            if(TaskBarPtr != IntPtr.Zero)
                                CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (TaskBarPtr,
                                    (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            break;

                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_WINDOWDESTROYED:

                            break;

                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_WINDOWREPLACING:
                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_WINDOWREPLACED:
                            CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (MenuBarPtr,
                                (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            if(TaskBarPtr != IntPtr.Zero)
                                CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (TaskBarPtr,
                                    (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            break;

                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_WINDOWACTIVATED:
                        case CairoDesktop.WindowsTasks.WindowsTasksService.HSHELL_RUDEAPPACTIVATED:
                            CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (MenuBarPtr,
                                (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            if(TaskBarPtr != IntPtr.Zero)
                                CairoDesktop.SupportingClasses.NativeMethods.SetWindowPos (TaskBarPtr,
                                    (IntPtr)CairoDesktop.SupportingClasses.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002);
                            break;

                        default:
                            Trace.WriteLine ("Uknown called. " + msg.Msg.ToString ());
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine ("Exception: " + ex.ToString ());
                    Debugger.Break ();
                }
            }
        }

        /// <summary>
        /// Checks the version of the framework available on the machine. We require .NET 3.5SP1 for the WPFToolkit.
        /// </summary>
        /// <returns>Result of framework check.</returns>
        private static bool FrameworkCheck()
        {
            try
            {
                var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\NET Framework Setup\\NDP\\v3.5", false);

                int verValue = (key == null) ? 0 : (int)key.GetValue("SP", 0, RegistryValueOptions.None);

                if (verValue != 0)
                {
                    return true;
                }
                else
                {
                    CairoMessage.Show("Cairo requires .NET Framework 3.5 SP1 or newer to run.  You can visit microsoft.com to obtain it.", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return false;
                }
            }
            catch (Exception ex)
            {
                CairoMessage.Show("Cairo requires .NET Framework 3.5 SP1 or newer to run.  You can visit microsoft.com to obtain it.", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                return false;
            }
        }

        /// <summary>
        /// Checks that a single instance of the application is running, and if another is found it notifies the user and exits.
        /// </summary>
        /// <returns>Result of instance check.</returns>
        private static bool SingleInstanceCheck()
        {
            string proc = Process.GetCurrentProcess().ProcessName;

            // get the list of all processes by that name
            Process[] processes = Process.GetProcessesByName(proc);

            // if there is more than one process...
            if (processes.Length > 1)
            {
                System.Threading.Thread.Sleep(1000);
                Process[] processes2 = Process.GetProcessesByName(proc);
                if (processes2.Length > 1)
                {
                    System.Threading.Thread.Sleep(3000);
                    Process[] processes3 = Process.GetProcessesByName(proc);
                    if (processes3.Length > 1)
                    {
                        CairoMessage.Show("If it's not responding, end it from Task Manager before trying to run Cairo again.", "Cairo is already running!", MessageBoxButton.OK, MessageBoxImage.Stop);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Executes the first run sequence.
        /// </summary>
        /// <param name="app">References to the app object.</param>
        private static void FirstRun(App app)
        {
            try
            {
                if (Properties.Settings.Default.IsFirstRun == true)
                {
                    Properties.Settings.Default.IsFirstRun = false;
                    Properties.Settings.Default.EnableTaskbar = true;
                    Properties.Settings.Default.Save();
                    AppGrabber.AppGrabber.Instance.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                CairoMessage.Show(string.Format("Woops! Something bad happened in the startup process.\nCairo will probably run, but please report the following details (preferably as a screen shot...)\n\n{0}", ex), 
                    "Unexpected error!", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes a new hidden toolwindow to be the owner for all other windows.
        /// This hides the applications icons from the task switcher.
        /// </summary>
        private static void InitializeParentWindow()
        {
            _parentWindow = new Window();
            _parentWindow.Top = -100; // Location of new window is outside of visible part of screen
            _parentWindow.Left = -100;
            _parentWindow.Width = 1; // size of window is enough small to avoid its appearance at the beginning
            _parentWindow.Height = 1;
            _parentWindow.WindowStyle = WindowStyle.ToolWindow; // Set window style as ToolWindow to avoid its icon in AltTab 
            _parentWindow.ShowInTaskbar = false;
            _parentWindow.Show(); // We need to show window before set is as owner to our main window
            _parentWindow.Hide(); // Hide helper window just in case
        }

    }
}
