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
	public partial class TaskButton
	{
		public TaskButton()
		{
			this.InitializeComponent();
		}

        private void btnClick(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null)
            {
                if (windowObject._openWindows.Count > 0)
                {
                    var windowSelected = windowObject._openWindows[0];
                    if (windowSelected.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                    {
                        if (windowSelected.RestoreMaximized)
                            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                        else
                            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Normal;
                        windowSelected.BringWindowToFront();
                    }
                    else
                    {
                        windowSelected.RestoreMaximized = windowSelected.WindowState == System.Windows.Forms.FormWindowState.Maximized;
                        windowSelected.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                    }
                }
            }
        }

        private void btn_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null)
            {
                if (windowObject._openWindows.Count > 0)
                {
                    var windowSelected = windowObject._openWindows[0];
                    if (windowSelected.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                    {
                        if (windowSelected.RestoreMaximized)
                            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                        else
                            windowSelected.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    }
                    else
                    {
                        windowSelected.RestoreMaximized = windowSelected.WindowState == System.Windows.Forms.FormWindowState.Maximized;
                        windowSelected.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                    }
                }
            }
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null && windowObject._openWindows.Count > 0)
                windowObject.Minimize();
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null && windowObject._openWindows.Count > 0)
                windowObject.Maximize();
        }

        private void Close_Click (object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null && windowObject._openWindows.Count > 0)
                windowObject.CloseWindow();
        }

        private void Force_Close_Click (object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null && windowObject._openWindows.Count > 0)
                windowObject.ForceCloseWindow();
        }

        private void Add_To_Menu_Click (object sender, RoutedEventArgs e)
        {
        }

        private void Remove_From_Menu_Click(object sender, RoutedEventArgs e)
        {
        }

        private void OpenNewInstance_Click(object sender, RoutedEventArgs e)
        {
            var windowObject = this.DataContext as TaskBarItem;
            if (windowObject != null && windowObject._openWindows.Count > 0)
                windowObject.OpenNewWindow();
        }
	}
}