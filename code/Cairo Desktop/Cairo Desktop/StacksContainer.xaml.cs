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

namespace CairoDesktop {
    /// <summary>
    /// Interaction logic for StacksContainer.xaml
    /// </summary>
    public partial class StacksContainer : UserControl 
    {
        private static DependencyProperty locationsProperty = DependencyProperty.Register("Locations", typeof(InvokingObservableCollection<SystemDirectory>), typeof(StacksContainer), new PropertyMetadata(new InvokingObservableCollection<SystemDirectory>(Dispatcher.CurrentDispatcher)));
        private string configFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CairoStacksConfig.xml";

        public StacksContainer() 
        {
            InitializeComponent();
            try 
            {
                this.deserialize();
            } 
            catch {}

            Locations.CollectionChanged += new NotifyCollectionChangedEventHandler(locations_CollectionChanged);
        }

        public InvokingObservableCollection<SystemDirectory> Locations
        {
            get
            {
                return GetValue(locationsProperty) as InvokingObservableCollection<SystemDirectory>;
            }
            set
            {
                if (!this.Dispatcher.CheckAccess())
                {
                    this.Dispatcher.Invoke((Action)(() => this.Locations = value), null);
                    return;
                }

                SetValue(locationsProperty, value);
            }
        }

        void locations_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.serialize();
        }
        
        private void serialize() {
            List<String> locationPaths = new List<String>();
            foreach (SystemDirectory dir in Locations) {
                locationPaths.Add(dir.FullName);
            }
            System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "    ";
            System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(configFile, settings);
            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<String>));
            serializer.Serialize(writer, locationPaths);
            writer.Close();
        }
        
        private void deserialize() {
            Locations.Clear();
            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<String>));
            System.Xml.XmlReader reader = System.Xml.XmlReader.Create(configFile);
            List<String> locationPaths = serializer.Deserialize(reader) as List<String>;
            foreach (String path in locationPaths) {
                Locations.Add(new SystemDirectory(path, this.Dispatcher));
            }
            reader.Close();
        }

        private void locationDisplay_DragEnter(object sender, DragEventArgs e)
        {
            String[] formats = e.Data.GetFormats(true);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }

            e.Handled = true;
        }

        private void locationDisplay_Drop(object sender, DragEventArgs e)
        {
            String[] fileNames = e.Data.GetData(DataFormats.FileDrop) as String[];
            if (fileNames != null) 
            {
                foreach (String fileName in fileNames)
                {
                    // Only add if the 'file' is a Directory
                    if (System.IO.Directory.Exists(fileName))
                    {
                        Locations.Add(new SystemDirectory(fileName, Dispatcher.CurrentDispatcher));
                    }
                }
            }

            e.Handled = true;
        }

        private void File_Button_Click(object sender, RoutedEventArgs e)
        {
            Button senderButton = sender as Button;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.FileName = senderButton.CommandParameter as String;
            try 
            {
                proc.Start();
            } 
            catch 
            {
                // No 'Open' command associated with this filetype in the registry
                ShowOpenWithDialog(proc.StartInfo.FileName);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) 
        {
            MenuItem item = sender as MenuItem;
            String fileName = item.CommandParameter as String;
            if (item.Header as String == "Open with...")
            {
                ShowOpenWithDialog(fileName);
                return;
            }

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.FileName = fileName;
            proc.StartInfo.Verb = item.Header as String;
            try 
            {
                proc.Start();
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Error running the {0} verb on {1}. ({2})",item.Header,fileName,ex.Message));
            }
        }

        /// <summary>
        /// Calls the Windows OpenWith dialog (shell32.dll) to open the file specified.
        /// </summary>
        /// <param name="fileName">Path to the file to open</param>
        private void ShowOpenWithDialog(string fileName) 
        {
            System.Diagnostics.Process owProc = new System.Diagnostics.Process();
            owProc.StartInfo.UseShellExecute = true;
            owProc.StartInfo.FileName = Environment.GetEnvironmentVariable("WINDIR") + @"\system32\rundll32.exe";
            owProc.StartInfo.Arguments =
                @"C:\WINDOWS\system32\shell32.dll,OpenAs_RunDLL " + fileName;
            owProc.Start();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            Locations.Remove((sender as MenuItem).CommandParameter as SystemDirectory);
        }

        private void Open_Click (object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine (sender.GetType ().ToString ());
            openDir ((sender as MenuItem).CommandParameter.ToString ());
        }

        private void Rename_Click (object sender, RoutedEventArgs e)
        {
            string result = CairoMessage.ShowTextField ("Rename?", "Rename");
            if (string.IsNullOrEmpty(result))
                return;
            string[] pieces = (((CairoDesktop.SystemDirectory)(sender as MenuItem).CommandParameter).DirectoryInfo.FullName).Split(System.IO.Path.DirectorySeparatorChar);
            List<string> parts = new List<string>(pieces);
            parts.RemoveAt(pieces.Length - 1);
            string rootPath = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), parts.ToArray());
            string newPath = System.IO.Path.Combine (rootPath, result);
            try
            {
                Directory.Move (((sender as MenuItem).CommandParameter as CairoDesktop.SystemDirectory).DirectoryInfo.FullName, newPath);
            }
            catch
            {
            }
        }
        
        /// <summary>
        /// Launches the FileManager specified in the application Settings object to the specified directory.
        /// </summary>
        /// <param name="directoryPath">Directory to open.</param>
        private void openDir(String directoryPath) 
        {
            CairoExplorer.CairoExplorerWindow window = new CairoExplorer.CairoExplorerWindow(directoryPath);
            window.Show();
        }
    }
}
