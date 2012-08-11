using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PreviewControls;

namespace CairoExplorer
{
    /// <summary>
    /// Interaction logic for Properties.xaml
    /// </summary>
    public partial class Properties : Window
    {
        private WrapperFileSystemInfo _file;
        private object _caller;

        public string CurrentPath = "";

        public Properties()
        {
            InitializeComponent();
        }

        public void InitWithFile(WrapperFileSystemInfo file, object caller)
        {
            _file = file;
            _caller = caller;

            Name.Text = file.Info.Name;
            Type.Text = file.Type.BaseExtension;
            Size.Text = file.Size;
            DateModified.Text = "Last Modified " + file.Info.DateModified.ToFileDateTime();
            Title.Text = file.Type.Folder ? "Folder" : "File";
            Icon.Source = file.Icon;

            if(Window.IsActive)
                Show();
        }

        private void Close_pressing(object sender, MouseButtonEventArgs e)
        {
            Close_btn.Fill = (ImageBrush)LayoutRoot.Resources["Close_pr"];
        }

        private void Activate_Title_Icons(object sender, MouseEventArgs e)
        {
            //hover effect, make sure your grid is named "Main" or replace "Main" with the name of your grid
            Close_btn.Fill = (ImageBrush)LayoutRoot.Resources["Close_act"];
        }

        private void Deactivate_Title_Icons(object sender, MouseEventArgs e)
        {
            Close_btn.Fill = (ImageBrush)LayoutRoot.Resources["Close_inact"];
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                Close_btn_MouseUp(sender, null);
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg,
                int wParam, int lParam);

        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public void move_window(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.Target is Ellipse)
                return;
            ReleaseCapture();
            SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private void Close_btn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Window.Close();

            PreviewControls.Preview.OpenPreview = null;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window.Topmost = false;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (_file != null)
            {
                CairoExplorerWindow.OpenItem(_file.Info.Path, _caller as CairoExplorerWindow, Settings.OpenFoldersInNewWindow);
                Close_btn_MouseUp(sender, null);
            }
        }

        private bool _doingExitAnimation = false;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_doingExitAnimation)
            {
                _doingExitAnimation = true;
                e.Cancel = true;
                var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.FadeInTime));
                anim.AccelerationRatio = 0.4;
                anim.DecelerationRatio = 0.6;
                anim.Completed += (s, _) => this.Close();
                this.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.FadeInTime));
            anim.AccelerationRatio = 0.4;
            anim.DecelerationRatio = 0.6;
            anim.From = 0;
            anim.To = 1;
            this.BeginAnimation(FrameworkElement.OpacityProperty, anim);
        }
    }

    public class PropertiesPreview : IPreview
    {
        public void Preview(object path, object caller, System.Windows.Point? pos)
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate()
            {
                Properties p = null;
                foreach (var w in Application.Current.Windows)
                    if (w is Properties)
                        p = (Properties)w;

                if (p != null)
                    LoadWindow(path as WrapperFileSystemInfo, p, caller, pos);
                else
                {
                    p = new Properties();
                    p.Loaded += delegate(object sender, RoutedEventArgs e)
                    {
                        LoadWindow(path as WrapperFileSystemInfo, p, caller, pos);
                    };
                    p.Show();
                }
            }));
        }

        public void ChangePreview(object path, object caller, System.Windows.Point? pos)
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate()
            {
                Properties p = null;
                foreach (var w in Application.Current.Windows)
                    if (w is Properties)
                        p = (Properties)w;

                if (p != null)
                    LoadWindow(path as WrapperFileSystemInfo, p, caller, pos);
                else
                {
                    p = new Properties();
                    p.Loaded += delegate(object sender, RoutedEventArgs e)
                    {
                        LoadWindow(path as WrapperFileSystemInfo, p, caller, null);
                    };
                    p.Show();
                }
            }));
        }

        public void ForcePreviewOpen()
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate()
            {
                Properties p = null;
                foreach (var w in Application.Current.Windows)
                    if (w is Properties)
                        p = (Properties)w;

                if (p != null)
                    p.Topmost = true;
            }));
        }

        public void PreviewHide()
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate()
            {
                Properties p = null;
                foreach (var w in Application.Current.Windows)
                    if (w is Properties)
                        p = (Properties)w;

                if (p != null)
                    p.Topmost = false;
            }));
        }

        public void PreviewClose()
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate()
            {
                Properties p = null;
                foreach (var w in Application.Current.Windows)
                    if (w is Properties)
                        p = (Properties)w;

                if (p != null)
                    p.Close();
                PreviewControls.Preview.OpenPreview = null;
            }));
        }

        private static void LoadWindow(WrapperFileSystemInfo path, Properties p, object caller, Point? pos)
        {
            p.WindowStartupLocation = WindowStartupLocation.Manual;
            p.Opacity = 1;
            if (pos != null)
            {
                p.Left = pos.Value.X;
                p.Top = pos.Value.Y;
            }
            if (path != null)
            {
                p.CurrentPath = path.Info.Path;
                if (!path.Type.File)
                    CairoExplorerWindow.AsyncGetSizeOfDirectory(path.Info.Path, path, delegate(WrapperFileSystemInfo file, double size, object param)
                    {
                        Application.Current.Dispatcher.Invoke(new Action(delegate()
                        {
                            file.ByteSize = size;
                            if(p.CurrentPath == file.Info.Path)
                                p.InitWithFile(file, caller);
                        }));
                    });
                 else
                    p.InitWithFile(path, caller);
            }
        }
    }
}
