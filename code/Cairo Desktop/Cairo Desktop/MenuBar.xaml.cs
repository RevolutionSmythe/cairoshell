using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using CairoDesktop.Interop;
using IWshRuntimeLibrary;
// Helper code - thanks to Greg Franklin - MSFT
using SHAppBarMessage1.Common;
using Windows.UI.Notifications;
using Microsoft.WindowsAPICodePack.Shell;

namespace CairoDesktop
{
    public partial class MenuBar
    {
        private WindowInteropHelper helper;
        private IntPtr handle;
        private int appbarMessageId = -1;
        private const string EXPLORER_PATH = "C:\\Windows\\explorer.exe";

        private String configFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CairoAppConfig.xml";

        public ObservableCollection<TaskBarItem> TaskBarItems
        {
            get;
            protected set;
        }

        public MenuBar()
        {
            this.DataContext = this;
            TaskBarItems = new ObservableCollection<TaskBarItem>();
            this.InitializeComponent();

            WindowsTasksService.WindowsChanged += WindowsTasksService_WindowsChanged;
            BuildTaskBarItems();

            // Show the search button only if the service is running
            if (WindowsServices.QueryStatus("WSearch") == ServiceStatus.Running)
            {
                ObjectDataProvider vistaSearchProvider = new ObjectDataProvider();
                vistaSearchProvider.ObjectType = typeof(VistaSearchProvider.VistaSearchProviderHelper);
                CairoSearchMenu.DataContext = vistaSearchProvider;
            }
            else
            {
                CairoSearchMenu.Visibility = Visibility.Collapsed;
                DispatcherTimer searchcheck = new DispatcherTimer(new TimeSpan(0, 0, 7), DispatcherPriority.Normal, delegate
                {

                    if (WindowsServices.QueryStatus("WSearch") == ServiceStatus.Running)
                    {
                        ObjectDataProvider vistaSearchProvider = new ObjectDataProvider();
                        vistaSearchProvider.ObjectType = typeof(VistaSearchProvider.VistaSearchProviderHelper);
                        CairoSearchMenu.DataContext = vistaSearchProvider;
                        CairoSearchMenu.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CairoSearchMenu.Visibility = Visibility.Collapsed;
                    }

                }, this.Dispatcher);

            }
            // ---------------------------------------------------------------- //

            InitializeClock();
        }

        void WindowsTasksService_WindowsChanged(object sender, EventArgs e)
        {
            BuildTaskBarItems();
        }

        private void BuildTaskBarItems()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Dictionary<string, TaskBarItem> items = new Dictionary<string, TaskBarItem>();
                int Position = 0;
                foreach (string linkPath in TaskbarPinnedItems.GetPinnedTaskBarItems())
                {
                    ShellFile link = ShellFile.FromFilePath(linkPath);
                    if (!link.IsLink)
                    {
                        link.Dispose();
                        continue;
                    }

                    string displayName = link.GetDisplayName(DisplayNameType.Default);
                    string path = link.Properties.System.Link.TargetParsingPath.Value;
                    if (System.IO.File.Exists(path))
                        items.Add(path, new TaskBarItem(path, null, displayName, Imaging.ExtractIcon(path, IntPtr.Zero), Position++));
                    else
                        items.Add(EXPLORER_PATH, new TaskBarItem(EXPLORER_PATH, null, "File Explorer", Imaging.ExtractIcon(EXPLORER_PATH, IntPtr.Zero), Position++));
                    link.Dispose();
                }
                foreach (var win in WindowsTasksService.Windows.Where((w) => w.Enabled && w.ClassName != "Progman"))
                {
                    if (!items.ContainsKey(win.Process.MainModule.FileName))
                        items.Add(win.Process.MainModule.FileName, new TaskBarItem(win.Process.MainModule.FileName, null, win.Title, win.Icon, Position++));
                    items[win.Process.MainModule.FileName]._openWindows.Add(win);
                }
                foreach (var itm in items.Values)
                {
                    TaskBarItem existingWindow = null;
                    if ((existingWindow = TaskBarItems.FirstOrDefault((esw) => esw.FullPath == itm.FullPath)) != null)
                        itm.CopyFrom(existingWindow);
                }
                TaskBarItems.Clear();
                foreach (var itm in items.Values.OrderByDescending((itm) => -1 * itm.Position))
                    TaskBarItems.Add(itm);
            }));
        }

        ///
        /// Focuses the specified UI element.
        ///
        /// The UI element.
        public void FocusSearchBox(object sender, RoutedEventArgs e)
        {
            searchStr.Dispatcher.BeginInvoke(
            new Action(delegate
            {
                searchStr.Focusable = true;
                searchStr.Focus();
                Keyboard.Focus(searchStr);
            }),
            DispatcherPriority.Render);
        }

        public void ExecuteOpenSearchResult(object sender, ExecutedRoutedEventArgs e)
        {
            // Get parameter (e.Parameter as T)
            // Try shell execute...
            // TODO: Determine which app to start the file as and boom!
            var searchObj = (VistaSearchProvider.SearchResult)e.Parameter;

            Process p = new Process();
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.FileName = searchObj.Path; // e.Parameter as T.x
            p.StartInfo.Verb = "Open";

            try
            {
                p.Start();
            }
            catch (Exception ex)
            {
                CairoMessage.Show("Woops, it seems we had some trouble opening the search result you chose.\n\n The error we received was: " + ex.Message, "Uh Oh!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (appbarMessageId == -1)
            {
                return IntPtr.Zero;
            }

            if (msg == appbarMessageId)
            {
                System.Diagnostics.Trace.WriteLine("Callback on AppBarMessage: " + wparam.ToString());
                switch (wparam.ToInt32())
                {
                    case 1:
                        // Reposition to the top of the screen.
                        if (this.Top != 0)
                        {
                            System.Diagnostics.Trace.WriteLine("Repositioning menu bar to top of screen.");
                            this.Top = 0;
                        }
                        /*SHAppBarMessageHelper.QuerySetPosition(hwnd, 
                            new System.Drawing.Size() { Height = (int)this.ActualWidth, Width = (int)this.ActualWidth }, 
                            SHAppBarMessage1.Win32.NativeMethods.ABEdge.ABE_TOP);*/
                        break;
                }
                handled = true;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Initializes the dispatcher timers to updates the time and date bindings
        /// </summary>
        private void InitializeClock()
        {
            // Create our timer for clock
            DispatcherTimer timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                timeText.Text = DateTime.Now.ToString("h:mm tt  ");
                monthText.Text = DateTime.Now.ToString("MMMM ");
                dayText.Text = DateTime.Now.Day.ToString() + "   ";
            }, this.Dispatcher);

            DispatcherTimer fulldatetimer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                string dateFormat = "D"; // Culturally safe Long Date Pattern

                timeText.ToolTip = DateTime.Now.ToString(dateFormat);
                monthText.ToolTip = DateTime.Now.ToString(dateFormat);
                dayText.ToolTip = DateTime.Now.ToString(dateFormat);
            }, this.Dispatcher);
        }

        private void OnWindowInitialized(object sender, EventArgs e)
        {
            Visibility = Visibility.Visible;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            helper = new WindowInteropHelper(this);

            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            source.AddHook(new HwndSourceHook(WndProc));

            handle = helper.Handle;
            System.Drawing.Size size = new System.Drawing.Size((int)this.ActualWidth, (int)this.ActualHeight);

            appbarMessageId = SHAppBarMessageHelper.RegisterBar(handle, size);
            //SHAppBarMessageHelper.QuerySetPosition(handle, size, SHAppBarMessage1.Win32.NativeMethods.ABEdge.ABE_TOP);

            SysTray.InitializeSystemTray();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (CairoMessage.ShowOkCancel("You will need to reboot or use the start menu shortcut in order to run Cairo again.", "Are you sure you want to exit Cairo?", "Resources/cairoIcon.png", "Exit Cairo", "Cancel") == true)
            {
                //SHAppBarMessageHelper.DeRegisterBar(handle);
                System.Drawing.Size size = new System.Drawing.Size((int)this.ActualWidth, (int)this.ActualHeight);
                SHAppBarMessageHelper.RegisterBar(handle, size);
                SysTray.DestroySystemTray();
                Application.Current.Shutdown();
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void OnWindowResize(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("OnWindowResize raised...");
            System.Drawing.Size size = new System.Drawing.Size((int)this.ActualWidth, (int)this.ActualHeight);
            //SHAppBarMessageHelper.QuerySetPosition(handle, size, SHAppBarMessage1.Win32.NativeMethods.ABEdge.ABE_TOP);
            SHAppBarMessageHelper.ABSetPos(handle, size);
        }

        private void LaunchProgram(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            try
            {
                System.Diagnostics.Process.Start(item.CommandParameter.ToString());
            }
            catch
            {
                CairoMessage.Show("The file could not be found.  If you just removed this program, try removing it from the App Grabber to make the icon go away.", "Oops!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutCairo(object sender, RoutedEventArgs e)
        {
            CairoMessage.Show(
                // Replace next line with the Version
                "Version 0.0.1.12 - Milestone 3 Preview 3"
                + "\nCopyright © 2007-2011 Cairo Development Team and community contributors.  All rights reserved."
                // +
                // Replace next line with the ID Key
                //"Not for redistribution."
                , "Cairo Desktop Environment", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void OpenLogoffBox(object sender, RoutedEventArgs e)
        {
            bool? LogoffChoice = CairoMessage.ShowOkCancel("You will lose all unsaved documents and be logged off.", "Are you sure you want to log off now?", "Resources/logoffIcon.png", "Log Off", "Cancel");
            if (LogoffChoice.HasValue && LogoffChoice.Value)
            {
                NativeMethods.Logoff();
            }
            else
            {
            }
        }

        private void OpenRebootBox(object sender, RoutedEventArgs e)
        {
            bool? RebootChoice = CairoMessage.ShowOkCancel("You will lose all unsaved documents and your computer will restart.", "Are you sure you want to restart now?", "Resources/restartIcon.png", "Restart", "Cancel");
            if (RebootChoice.HasValue && RebootChoice.Value)
            {
                NativeMethods.Reboot();
            }
            else
            {
            }
        }

        private void OpenShutDownBox(object sender, RoutedEventArgs e)
        {
            bool? ShutdownChoice = CairoMessage.ShowOkCancel("You will lose all unsaved documents and your computer will turn off.", "Are you sure you want to shut down now?", "Resources/shutdownIcon.png", "Shut Down", "Cancel");
            if (ShutdownChoice.HasValue && ShutdownChoice.Value)
            {
                NativeMethods.Shutdown();
            }
            else
            {
            }
        }

        private void OpenCloseCairoBox(object sender, RoutedEventArgs e)
        {
            bool? CloseCairoChoice = CairoMessage.ShowOkCancel("You will need to reboot or use the start menu shortcut in order to run Cairo again.", "Are you sure you want to exit Cairo?", "Resources/cairoIcon.png", "Exit Cairo", "Cancel");
            if (CloseCairoChoice.HasValue && CloseCairoChoice.Value)
            {
                //SHAppBarMessageHelper.DeRegisterBar(handle);
                System.Drawing.Size size = new System.Drawing.Size((int)this.ActualWidth, (int)this.ActualHeight);
                SHAppBarMessageHelper.RegisterBar(handle, size);
                SysTray.DestroySystemTray();
                Application.Current.Shutdown();
                // TODO: Will want to relaunch explorer.exe when we start disabling it
            }
            else
            {
            }
        }

        /*private void OpenRecycleBin(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(fileManger, "::{645FF040-5081-101B-9F08-00AA002F954E}");
        }*/

        private void OpenControlPanel(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("control.exe");
        }

        private void OpenTaskManager(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("taskmgr.exe");
        }

        private void OpenTimeDateCPL(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("timedate.cpl");
        }

        private void SysSleep(object sender, RoutedEventArgs e)
        {
            NativeMethods.Sleep();
        }

        private void LaunchShortcut(object sender, RoutedEventArgs e)
        {
            Button item = (Button)sender;
            string ItemLoc = item.CommandParameter.ToString();
            System.Diagnostics.Process.Start(ItemLoc);
        }

        /// <summary>
        /// Retrieves the users ID key from a compiled resources file.
        /// </summary>
        /// <returns>The users ID key.</returns>
        private string GetUsersIdKey()
        {
            string idKey = "For internal use only.";
            string resKey = null;

            try
            {
                var mgr = ResourceManager.CreateFileBasedResourceManager("cairo", Environment.CurrentDirectory, null);
                resKey = mgr.GetString("ID-Key");
            }
            catch (Exception)
            {
                resKey = null;
            }

            return resKey ?? idKey;
        }
    }

    public class TaskBarItem
    {
        public List<ExtendedSystemWindow> _openWindows = new List<ExtendedSystemWindow>();
        private System.Drawing.Icon _icon;
        public System.Drawing.Icon Icon
        {
            get
            {
                return _icon;
            }
        }

        private string _title;
        public string Title
        {
            get 
            {
                if (_openWindows.Count > 0)
                    return _openWindows[0].Title;
                return _title;
            }
        }

        public int Position
        {
            get;
            set;
        }

        public string FullPath
        {
            get;
            protected set;
        }

        public TaskBarItem(string filePath, IEnumerable<ExtendedSystemWindow> windows, string title, System.Drawing.Icon icon, int predictedPosition)
        {
            _openWindows = new List<ExtendedSystemWindow>();
            FullPath = filePath;
            _title = title;
            _icon = icon;
            Position = predictedPosition;
            if (windows != null && windows.Count() > 0)
                _openWindows.AddRange(windows);
        }

        internal void CopyFrom(TaskBarItem existingWindow)
        {
            Position = existingWindow.Position;
        }

        public void Maximize()
        {
            var windowSelected = _openWindows[0];
            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }

        public void CloseWindow()
        {
            var windowSelected = _openWindows[0];
            windowSelected.SendClose();
        }

        public void Minimize()
        {
            var windowSelected = _openWindows[0];
            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Minimized;
        }

        public void ForceCloseWindow()
        {
            var windowSelected = _openWindows[0];
            windowSelected.Process.Kill();//Kill it
        }

        internal void OpenNewWindow()
        {
            throw new NotImplementedException();
        }
    }
}
