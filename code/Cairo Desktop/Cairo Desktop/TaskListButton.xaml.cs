using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using System.Windows.Markup;
using System.Runtime.InteropServices;
using ManagedWinapi.Windows;

namespace CairoDesktop
{
    public partial class TaskListButton
    {
        [DllImport("user32.dll")]
        public static extern int FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);

        public const int WM_COMMAND = 0x0112;
        public const int WM_CLOSE = 0xF060;
        public TaskListButton()
        {
            this.InitializeComponent();
            // Insert code required on object creation below this point.
            // Sets the Theme for Cairo
            string theme = Properties.Settings.Default.CairoTheme;
            if (theme != "Cairo.xaml")
            {
                ResourceDictionary CairoDictionary = (ResourceDictionary)XamlReader.Load(System.Xml.XmlReader.Create(AppDomain.CurrentDomain.BaseDirectory + theme));
                this.Resources.MergedDictionaries[0] = CairoDictionary;
            }

        }

        private void btnClick(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
            {
                if (windowObject.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                {
                    if (windowObject.Enabled)
                        windowObject.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                    else
                        windowObject.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    windowObject.Enabled = true;
                }
                else
                {
                    windowObject.Enabled = windowObject.WindowState == System.Windows.Forms.FormWindowState.Maximized;
                    windowObject.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                }
            }
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
                windowObject.WindowState = System.Windows.Forms.FormWindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
                windowObject.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
                windowObject.SendClose();
        }

        private void Force_Close_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
                windowObject.Process.Kill();//Kill it
        }

        private void Add_To_Menu_Click (object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as ExtendedSystemWindow;
            if (windowObject != null)
            {
                AppGrabber.ApplicationInfo info = new AppGrabber.ApplicationInfo(windowObject.Process.MainWindowTitle, windowObject.Process.MainModule.FileName, null);
                info.Icon = info.GetAssociatedIcon();
                AppGrabber.Category c = AppGrabber.AppGrabber.Instance.CategoryList.GetCategory("Uncategorized");
                c.Add(info);
                AppGrabber.AppGrabber.Instance.CategoryList.Remove(c);
                AppGrabber.AppGrabber.Instance.CategoryList.Add(c);
            }
        }
    }
}