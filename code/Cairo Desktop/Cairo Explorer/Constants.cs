using System;
using System.Collections.Generic;
//using System.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using GlassLib;

namespace CairoExplorer
{
    public class Constants : DependencyObject
    {
        public static readonly DependencyProperty FilterStringProperty =
            DependencyProperty.Register("Branding", typeof(string),
            typeof(Constants), new UIPropertyMetadata("Cairo Explorer"));
        
        private static string branding = "Cairo Explorer";

        public static string Branding
        {
            get { return branding; }
            set { branding = value; }
        }
        public static Constants Instance { get; private set; }

        static Constants() 
        {
            Instance = new Constants();
        }
    }
}
