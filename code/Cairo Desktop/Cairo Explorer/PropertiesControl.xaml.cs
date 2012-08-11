using System;
using System.Collections.Generic;
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

namespace CairoExplorer
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class PropertiesControl : UserControl
    {
        private WrapperFileSystemInfo _file;
        private object _caller;

        public string CurrentPath = "";

        public PropertiesControl()
        {
            InitializeComponent();
        }

        public void InitWithFile(WrapperFileSystemInfo file, object caller)
        {
            _file = file;
            _caller = caller;

            Name.Text = file.NameNoExtension;
            Type.Text = file.Type.BaseExtension;
            Size.Text = file.Size;
            DateModified.Text = "Last Modified " + file.Info.DateModified;
            Title.Text = file.Type.Folder ? "Folder" : "File";
            Icon.Source = file.Icon;
        }
    }
}
