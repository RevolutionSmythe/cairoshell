using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace CairoExplorer
{
    public delegate void AsyncDirectorySizeCallBack(WrapperFileSystemInfo f, double size, object param);
    public delegate void RoutedEventHandlerListView(object sender, RoutedEventArgs e, FrameworkElement view);
    /// <summary>
    /// Interaction logic for CairoExplorerWindow.xaml
    /// </summary>
    public partial class CairoExplorerWindow : Window
    {
        #region Declares

        private string _currentWindowPath = "Home";
        private string _currentWindowName = "Home";
        private Flow _currentFlow = Flow.Detail;
        private const string _blankSpace = "        ";
        private static ThreadPool ThreadPool = new ThreadPool();
        private bool _hasFinishedInit = false;

        public Brush Color
        {
            get { return Settings.TextColorBrush; }
        }

        private Stack<string> _backQueue = new Stack<string>();
        private Stack<string> _forwardQueue = new Stack<string>();
        private const int SpaceKey = 550;
        public static int ByteCount = 1024;
        private string _sortingByName = "Name";
        private bool _sortingBySort = false;
        private string[] _specialPaths = new string[7] { "Favorites", "Home", "//", "Desktop", "Computer", "My Computer", "Network" };

        private List<WrapperFileSystemInfo> _fileSysList = new List<WrapperFileSystemInfo>();

        #endregion

        #region Enums

        public enum Flow
        {
            Thumbnail,
            CoverFlow,
            Detail,
            Column
        }

        public enum FlowRebuild
        {
            NoRebuild,
            NoRebuildPathChanged,
            NoRebuildPathAddition,
            RebuildAllFlows
        }

        private enum Notifications
        {
            FolderChanged,
            ChangingFolder
        }

        #endregion

        #region Constructors/Init

        public CairoExplorerWindow()
        {
            Init();
        }

        public CairoExplorerWindow(string filePath)
        {
            Init(filePath);
        }

        private void Init(string filePath = "Home")
        {
            _currentWindowPath = filePath;
            SourceInitialized += new EventHandler(win_SourceInitialized);
            InitializeComponent();
            Opacity = 0;
            _hasFinishedInit = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //foreach (var i in DisplaySidebarDirectoryTreeView("//"))
            //    DriveTreeView.Items.Add(i);

            PopulateDevices();

            PopulateFavorites();

            SetUpColumnView();
            SetUpThumbnailView();
            PushNotification(Notifications.ChangingFolder, _currentWindowPath);
            ActivateTopBarIcons();

            if(!PreviewControls.Preview.AdditionalPaths.ContainsKey("*"))
                PreviewControls.Preview.AdditionalPaths.Add("*", typeof(PropertiesPreview));

            FadeInWindowAnimation();


            AddAutoHideScrollViewer(ColumnView);
        }

        private void SetUpThumbnailView()
        {
            AddAutoHideScrollViewer(ThumbnailDock);
            ThumbnailScroll.ScrollChanged += new ScrollChangedEventHandler(ThumbnailScroll_ScrollChanged);
            if(Settings.DetailedThumbnailView)
                ThumbnailSize.Minimum = 70;
            else
                ThumbnailSize.Minimum = 7;
        }

        #region Fade In Animation

        private void FadeInWindowAnimation()
        {
            System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(100);
            t.Tick += delegate(object s, EventArgs ee)
            {
                t.Stop();

                var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.WindowFadeInTime));
                anim.From = 0;
                anim.To = 1;
                anim.AccelerationRatio = 0.4;
                anim.DecelerationRatio = 0.6;

                var hsizeAnim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.WindowFadeInTime));
                hsizeAnim.From = Window.Top * .975;
                hsizeAnim.To = Window.Top;
                hsizeAnim.AccelerationRatio = 0.9;
                hsizeAnim.DecelerationRatio = 0.1;

                var lsizeAnim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.WindowFadeInTime));
                lsizeAnim.From = Window.Left * .975;
                lsizeAnim.To = Window.Left;
                lsizeAnim.AccelerationRatio = 0.9;
                lsizeAnim.DecelerationRatio = 0.1;

                var wsizeAnim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.WindowFadeInTime));
                wsizeAnim.From = Window.ActualWidth * .975;
                wsizeAnim.To = Window.ActualWidth;
                wsizeAnim.AccelerationRatio = 0.9;
                wsizeAnim.DecelerationRatio = 0.1;

                var hhsizeAnim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(Settings.WindowFadeInTime));
                hhsizeAnim.From = Window.ActualHeight * .975;
                hhsizeAnim.To = Window.ActualHeight;
                hhsizeAnim.AccelerationRatio = 0.4;
                hhsizeAnim.DecelerationRatio = 0.6;


                this.BeginAnimation(System.Windows.Window.HeightProperty, hhsizeAnim);
                this.BeginAnimation(System.Windows.Window.LeftProperty, lsizeAnim);
                this.BeginAnimation(System.Windows.Window.TopProperty, hsizeAnim);
                this.BeginAnimation(UIElement.OpacityProperty, anim);
                this.BeginAnimation(System.Windows.Window.WidthProperty, wsizeAnim);
                

                Window.Topmost = true;
                System.Windows.Threading.DispatcherTimer tt = new System.Windows.Threading.DispatcherTimer();
                tt.Interval = TimeSpan.FromSeconds(Settings.WindowFadeInTime);
                tt.Tick += delegate(object ss, EventArgs eee)
                {
                    Window.Topmost = false;
                    tt.Stop();
                };
                tt.Start();
            };
            t.Start();
        }

        #endregion

        private void SetUpColumnView()
        {
            ColumnView.Visibility = System.Windows.Visibility.Visible;
            ColumnView.ContextMenu = new ContextMenu();
            ColumnView.ItemContainerGenerator.StatusChanged += new EventHandler(ColumnView_ContainerStatusChanged);

            var gridView = ColumnView.View as GridView;
            //Add a few extra spaces just to add some additional space to the grid (it looks way better)
            AddColumn(gridView, "X items, details view  ", new TextBindingInfo() { Bind = "Info.NameWithoutExtension", FontSize = 16 }, 350, true, 32, "Icon", new TextBindingInfo() { Bind = "Type.Extension", FontSize = 16, TextColor = new SolidColorBrush(Colors.Gray) });
            AddColumn(gridView, "date modified  ", new TextBindingInfo() { Bind = "Info.DateModifiedString", FontSize = 16 });
            AddColumn(gridView, "type  ", new TextBindingInfo() { Bind = "Type.BaseExtension", FontSize = 16 });
            AddColumn(gridView, "size  ", new TextBindingInfo() { Bind = "Size", FontSize = 16 }, 80);

            List<GridViewColumnHeader> columns = GetVisualChildCollection<GridViewColumnHeader>(ColumnView);
            foreach (GridViewColumnHeader col in columns)
            {
                if (col.Column != null)
                {
                    col.Width = Double.NaN;
                    col.Height = 28;
                    col.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                    col.FontStyle = FontStyles.Italic;
                    col.FontSize = 18;
                    col.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
        }

        private class TextBindingInfo
        {
            public string Bind;
            public FontWeight FontWeight = FontWeights.Normal;
            public double FontSize = -1;
            public FontStretch FontStretch = FontStretches.Normal;
            public Brush TextColor = null;
            public FontStyle FontStyle = FontStyles.Normal;
        }

        private void AddColumn(GridView gridView, string header, TextBindingInfo bindingInfo, double width = 0, bool image = false, double imageWidth = 0, string imageBinding = "", TextBindingInfo secondaryBinding = null)
        {
            ExtraGridViewColumn gvc = new ExtraGridViewColumn();
            gvc.Header = header;

            FrameworkElementFactory dp = new FrameworkElementFactory(typeof(DockPanel));
            dp.SetValue(DockPanel.LastChildFillProperty, true);
            if (image)
            {
                FrameworkElementFactory icon = new FrameworkElementFactory(typeof(Image));
                icon.SetBinding(Image.SourceProperty, new Binding(imageBinding));
                icon.SetValue(Image.WidthProperty, imageWidth);
                icon.SetValue(Image.HeightProperty, imageWidth);
                icon.SetValue(Image.MarginProperty, new Thickness(0, 0, 5, 0));
                icon.SetValue(Grid.ColumnProperty, 0);
                dp.AppendChild(icon);
            }

            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding(bindingInfo.Bind));
            if (bindingInfo.FontSize > 0)
                tb.SetValue(TextBlock.FontSizeProperty, bindingInfo.FontSize);
            tb.SetValue(TextBlock.FontWeightProperty, bindingInfo.FontWeight);
            tb.SetValue(TextBlock.FontStretchProperty, bindingInfo.FontStretch);
            tb.SetValue(TextBlock.FontStyleProperty, bindingInfo.FontStyle);
            if(bindingInfo.TextColor != null)
                tb.SetValue(TextBlock.ForegroundProperty, bindingInfo.TextColor);
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
            tb.SetValue(Grid.ColumnProperty, 1);
            dp.AppendChild(tb);
            if (secondaryBinding != null)
            {
                tb = new FrameworkElementFactory(typeof(TextBlock));
                tb.SetBinding(TextBlock.TextProperty, new Binding(secondaryBinding.Bind));
                if(secondaryBinding.FontSize > 0)
                    tb.SetValue(TextBlock.FontSizeProperty, secondaryBinding.FontSize);
                tb.SetValue(TextBlock.FontWeightProperty, secondaryBinding.FontWeight);
                tb.SetValue(TextBlock.FontStretchProperty, secondaryBinding.FontStretch);
                tb.SetValue(TextBlock.FontStyleProperty, secondaryBinding.FontStyle);
                if (secondaryBinding.TextColor != null)
                    tb.SetValue(TextBlock.ForegroundProperty, secondaryBinding.TextColor);
                tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
                tb.SetValue(Grid.ColumnProperty, 1);
                dp.AppendChild(tb);
            }
            DataTemplate dt = new DataTemplate();
            dt.VisualTree = dp;
            gvc.CellTemplate = dt;

            if (width != 0)
                gvc.Width = width;
            gridView.Columns.Add(gvc);
        }

        #endregion

        #region Devices Sidebar

        private bool _hasAddedDeviceStatusChangedEvent = false;
        private void PopulateDevices()
        {
            #region Factory
            FrameworkElementFactory dp = new FrameworkElementFactory(typeof(DockPanel));
            dp.SetValue(DockPanel.LastChildFillProperty, true);
            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
            tb.SetValue(TextBlock.MarginProperty, new Thickness(78, 0, 0, 0));
            DataTemplate dt = new DataTemplate();
            dp.AppendChild(tb);
            dt.VisualTree = dp;
            DeviceList.ItemTemplate = dt;
            if (!_hasAddedDeviceStatusChangedEvent)
            {
                _hasAddedDeviceStatusChangedEvent = true;
                DeviceList.ItemContainerGenerator.StatusChanged += new EventHandler(DeviceItemContainerGenerator_StatusChanged);
            }
            #endregion

            DeviceList.ItemsSource = new ObservableCollection<DriveInfo>(DriveInfo.GetDrives());
        }

        void DeviceItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator gen = sender as ItemContainerGenerator;
            if (gen.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < DeviceList.Items.Count; i++)
                {
                    ListViewItem item = gen.ContainerFromIndex(i) as ListViewItem;
                    if (item != null)
                    {
                        item.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(Deviceitem_PreviewMouseLeftButtonDown);
                        SetFont(item);
                    }
                }
            }
        }

        private int _lastDevicePreviewMouseDown = 0;
        void Deviceitem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_lastDevicePreviewMouseDown == e.Timestamp)
                return;
            _lastDevicePreviewMouseDown = e.Timestamp;
            ListViewItem item = (ListViewItem)sender;
            NavigateTo(item.Content.ToString());
        }

        #endregion

        #region Favorites

        private List<Favorite> _favorites = new List<Favorite>();

        private void PopulateFavorites()
        {
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            userName = userName.Split('\\')[1];
            _favorites.Add(new Favorite() { Name = "Home", Path = "Favorites" });
            _favorites.Add(new Favorite() { Name = "My Computer", Path = "//" });
            _favorites.Add(new Favorite() { Name = "Network Places", Path=Environment.GetFolderPath(Environment.SpecialFolder.NetworkShortcuts) });
            _favorites.Add(new Favorite() { Name = userName, Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) });
            _favorites.Add(new Favorite() { Name = "Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) });
            _favorites.Add(new Favorite() { Name = "Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) });
            _favorites.Add(new Favorite() { Name = "Downloads", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads") });
            _favorites.Add(new Favorite() { Name = "Dropbox", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox") });
            _favorites.Add(new Favorite() { Name = "Music", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) });
            _favorites.Add(new Favorite() { Name = "Pictures", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) });
            _favorites.Add(new Favorite() { Name = "Videos", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) });
            BuildFavoritesPanel();

            FavoritesList.ContextMenu = new ContextMenu();
            FavoritesList.ContextMenuOpening += new ContextMenuEventHandler(FavoritesList_ContextMenuOpening);
        }

        private bool _hasAddedFavoritesStatusChangedEvent = false;
        private void BuildFavoritesPanel()
        {
            #region Factory
            /*FrameworkElementFactory dp = new FrameworkElementFactory(typeof(DockPanel));
            dp.SetValue(DockPanel.LastChildFillProperty, true);
            double iconSize = 16.0;
            FrameworkElementFactory icon = new FrameworkElementFactory(typeof(Image));
            icon.SetBinding(Image.SourceProperty, new Binding("Icon"));
            icon.SetValue(Image.WidthProperty, iconSize);
            icon.SetValue(Image.HeightProperty, iconSize);
            icon.SetValue(Image.MarginProperty, new Thickness(15, 0, 5, 0));
            dp.AppendChild(icon);*/
            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
            tb.SetValue(TextBlock.MarginProperty, new Thickness(15, 0, 0, 0));
            DataTemplate dt = new DataTemplate();
            //dp.AppendChild(tb);
            //dt.VisualTree = dp;
            dt.VisualTree = tb;
            FavoritesList.ItemTemplate = dt;
            #endregion
            if (!_hasAddedFavoritesStatusChangedEvent)
            {
                _hasAddedFavoritesStatusChangedEvent = true;
                FavoritesList.ItemContainerGenerator.StatusChanged += new EventHandler(ItemContainerGenerator_StatusChanged);
            }
            FavoritesList.ItemsSource = _favorites;
        }

        void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator gen = sender as ItemContainerGenerator;
            if (gen.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < FavoritesList.Items.Count; i++)
                {
                    ListViewItem item = gen.ContainerFromIndex(i) as ListViewItem;
                    if (item != null)
                    {
                        item.PreviewMouseLeftButtonDown -= new MouseButtonEventHandler(item_PreviewMouseLeftButtonDown);
                        item.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(item_PreviewMouseLeftButtonDown);
                        SetFont(item);
                    }
                }
            }
        }

        void FavoritesList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            FavoritesList.ContextMenu.Items.Clear();
            FavoritesList.ContextMenu.Items.Add(createMenuItem("Remove Favorite", favoritesList_ContextMenu, FavoritesList));
        }

        void favoritesList_ContextMenu(object sender, RoutedEventArgs e, FrameworkElement view)
        {
            MenuItem menuItem = sender as MenuItem;
            switch (menuItem.Header.ToString())
            {
                case "Remove Favorite":
                    Favorite f = FavoritesList.SelectedItem as Favorite;
                    _favorites.Remove(f);
                    BuildFavoritesPanel();
                    break;
                default:
                    break;
            }
        }

        private int _lastFavoritePreviewMouseDown = 0;
        void item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_lastFavoritePreviewMouseDown == e.Timestamp)
                return;
            _lastFavoritePreviewMouseDown = e.Timestamp;
            ListViewItem item = (ListViewItem)sender;
            Favorite fav = item.Content as Favorite;
            if (fav == null)
                throw new InvalidCastException();
            NavigateTo(fav);
        }

        private class Favorite
        {
            public string Name
            {
                get;
                set;
            }

            public BitmapImage Icon
            {
                get
                {
                    return new WrapperFileSystemInfo(new GenericFileSystemInfo(Path, Path, DateTime.Now)).Icon;
                }
            }

            public string Path
            {
                get;
                set;
            }
        }

        private Favorite GetFavorite(string name)
        {
            foreach (var fav in _favorites)
            {
                if (fav.Name == name.Replace(_blankSpace, ""))
                    return fav;
            }
            return null;
        }

        private void NavigateTo(Favorite f)
        {
            if (f == null)
                return;

            if (_currentFlow == Flow.Column)
            {
                _columnViewPath.Clear();
                _columnViewPath.Add(f.Path);
            }
            NavigateTo(f.Path, FlowRebuild.NoRebuildPathChanged, true, f.Name);
        }

        #endregion

        #region Build Sidebar Viewing Panel

        private List<DirectoryTreeViewItem> DisplaySidebarDirectoryTreeView(string p)
        {
            List<DirectoryTreeViewItem> r = new List<DirectoryTreeViewItem>();
            if (p == "//")
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives)
                {
                    int f = 0;
                    var o = BuildFolderTree(new DirectoryInfo(d.Name), f);
                    if (o != null)
                        r.Add(o);
                }
            }
            else
            {
                DirectoryInfo i = new DirectoryInfo(p);
                int f = 0;
                var o = BuildFolderTree(i, f);
                if (o != null)
                    r.Add(o);
            }
            return r;
        }

        private DirectoryTreeViewItem BuildFolderTree(DirectoryInfo i, int f)
        {
            if (f > 1)
                return null;
            DirectoryTreeViewItem item = BuildTreeViewItem(i.FullName, i.Name);
            item.Expanded += new RoutedEventHandler(item_Expanded);
            try
            {
                foreach (string directory in Directory.GetDirectories(i.FullName))
                {
                    DirectoryInfo folder = new DirectoryInfo(directory);
                    if ((folder.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                    object o = BuildFolderTree(folder, f + 1);
                    if (o != null)
                        item.Items.Add(o);
                }
            }
            catch { }
            return item;
        }

        private DirectoryTreeViewItem BuildTreeViewItem(string path, string title)
        {
            var item = new DirectoryTreeViewItem() { Header = title, Path = path };
            SetFont(item);
            item.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(item_MouseDoubleClick);
            return item;
        }

        private void SetFont(Control item)
        {
            item.Foreground = Settings.TextColorBrush;
            /*item.FontFamily = new FontFamily("Calibri");
            item.FontSize = 15;
            item.FontWeight = FontWeights.SemiBold;
            item.FontStretch = FontStretches.UltraCondensed;*/
            item.FontSize = 20;
            item.FontWeight = FontWeights.Thin;
            item.FontStretch = FontStretches.ExtraExpanded;
            if (item is ListViewItem)
            {
                item.Height = 30;
                item.Padding = new Thickness();
            }
        }
        private void SetFont(TextBlock item)
        {
            item.Foreground = Settings.TextColorBrush;
            item.FontSize = 20;
            item.FontWeight = FontWeights.Thin;
            item.FontStretch = FontStretches.ExtraExpanded;
        }

        private DateTime _lastTime;

        void item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DirectoryTreeViewItem item = (DirectoryTreeViewItem)sender;
            if ((DateTime.Now - _lastTime).TotalMilliseconds < 250)
                return;
            _lastTime = DateTime.Now;
            PushNotification(Notifications.ChangingFolder, item.Path);
        }

        private List<string> previouslyExpanded = new List<string>();

        void item_Expanded(object sender, RoutedEventArgs e)
        {
            DirectoryTreeViewItem item = (DirectoryTreeViewItem)sender;

            if (previouslyExpanded.Contains(item.Path))
                return;
            previouslyExpanded.Add(item.Path);

            //item = GetRealSender(item, DriveTreeView.Items);
            List<DirectoryTreeViewItem> items = new List<DirectoryTreeViewItem>();
            foreach (DirectoryTreeViewItem i in item.Items)
            {
                items.AddRange(DisplaySidebarDirectoryTreeView(i.Path));
            }
            item.Items.Clear();
            foreach (var i in items)
                item.Items.Add(i);
        }

        private DirectoryTreeViewItem GetRealSender(DirectoryTreeViewItem item, ItemCollection col)
        {
            foreach (DirectoryTreeViewItem i in col)
            {
                if (i.Path == item.Path)
                    return i;
                var o = GetRealSender(item, i.Items);
                if (o != null)
                    return o;
            }
            return null;
        }

        private class DirectoryTreeViewItem : TreeViewItem
        {
            public string Path = "";
            public DirectoryTreeViewItem()
            {
                this.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
            }
        }

        #endregion

        #region Back/Forward/Up handling

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBackFolder();
        }

        private void GoBackFolder()
        {
            if (_backQueue.Count == 0)
                return;
            string newPath = _backQueue.Pop();
            _forwardQueue.Push(_currentWindowPath);
            NavigateTo(newPath, FlowRebuild.NoRebuildPathChanged, false);
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            GoForwardFolder();
        }

        private void GoForwardFolder()
        {
            if (_forwardQueue.Count == 0)
                return;
            string newPath = _forwardQueue.Pop();
            _backQueue.Push(_currentWindowPath);
            NavigateTo(newPath, FlowRebuild.NoRebuildPathChanged, false);
        }

        private void UpdateBackForwardQueues(string path)
        {
            _backQueue.Push(_currentWindowPath);
            _forwardQueue.Clear();
        }

        private void UpFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_specialPaths.Contains(_currentWindowPath))
                return;
            DirectoryInfo info = Directory.GetParent(_currentWindowPath);
            if (info == null)
            {
                PushNotification(Notifications.ChangingFolder, "//");
                return;
            }
            PushNotification(Notifications.ChangingFolder, info.FullName);
        }

        #endregion

        #region Push Notification

        private void PushNotification(Notifications notifications, string p, FlowRebuild flowRebuild = FlowRebuild.NoRebuildPathChanged)
        {
            if (notifications == Notifications.FolderChanged)
            {
                if (string.IsNullOrEmpty(p) || Path.GetFullPath(p) == Path.GetFullPath(_currentWindowPath))
                    UpdateFlows(FlowRebuild.NoRebuild);
            }
            else if (notifications == Notifications.ChangingFolder)
            {
                NavigateTo(p, flowRebuild);
            }
        }

        private void NavigateTo(string path, FlowRebuild flowRebuild = FlowRebuild.NoRebuildPathChanged, bool updateBackForwardQueues = true, string name = "")
        {
            if (updateBackForwardQueues)
                UpdateBackForwardQueues(path);
            _currentWindowPath = path;
            if (name == "")
                name = Path.GetFileName(path);
            if (name == "")
                name = path;
            _currentWindowName = name;
            SearchText.Text = path;

            lock (FavoritesList)
            {
                for (int ind = 0; ind < FavoritesList.Items.Count; ind++)
                {
                    ListViewItem it = FavoritesList.ItemContainerGenerator.ContainerFromIndex(ind) as ListViewItem;
                    if (it != null)
                        it.Foreground = Settings.TextColorBrush;
                }
                int i = 0;
                var item = FavoritesList.ItemsSource.Cast<Favorite>().FirstOrDefault((f) => 
                {
                    i++; return Directory.Exists(f.Path) ? Path.GetFullPath(f.Path) == Path.GetFullPath(path) : f.Path == path;
                });
                if (item != null)
                    (FavoritesList.ItemContainerGenerator.ContainerFromIndex(i-1) 
                        as ListViewItem).Foreground = Settings.SelectedTextColorBrush;
            }

            UpdateFlows(flowRebuild);
            SetWindowTitleByFolder(name);
        }

        #endregion

        #region Update Flows

        private void UpdateFlows(FlowRebuild flowRebuild)
        {
            if (flowRebuild == FlowRebuild.RebuildAllFlows)
            {
                if (_currentFlow != Flow.Thumbnail)
                {
                    ThumbnailSize.Visibility = System.Windows.Visibility.Collapsed;
                    thumbnailSizeText.Visibility = System.Windows.Visibility.Collapsed;
                    ThumbnailScroll.Visibility = System.Windows.Visibility.Collapsed;
                    ThumbnailHideNames.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ThumbnailSize.Visibility = System.Windows.Visibility.Visible;
                    thumbnailSizeText.Visibility = System.Windows.Visibility.Visible;
                    ThumbnailScroll.Visibility = System.Windows.Visibility.Visible;
                    ThumbnailHideNames.Visibility = System.Windows.Visibility.Visible;
                }
                if (_currentFlow != Flow.Detail)
                    ColumnView.Visibility = System.Windows.Visibility.Collapsed;
                else
                    ColumnView.Visibility = System.Windows.Visibility.Visible;
                if (_currentFlow != Flow.CoverFlow)
                {
                    CoverFlowSplit.Visibility = System.Windows.Visibility.Collapsed;
                    CoverFlowViewer.Visibility = System.Windows.Visibility.Collapsed;
                    if (_currentFlow != Flow.Detail)
                        ColumnView.Visibility = System.Windows.Visibility.Collapsed;
                    ColumnView.SetValue(Grid.RowProperty, 0);
                    ColumnView.SetValue(Grid.RowSpanProperty, 3);
                }
                else
                {
                    CoverFlowSplit.Visibility = System.Windows.Visibility.Visible;
                    CoverFlowViewer.Visibility = System.Windows.Visibility.Visible;
                    ColumnView.Visibility = System.Windows.Visibility.Visible;
                    ColumnView.SetValue(Grid.RowProperty, 2);
                    ColumnView.SetValue(Grid.RowSpanProperty, 1);
                }
                if (_currentFlow != Flow.Column)
                {
                    foreach (ListView v in _columnViews)
                        ColumnScroller.Children.Remove(v);
                    _columnViews.Clear();
                    _columnViewPath.Clear();
                    ColumnScroller.ColumnDefinitions.Clear();
                    ColumnScroller.Visibility = System.Windows.Visibility.Collapsed;
                    ColumnScroll.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ColumnScroller.Visibility = System.Windows.Visibility.Visible;
                    ColumnScroll.Visibility = System.Windows.Visibility.Visible;
                }
                FixFlowSizes();
            }

            if (flowRebuild == FlowRebuild.NoRebuildPathChanged ||
                flowRebuild == FlowRebuild.NoRebuildPathAddition)
                PreviewControls.Preview.PreviewClose();
            if (_currentFlow == Flow.Thumbnail)
                UpdateThumbnailFlow(flowRebuild);
            if (_currentFlow == Flow.Detail)
                UpdateDetailsFlow(flowRebuild);
            if (_currentFlow == Flow.Column)
                UpdateColumnFlow(flowRebuild);
            if (_currentFlow == Flow.CoverFlow)
                UpdateDetailsFlow(flowRebuild);
        }

        private void FixFlowSizes()
        {
            double width = LayoutRoot.ColumnDefinitions[2].ActualWidth;
            if((int)FolderNameText.GetValue(Grid.ColumnSpanProperty) == 2)
                width += LayoutRoot.ColumnDefinitions[1].ActualWidth;
            if (_currentFlow == Flow.CoverFlow)
            {
                ColumnView.Height = (FolderNameText.ActualHeight - 10) / 2 - 2;
                ColumnView.MaxWidth = (width - 10);
            }
            else
            {
                if (FolderNameText.ActualHeight - 10 < 0)
                    ColumnView.Height = 1;
                else
                    ColumnView.Height = FolderNameText.ActualHeight - 10;
                if (width - 10 < 0)
                    ColumnView.MaxWidth = 1;
                else
                    ColumnView.MaxWidth = width - 10;
            }

            ThumbnailScroll.Width = width - 10;
            ThumbnailScroll.Height = FolderNameText.ActualHeight - 10;
            if ((width - 10) / 2 - 2 < 0)
            {
                CoverFlowViewer.Height = 1;
                CoverFlowViewer.Width = 1;
            }
            else
            {
                CoverFlowViewer.Height = (FolderNameText.ActualHeight - 10) / 2 - 2;
                CoverFlowViewer.Width = (width - 10);
            }
        }

        #endregion

        #region Update Column Flow

        private List<string> _columnViewPath = new List<string>();
        private List<ColumnViewList> _columnViews = new List<ColumnViewList>();

        private void UpdateColumnFlow(FlowRebuild flowRebuild)
        {
            ColumnScroller.Children.Clear();
            List<ColumnViewList> oldColumnView = new List<ColumnViewList>(_columnViews);
            _columnViews.Clear();

            if (_columnViewPath.Count == 0 || flowRebuild == FlowRebuild.NoRebuildPathChanged)
            {
                _columnViewPath.Clear();
                _columnViewPath.Add(_currentWindowPath);
            }
            else if (flowRebuild == FlowRebuild.NoRebuildPathAddition)
            {
                _columnViewPath.Add(_currentWindowPath);
            }

            ColumnScroller.ColumnDefinitions.Clear();

            int colNum = 0, gridColNum = 0;
            foreach (string path in _columnViewPath)
            {
                if (File.Exists(path))
                    continue;
                ColumnViewList view = new ColumnViewList();
                ScrollViewer scroll = new ScrollViewer();

                string p = path;
                foreach (var t in GetFilesAndFoldersForDirectory(ref p))
                {
                    view.Items.Add(t);
                }

                #region Build Factory

                FrameworkElementFactory dp = new FrameworkElementFactory(typeof(DockPanel));
                dp.SetValue(DockPanel.LastChildFillProperty, true);
                double iconSize = 16.0;
                FrameworkElementFactory icon = new FrameworkElementFactory(typeof(Image));
                icon.SetBinding(Image.SourceProperty, new Binding("Icon"));
                icon.SetValue(Image.WidthProperty, iconSize);
                icon.SetValue(Image.HeightProperty, iconSize);
                icon.SetValue(Image.MarginProperty, new Thickness(15, 0, 5, 0));
                icon.SetValue(Grid.ColumnProperty, 0);
                dp.AppendChild(icon);
                FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
                tb.SetBinding(TextBlock.TextProperty, new Binding("Info.NameWithoutExtension"));
                tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
                tb.SetValue(Grid.ColumnProperty, 1);
                DataTemplate dt = new DataTemplate();
                dp.AppendChild(tb);
                dt.VisualTree = dp;
                view.ItemTemplate = dt;

                #endregion

                view.ContextMenu = new System.Windows.Controls.ContextMenu();
                view.ContextMenuOpening += ContextMenuOpening;

                view.PreviewMouseDoubleClick += new MouseButtonEventHandler(_columnView_MouseDoubleClick);
                view.ColumnNumber = gridColNum;
                Column_ContainerStatusChanged(view);
                if (oldColumnView.Count > gridColNum)
                    view.SelectedIndex = oldColumnView[gridColNum].SelectedIndex;
                _columnViews.Add(view);
                view.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                view.MinWidth = 10000;
                if (oldColumnView.Count <= gridColNum)
                    view.Width = 200;
                else
                {
                    view.Width = oldColumnView[gridColNum].DesiredSize.Width > 0 ?
                        (((ScrollViewer)oldColumnView[gridColNum].Parent).ScrollableHeight < ((ScrollViewer)oldColumnView[gridColNum].Parent).ViewportHeight ? 0 : 17) +
                        oldColumnView[gridColNum].DesiredSize.Width : 200;
                    scroll.ScrollToVerticalOffset(((ScrollViewer)oldColumnView[gridColNum].Parent).ContentVerticalOffset);
                }

                scroll.Content = view;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroll.Height = FolderNameText.ActualHeight;
                Grid.SetColumn(scroll, colNum++);
                Grid.SetRowSpan(scroll, 3);
                view.PreviewMouseWheel += new MouseWheelEventHandler(view_MouseWheel);
                ColumnScroller.MouseWheel += new MouseWheelEventHandler(view_MouseWheel);
                scroll.MouseWheel += new MouseWheelEventHandler(view_MouseWheel);
                ColumnScroller.Children.Add(scroll);
                ColumnScroller.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(view.Width) });
                gridColNum++;

                view.PreviewKeyDown += ColumnView_KeyDown;
                view.SelectionChanged += new SelectionChangedEventHandler(view_SelectionChanged);
                GridSplitter splitter = new GridSplitter();
                splitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
                splitter.ResizeDirection = GridResizeDirection.Auto;
                splitter.Width = 4;
                ColumnScroller.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(4) });
                Grid.SetColumn(splitter, colNum++);
                ColumnScroller.Children.Add(splitter);
                AddAutoHideScrollViewer(view);
            }
            if (File.Exists(_columnViewPath[_columnViewPath.Count - 1]))
            {
                VerticalPropertiesControl c = new VerticalPropertiesControl();
                c.InitWithFile(new WrapperFileSystemInfo(new GenericFileSystemInfo(new FileInfo(_columnViewPath[_columnViewPath.Count - 1]))), Window);
                c.Height = 300;
                c.Width = 300;
                c.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                //c.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                //c.MinWidth = 10000;
                Grid.SetColumn(c, colNum);
                Grid.SetRowSpan(c, 3);
                ColumnScroller.Children.Add(c);
                ColumnScroller.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(c.Width) });
                ColumnScroller.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(c.Width) });
            }
            else
            {
                ColumnViewList last_view = new ColumnViewList();
                last_view.ColumnNumber = gridColNum;
                last_view.Width = 200;
                _columnViews.Add(last_view);
                last_view.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                last_view.MinWidth = 10000;
                ScrollViewer scroll = new ScrollViewer();
                scroll.Content = last_view;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroll.Height = FolderNameText.ActualHeight;
                Grid.SetColumn(scroll, colNum);
                Grid.SetRowSpan(scroll, 3);
                ColumnScroller.Children.Add(scroll);
                ColumnScroller.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(200) });
                AddAutoHideScrollViewer(last_view);
            }

            if (gridColNum * 200 > FolderNameText.RenderSize.Width)
                ColumnScroll.ScrollToRightEnd();
        }

        private void ColumnView_ContainerStatusChanged(object sender, EventArgs e)
        {
            if (ColumnView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < ColumnView.Items.Count; i++)
                {
                    ListViewItem item = ColumnView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                    if (item != null)
                    {
                        item.Height = 40;
                        if (i % 2 == 0)
                            item.Background = new SolidColorBrush(Settings.ItemBackColor1);
                        else
                            item.Background = new SolidColorBrush(Settings.ItemBackColor2);
                        item.FontSize = Settings.FontSize;
                        item.FontFamily = new FontFamily(Settings.FontName);
                        item.FontStretch = Settings.FontStretch;
                    }
                }
            }
        }

        void Column_ContainerStatusChanged(ListView l)
        {
            l.ItemContainerGenerator.StatusChanged += delegate(object sender, EventArgs e)
            {
                if (l.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    for (int i = 0; i < l.Items.Count; i++)
                    {
                        ListViewItem item = l.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                        if (item != null)
                        {
                            if (i % 2 == 0)
                                item.Background = new SolidColorBrush(Settings.ItemBackColor1);
                            else
                                item.Background = new SolidColorBrush(Settings.ItemBackColor2);
                            item.Foreground = new SolidColorBrush(Colors.Black);
                            item.FontSize = Settings.FontSize;
                            item.FontFamily = new FontFamily(Settings.FontName);
                            item.FontStretch = Settings.FontStretch;
                        }
                    }
                }
            };
        }

        void view_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ColumnViewList col = sender as ColumnViewList;
            Grid grid = sender as Grid;
            ScrollViewer scroll = col == null ? grid.Parent as ScrollViewer : col.Parent as ScrollViewer;
            scroll.ScrollToVerticalOffset(scroll.ContentVerticalOffset + (e.Delta < 0 ? 1 : -1) * 50);
        }

        void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView view = sender as ListView;
            if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed)
            {
                if (view.SelectedItem != null)
                {
                    if (view.Name != "" || File.Exists(((WrapperFileSystemInfo)view.SelectedItem).Info.Path))
                    {
                        PreviewControls.Preview.PreviewFileChanged(((WrapperFileSystemInfo)view.SelectedItem).Info.Path, ((WrapperFileSystemInfo)view.SelectedItem), this, GetScreenPointForPreview(view));

                        if (_currentFlow == Flow.Column)
                        {
                            ColumnViewList cview = sender as ColumnViewList;
                            _columnViewPath.RemoveRange(cview.ColumnNumber + 1, _columnViewPath.Count - cview.ColumnNumber - 1);
                            _columnViewPath.Add((cview.SelectedItem as WrapperFileSystemInfo).Info.Path);
                            UpdateFlows(FlowRebuild.NoRebuild);
                        }
                    }
                    else
                        _columnView_MouseDoubleClick(sender, null);
                }
            }
        }

        void _columnView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ColumnViewList view = sender as ColumnViewList;
            if (view == null || view.SelectedItem == null)
                return;

            UpdateBackForwardQueues(_currentWindowName);
            _currentWindowPath = ((WrapperFileSystemInfo)view.SelectedItem).Info.Path;
            var file = (view.SelectedItem as WrapperFileSystemInfo);
            if (file.Type.File)
                OpenFile(file.Info.Path);
            else if (file.Type.Special)
            {
                _columnViewPath.Clear();
                _columnViewPath.Add((view.SelectedItem as WrapperFileSystemInfo).Info.Path);
                UpdateFlows(FlowRebuild.NoRebuild);
            }
            else
            {
                if (file.Info.IsReady)
                {
                    _columnViewPath.RemoveRange(view.ColumnNumber + 1, _columnViewPath.Count - view.ColumnNumber - 1);
                    _columnViewPath.Add(file.Info.Path);
                    UpdateFlows(FlowRebuild.NoRebuild);
                }
                else
                    MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
            }
        }

        private class ColumnViewList : ListView
        {
            public int ColumnNumber = 0;
        }

        #endregion

        #region Update Details Flow

        private void UpdateDetailsFlow(FlowRebuild flowRebuild)
        {
            _fileSysList = GetFilesAndFoldersForDirectory(ref _currentWindowPath, AsyncSizeUpdate);
            foreach(var f in new List<WrapperFileSystemInfo>(_fileSysList))
                f.GetSizeAsync();

            GridView view = ColumnView.View as GridView;
            view.Columns[0].Header = _fileSysList.Count + " items ";

            ColumnView.ItemsSource = _fileSysList;
            ColumnSort(ColumnView, _sortingByName, _sortingBySort);
            if(ColumnView.Items.Count > 0)
                ColumnView.ScrollIntoView(ColumnView.Items[0]);
        }

        private void AsyncSizeUpdate(WrapperFileSystemInfo f, double size, object v)
        {
            LayoutRoot.Dispatcher.Invoke(new Action(delegate()
            {
                int index = GetIndex(_fileSysList, f);
                if (index >= 0)
                {
                    _fileSysList.RemoveAt(index);
                    f.ByteSize = size;
                    _fileSysList.Insert(index, f);
                    ColumnView.ItemsSource = _fileSysList;
                }
                else
                {
                }
            }));
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader target =
              e.OriginalSource as GridViewColumnHeader;

            if (target == null || target.Column == null)
                return;

            target.Background = new LinearGradientBrush(new GradientStopCollection(new List<GradientStop>(new GradientStop[3] { new GradientStop(Colors.White, 0), new GradientStop(Colors.LightBlue, 0.4091), new GradientStop(Colors.White, 1) })), new Point(0, 0), new Point(0, 1));
            List<GridViewColumnHeader> allGridViewColumnHeaders = GetVisualChildCollection<GridViewColumnHeader>(ColumnView);
            foreach (GridViewColumnHeader columnHeader in allGridViewColumnHeaders)
            {
                if (columnHeader.Column != null && columnHeader.Column.Header != target.Column.Header)
                {
                    columnHeader.Background = new LinearGradientBrush(new GradientStopCollection(new List<GradientStop>(new GradientStop[3] { new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFFFF"),0), new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFFFF"),0.4091), new GradientStop((Color)ColorConverter.ConvertFromString("#FFF7F8F9"),1) })),new Point(0,0), new Point(0,1));
                }
            }
            _sortingByName = target.Column.Header.ToString();
            _sortingBySort = !((ExtraGridViewColumn)target.Column).sortingUp;
            ColumnSort(ColumnView, target.Column.Header.ToString(), !((ExtraGridViewColumn)target.Column).sortingUp);
            ((ExtraGridViewColumn)target.Column).sortingUp = !((ExtraGridViewColumn)target.Column).sortingUp;
        }

        void ColumnView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListView list = sender as ListView;

            if (e.MouseDevice.Target is GridViewColumnHeader)
            {
                GridViewColumnHeader target = e.MouseDevice.Target as GridViewColumnHeader;
                _sortingByName = target.Column.Header.ToString();
                _sortingBySort = !((ExtraGridViewColumn)target.Column).sortingUp;
                ColumnSort(ColumnView, target.Column.Header.ToString(), !((ExtraGridViewColumn)target.Column).sortingUp);
                ((ExtraGridViewColumn)target.Column).sortingUp = !((ExtraGridViewColumn)target.Column).sortingUp;
            }
            else if (e.MouseDevice.Target is Border && !(((Border)e.MouseDevice.Target).Child is GridViewRowPresenter) && ((Border)e.MouseDevice.Target).Child is System.Windows.Shapes.Rectangle)
            {
                //Autosizing of the headers, just pass it through
            }
            else
            {
                if (ColumnView.SelectedItem == null)
                    return;
                var item = (ColumnView.SelectedItem as WrapperFileSystemInfo);
                if (item.Type.File)
                    OpenFile(item.Info.Path);
                else
                {
                    if (item.Info.IsReady)
                        PushNotification(Notifications.ChangingFolder, item.Info.Path);
                    else
                        MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
                }
            }
        }

        private void ColumnSort(ListView view, string name, bool sortUp)
        {
            List<WrapperFileSystemInfo> lists = new List<WrapperFileSystemInfo>(_fileSysList);
            List<WrapperFileSystemInfo> folders = new List<WrapperFileSystemInfo>();
            List<WrapperFileSystemInfo> files = new List<WrapperFileSystemInfo>();

            foreach (var f in lists)
                if (f.Type.File)
                    files.Add(f);
                else
                    folders.Add(f);

            ColumnListSorter(name, folders);
            ColumnListSorter(name, files);

            if (sortUp)
            {
                folders.Reverse();
                files.Reverse();
            }
            folders.AddRange(files);
            view.ItemsSource = new ObservableCollection<WrapperFileSystemInfo>(folders);
        }

        private static void ColumnListSorter(string name, List<WrapperFileSystemInfo> lists)
        {
            name = name.TrimEnd();
            lists.Sort(delegate(WrapperFileSystemInfo a, WrapperFileSystemInfo b)
            {
                if (name == "date modified")
                    if (a.Info.DateModifiedString == b.Info.DateModifiedString)
                        if (a.Type.Drive && b.Type.Drive)
                            return ((DriveFileSystemInfo)a.Info).Drive.Name.CompareTo(((DriveFileSystemInfo)b.Info).Drive.Name);
                        else
                            return a.NameNoExtension.CompareTo(b.NameNoExtension);
                    else
                        return a.Info.DateModified.CompareTo(b.Info.DateModified);
                else if (name == "type")
                    if (a.Type.BaseExtension == b.Type.BaseExtension)
                        if (a.Type.Drive && b.Type.Drive)
                            return ((DriveFileSystemInfo)a.Info).Drive.Name.CompareTo(((DriveFileSystemInfo)b.Info).Drive.Name);
                        else
                            return a.NameNoExtension.CompareTo(b.NameNoExtension);
                    else
                        return a.Type.BaseExtension.CompareTo(b.Type.BaseExtension);
                else if (name == "size")
                    return a.ByteSize.CompareTo(b.ByteSize);
                else
                {
                    if (a.Type.Drive && b.Type.Drive)
                        return ((DriveFileSystemInfo)a.Info).Drive.Name.CompareTo(((DriveFileSystemInfo)b.Info).Drive.Name);
                    else
                        return a.NameNoExtension.CompareTo(b.NameNoExtension);
                }
            });
        }

        #endregion

        #region Thumbnail Flow

        private void UpdateThumbnailFlow(FlowRebuild flowRebuild)
        {
            ThumbnailDock.Children.Clear();
            ThumbnailDock.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            double width = ThumbnailSize.Value * 20;
            double height = ThumbnailSize.Value * 20;
            List<WrapperFileSystemInfo> infos = GetFilesAndFoldersForDirectory(ref _currentWindowPath);
            int col = 0, row = 0;
            int maxCol = (int)Math.Round(FolderNameText.ActualWidth / (width - ThumbnailSize.Value * 1.5)) - 1;
            int rows = (int)Math.Round((double)infos.Count / maxCol) + 1;

            ThumbnailDock.Columns = maxCol;
            foreach (var i in infos)
            {
                var ui = CreateDockPanel(i, width, height);
                if (maxCol != 0)
                    Grid.SetColumn(ui, col++);
                Grid.SetRow(ui, row);
                if (col == maxCol)
                {
                    col = 0;
                    row++;
                }
                ThumbnailDock.Children.Add(ui);
            }
            ThumbnailDock.Rows = row;
        }

        private UIElement CreateDockPanel(WrapperFileSystemInfo info, double height, double width)
        {
            ThumbnailViewElement ele = new ThumbnailViewElement();
            ele.Image.Source = info.GetIcon(IconSize.jumbo);
            ele.File = info;
            ele.Name.Text = info.NameNoExtension;
            if (Settings.ShowThumbnailNames)
                ele.Name.Visibility = System.Windows.Visibility.Visible;
            else
                ele.Name.Visibility = System.Windows.Visibility.Collapsed;
            if (Settings.DetailedThumbnailView)
            {
                ele.Description.Text = info.Info.DateModified.ToFileDateTime();
                ele.Size.Text = info.Size;
                ele.Type.Text = info.Type.BaseExtension;
            }
            else
            {
                ele.Description.Visibility = System.Windows.Visibility.Collapsed;
                ele.Size.Visibility = System.Windows.Visibility.Collapsed;
                ele.Type.Visibility = System.Windows.Visibility.Collapsed;
            }
            if (!Settings.DetailedThumbnailView && !Settings.ShowThumbnailNames)
            {
                Grid.SetColumnSpan(ele.Image, 4);
                Grid.SetRowSpan(ele.Image, 8);
                Grid.SetColumn(ele.Image, 0);
                Grid.SetRow(ele.Image, 0);
                ele.Image.Margin = new Thickness(10, 10, 0, 0);
            }
            else
            {
                Grid.SetColumnSpan(ele.Image, 1);
                Grid.SetRowSpan(ele.Image, 1);
                Grid.SetColumn(ele.Image, 1);
                Grid.SetRow(ele.Image, 1);
                ele.Image.Margin = new Thickness();
            }
            ele.ContextMenu = new System.Windows.Controls.ContextMenu();
            ele.ContextMenuOpening += ContextMenuOpening;
            ele.MouseDoubleClick += new MouseButtonEventHandler(ele_MouseDoubleClick);
            ele.PreviewKeyDown += ColumnView_KeyDown;
            ele.grid1.Width = width;
            ele.grid1.Height = height;
            return ele;
        }

        void ele_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThumbnailViewElement ele = sender as ThumbnailViewElement;
            if (ele != null)
            {
                if (ele.File.Type.File)
                    OpenFile(ele.File.Info.Path);
                else
                {
                    if (ele.File.Info.IsReady)
                        PushNotification(Notifications.ChangingFolder, ele.File.Info.Path);
                    else
                        MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
                }
            }
        }

        #endregion

        #region Context Menu

        new void ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            FrameworkElement view = sender as FrameworkElement;
            var items = GetSelectedFileWrappers(view);
            if (items.Count == 0)
            {
                view.ContextMenu = null;
                return;
            }
            view.ContextMenu = new ContextMenu();
            view.ContextMenu.Items.Add(createMenuItem("Open", item_Click, view));
            if (items[0].Type.File)
                view.ContextMenu.Items.Add(createMenuItem("Preview", item_Click, view));
            if (IsDirectoryOrSpecialFolder(items[0]))
                view.ContextMenu.Items.Add(createMenuItem("Open in new window", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Restore previous versions - N/A", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Cut", item_Click, view));
            view.ContextMenu.Items.Add(createMenuItem("Copy", item_Click, view));
            if (IsDirectoryOrSpecialFolder(items[0]))
                view.ContextMenu.Items.Add(createMenuItem("Paste into", item_Click, view));
            else
                view.ContextMenu.Items.Add(createMenuItem("Paste", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Create shortcut", item_Click, view));
            view.ContextMenu.Items.Add(createMenuItem("Delete", item_Click, view));
            view.ContextMenu.Items.Add(createMenuItem("Rename", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Properties", item_Click, view));
        }

        MenuItem createMenuItem(string text, RoutedEventHandlerListView handler, FrameworkElement view)
        {
            MenuItem item = new MenuItem() { Header = text };
            item.Click += delegate(object sender, RoutedEventArgs e)
            {
                handler(sender, e, view);
            };
            return item;
        }

        void item_Click(object sender, RoutedEventArgs e, FrameworkElement view)
        {
            MenuItem menuItem = sender as MenuItem;
            List<string> selectedItems = GetSelectedFiles(view);
            var selectedItemWrappers = GetSelectedFileWrappers(view);
            switch (menuItem.Header.ToString())
            {
                case "Open":
                    if (selectedItems.Count > 0)
                        OpenItem(selectedItems[0], this);
                    break;
                case "Open in new window":
                    if (selectedItems.Count > 0)
                        OpenItem(selectedItems[0], this, true);
                    break;
                case "Preview":
                    if (selectedItems.Count > 0)
                        PreviewControls.Preview.PreviewFile(selectedItems[0], selectedItemWrappers[0], this, GetScreenPointForPreview(view));
                    break;
                case "Cut":
                    CutSelectedFiles(selectedItems);
                    break;
                case "Copy":
                    CopySelectedFiles(selectedItems);
                    break;
                case "Paste":
                    PasteClipboardContents(selectedItems);
                    break;
                case "Paste into":
                    PasteClipboardContents(selectedItems);
                    break;
                case "Delete":
                    DeleteSelectedFiles(selectedItems);
                    break;
                case "Create shortcut":
                    IWshRuntimeLibrary.WshShellClass shell = new IWshRuntimeLibrary.WshShellClass();
                    foreach (string file in selectedItems)
                    {
                        IWshRuntimeLibrary.IWshShortcut MyShortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".lnk"));
                        MyShortcut.TargetPath = file;
                        MyShortcut.Save();
                    }
                    PushNotification(Notifications.FolderChanged, _currentWindowPath);
                    break;
                case "Properties":
                    PreviewSelectedFiles(GetSelectedFileWrappers(view), GetScreenPointForPreview(view));
                    break;
                case "Rename":
                case "Restore previous versions":
                    break;
                default:
                    break;
            }
        }

        public static void OpenItem(string item, CairoExplorerWindow window = null, bool openFolderInNewWindow=false)
        {
            if (File.Exists(item))
                OpenFile(item);
            else
            {
                if (openFolderInNewWindow)
                {
                    CairoExplorerWindow w = new CairoExplorerWindow(item);
                    w.Show();
                }
                else if (window != null)
                    window.PushNotification(Notifications.ChangingFolder, item, FlowRebuild.NoRebuildPathAddition);
            }
        }

        #endregion

        #region File Management

        private bool IsDirectoryOrSpecialFolder(object p)
        {
            return p is WrapperFileSystemInfo && !((WrapperFileSystemInfo)p).Type.File;
        }

        private void DeleteSelectedFiles(List<string> selectedItems = null)
        {
            selectedItems = selectedItems == null ? GetSelectedFiles() : selectedItems;
            if (MessageBox.Show(string.Format("Are you sure you want to delete {1} {0} file{2}?", selectedItems.Count, selectedItems.Count > 1 ? "these" : "this", selectedItems.Count > 1 ? "s" : ""), "Delete?", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                List<string> notificationsRecentlyPushed = new List<string>();
                foreach (string file in selectedItems)
                {
                    if (Directory.Exists(file))
                        Directory.Delete(file, true);
                    else
                        File.Delete(file);
                }
                PushNotification(Notifications.FolderChanged, _currentWindowPath);
            }
        }

        private void PasteClipboardContents(List<string> selectedItems = null)
        {
            string folderBeingPastedInfo = _currentWindowPath;
            selectedItems = selectedItems == null ? GetSelectedFiles() : selectedItems;
            if (selectedItems.Count > 0)
            {
                if (Directory.Exists(selectedItems[0]))
                    folderBeingPastedInfo = selectedItems[0];
            }
            System.Collections.Specialized.StringCollection paste = Clipboard.GetFileDropList();
            PasteFilesIntoFolder(paste, folderBeingPastedInfo);
        }

        private void CutSelectedFiles(List<string> selectedItems = null)
        {
            selectedItems = selectedItems == null ? GetSelectedFiles() : selectedItems;
            System.Collections.Specialized.StringCollection cut = new System.Collections.Specialized.StringCollection();
            cut.Add("Cut");
            cut.AddRange(selectedItems.ToArray());
            Clipboard.SetFileDropList(cut);
        }

        private void CopySelectedFiles(List<string> selectedItems = null)
        {
            selectedItems = selectedItems == null ? GetSelectedFiles() : selectedItems;
            System.Collections.Specialized.StringCollection copy = new System.Collections.Specialized.StringCollection();
            copy.Add("Copy");
            copy.AddRange(selectedItems.ToArray());
            Clipboard.SetFileDropList(copy);
        }

        #endregion

        #region Paste Ability

        private void PasteFilesIntoFolder(System.Collections.Specialized.StringCollection paste, string folderBeingPastedInfo)
        {
            if (paste.Count != 0)
            {
                bool doCut = false;
                if (paste[0] == "Cut")
                    doCut = true;
                paste.RemoveAt(0);
                foreach (string file in paste)
                {
                    bool success = true;
                    string fileName = file;
                    if (File.Exists(Path.Combine(folderBeingPastedInfo, Path.GetFileName(file))))
                    {
                        MessageBoxResult result = MessageBox.Show("File already exists here, overwrite?", "Overwrite?", MessageBoxButton.YesNoCancel);
                        if (result == MessageBoxResult.No)
                            fileName = Path.Combine(folderBeingPastedInfo, Path.GetFileNameWithoutExtension(file) + " (Copy)" + Path.GetExtension(file));
                        else if (result == MessageBoxResult.Cancel)
                            success = false;
                    }
                    if (success)
                    {
                        if (doCut)
                            File.Move(file, Path.Combine(folderBeingPastedInfo, Path.GetFileName(fileName)));
                        else
                            File.Copy(file, Path.Combine(folderBeingPastedInfo, Path.GetFileName(fileName)));
                    }
                }
                PushNotification(Notifications.FolderChanged, folderBeingPastedInfo);
            }
        }

        #endregion

        #region Selected Files

        private List<string> GetSelectedFiles(object view = null)
        {
            List<string> files = new List<string>();
            if (_currentFlow == Flow.Detail ||
                _currentFlow == Flow.CoverFlow)
            {
                foreach (var o in ColumnView.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo).Info.Path);
            }
            if (_currentFlow == Flow.Column)
            {
                int max = _columnViews.Count - 1;
                var column = _columnViews[max];
                if (view as ColumnViewList != null)
                    column = view as ColumnViewList;

                foreach (var o in column.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo).Info.Path);
            }
            if (_currentFlow == Flow.Thumbnail)
            {
                ThumbnailViewElement ele = (ThumbnailViewElement)view;
                files.Add(ele.File.Info.Path);
            }
            return files;
        }

        private List<WrapperFileSystemInfo> GetSelectedFileWrappers(object view = null)
        {
            List<WrapperFileSystemInfo> files = new List<WrapperFileSystemInfo>();
            if (_currentFlow == Flow.Detail ||
                _currentFlow == Flow.CoverFlow)
            {
                foreach (var o in ColumnView.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo));
            }
            if (_currentFlow == Flow.Column)
            {
                int max = _columnViews.Count - 1;
                var column = _columnViews[max];
                if (view as ColumnViewList != null)
                    column = view as ColumnViewList;

                foreach (var o in column.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo));
            }
            if (_currentFlow == Flow.Thumbnail)
            {
                ThumbnailViewElement ele = (ThumbnailViewElement)view;
                files.Add(ele.File);
            }
            return files;
        }

        #endregion

        #region Folder Helpers

        private List<WrapperFileSystemInfo> GetFilesAndFoldersForDirectory(ref string path, AsyncDirectorySizeCallBack AsyncSizeUpdate = null)
        {
            List<WrapperFileSystemInfo> fs = new List<WrapperFileSystemInfo>();
            if (path == "//" || path == "Computer" || path == "My Computer")
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives)
                {
                    if (d.IsReady)
                    {
                        WrapperFileSystemInfo f = new WrapperFileSystemInfo(new DriveFileSystemInfo(d), 0, AsyncSizeUpdate);
                        fs.Add(f);
                    }
                    else
                        fs.Add(new WrapperFileSystemInfo(new DriveFileSystemInfo(d), 0));
                }
                path = "//";
                return fs;
            }
            else if (path == "Favorites" || path == "Home")
            {
                foreach (Favorite f in _favorites)
                {
                    WrapperFileSystemInfo wrapper = null;
                    if (_specialPaths.Contains(f.Path) && f.Name != "Favorites" && f.Name != "Home")
                        wrapper = new WrapperFileSystemInfo(new GenericFileSystemInfo(f.Path, f.Name, DateTime.Now), 0, AsyncSizeUpdate);
                    else if (f.Name != "Favorites" && f.Name != "Home")
                        wrapper = new WrapperFileSystemInfo(new GenericFileSystemInfo(new DirectoryInfo(f.Path)), 0, AsyncSizeUpdate);
                    if (wrapper != null)
                    {
                        fs.Add(wrapper);
                    }
                }
                return fs;
            }
            else if (path == "Desktop")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            else if (path == "Network")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                path = Path.Combine(Path.Combine(Path.Combine(path, "Microsoft"), "Windows"), "Network Shortcuts");
            }

            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    return fs;
                MessageBox.Show("The current location is unavailable, please try again later.", "Not currently available");
                return fs;
            }
            string[] dirs = Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                var d = new DirectoryInfo(dir);
                if (!Settings.ShowHiddenFilesAndFolders)
                    if ((d.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                WrapperFileSystemInfo f = new WrapperFileSystemInfo(new GenericFileSystemInfo(d), 0, AsyncSizeUpdate);
                fs.Add(f);
            }
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                var fi = new FileInfo(file);
                if (!Settings.ShowHiddenFilesAndFolders)
                    if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                WrapperFileSystemInfo f = new WrapperFileSystemInfo(new GenericFileSystemInfo(fi), fi.Length);
                fs.Add(f);
            }
            return fs;
        }

        private double GetFolderSize(DirectoryInfo f)
        {
            return double.PositiveInfinity;
        }

        private static void OpenFile(string file)
        {
            System.Diagnostics.Process.Start(file);
        }

        private void SetWindowTitleByFolder(string text)
        {
            TitleBar.Text = text;
            Window.Title = text + " - " + Constants.Branding;
        }

        /*private void CalcBottomBarStats()
        {
            List<string> selectedFiles = GetSelectedFiles();
            BottomBarItemSelectedFolderText.Text = _currentWindowName;
            if (selectedFiles.Count > 0)
            {
                if (_specialPaths.Contains(_currentWindowPath))
                {
                    BottomBarItemSelectedText.Text = "";
                    return;
                }
                double size = GetSizeOf(selectedFiles.ToArray());
                string sizeAmt = GetFileSize(ref size);
                BottomBarItemSelectedText.Text = string.Format("{0} Item{1} Selected, {2} {3} Total", selectedFiles.Count, selectedFiles.Count != 1 ? "s" : "", size, sizeAmt);
                UpdateFlows(FlowRebuild.BottomBarUpdated);
            }
            else
            {
                if (_specialPaths.Contains(_currentWindowPath))
                {
                    BottomBarItemSelectedText.Text = "";
                    return;
                }
                try
                {
                    string[] files = Directory.GetFiles(_currentWindowPath);
                    int numItems = files.Length + Directory.GetDirectories(_currentWindowPath).Length;
                    double size = GetSizeOf(files);
                    string sizeAmt = GetFileSize(ref size);
                    BottomBarItemSelectedText.Text = string.Format("{0} Item{1}, {2} {3} Total", numItems, numItems != 1 ? "s" : "", size, sizeAmt);
                    UpdateFlows(FlowRebuild.BottomBarUpdated);
                }
                catch { }
            }
        }*/

        public static double GetSizeOf(string[] path)
        {
            double size = 0;
            foreach (string p in path)
                size += GetSizeOf(p);
            return size;
        }

        public static double GetSizeOf(string path)
        {
            if (!File.Exists(path))
                return GetSizeOfDirectory(path);
            else
                return GetSizeOfFiles(new string[1] { path });
        }

        public static void AsyncGetSizeOfDirectory(string _currentWindowPath, WrapperFileSystemInfo f, AsyncDirectorySizeCallBack a = null, object param=null)
        {
            lock (_cache)
            {
                if (_cache.ContainsKey(_currentWindowPath))
                {
                    a(f, _cache[_currentWindowPath], param);
                    return;
                }
            }
            ThreadPool.Push(delegate()
                {
                    Dictionary<string, double> folders = new Dictionary<string, double>();
                    double size = ApplyAllFiles(_currentWindowPath, "*.*");
                    lock (_cache)
                        _cache[_currentWindowPath] = size;
                    if (a != null && size > 0)
                        a(f, size, param);
                });
        }

        private static Dictionary<string, double> _cache = new Dictionary<string, double>();

        private static double ApplyAllFiles(string folder, string searchPattern)
        {
            ThreadPool.CheckClose();
            lock (_cache)
            {
                if (_cache.ContainsKey(folder))
                    return _cache[folder];
            }

            double size = 0;
            try
            {
                foreach (string file in Directory.GetFiles(folder, searchPattern))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch { }
            try
            {
                foreach (string subDir in Directory.GetDirectories(folder))
                {
                    try
                    {
                        size += ApplyAllFiles(subDir, searchPattern);
                    }
                    catch
                    {
                        // swallow, log, whatever
                    }
                }
            }
            catch { }
            lock (_cache)
                _cache[folder] = size;
            return size;
        }

        private static int GetIndex(IEnumerable<WrapperFileSystemInfo> e, WrapperFileSystemInfo file)
        {
            int i = 0;
            foreach (WrapperFileSystemInfo f in e)
            {
                if (f.Info.Path == file.Info.Path)
                    return i;
                i++;
            }
            return -1;
        }

        private static double GetSizeOfDirectory(string _currentWindowPath)
        {
            double size = 0;
            try
            {
                string[] infos = Directory.GetFiles(_currentWindowPath, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string i in infos)
                    try
                    {
                        size += new FileInfo(i).Length;
                    }
                    catch { }
            }
            catch { }
            return size;
        }

        public static string GetFileSize(ref double size)
        {
            string sizeAmt = "KB";
            if (size > ByteCount)
            {
                size /= ByteCount;
                sizeAmt = "MB";
                if (size > ByteCount)
                {
                    size /= ByteCount;
                    size = Math.Round(size, 3);
                    sizeAmt = "GB";
                    if (size > ByteCount)
                    {
                        size /= ByteCount;
                        sizeAmt = "TB";
                        size = Math.Round(size, 2);
                    }
                    else
                        size = Math.Round(size, 1);
                }
                else
                    size = Math.Round(size, 1);
            }
            else
                size = Math.Round(size, 0);
            return sizeAmt;
        }

        public static string GetFileSize(double size)
        {
            double s = size;
            string t = GetFileSize(ref s);
            return s + " " + t;
        }

        private static double GetSizeOfFiles(string[] files)
        {
            double size = 0;
            foreach (string file in files)
            {
                FileInfo f = new FileInfo(file);
                size += f.Length / ByteCount;
            }
            return size;
        }

        #endregion

        #region Search

        private void SearchRun_Click(object sender, RoutedEventArgs e)
        {
            PushNotification(Notifications.ChangingFolder, SearchText.Text);
        }

        private void SearchText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                PushNotification(Notifications.ChangingFolder, SearchText.Text);
        }

        #endregion

        #region Select Flow Type

        private void ThumbnailFlow_Click(object sender, RoutedEventArgs e)
        {
            _currentFlow = Flow.Thumbnail;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        private void CoverflowFlow_Click(object sender, RoutedEventArgs e)
        {
            _currentFlow = Flow.CoverFlow;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        private void ColumnsFlow_Click(object sender, RoutedEventArgs e)
        {
            _currentFlow = Flow.Column;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        private void DetailsFlow_Click(object sender, RoutedEventArgs e)
        {
            _currentFlow = Flow.Detail;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        #endregion

        #region Extra Mouse Buttons

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                //Back
                GoBackFolder();
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                //Forward
                GoForwardFolder();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            foreach (FrameworkElement ele in _scrollingElements)
                ScrollCheck(ele, e);
        }

        private List<FrameworkElement> _scrollingElements = new List<FrameworkElement>();
        private Dictionary<FrameworkElement, DateTime> _currentlyScrollingElements = new Dictionary<FrameworkElement, DateTime>();
        private Dictionary<FrameworkElement, DateTime> _currentlyEndingScrollingElements = new Dictionary<FrameworkElement, DateTime>();

        private void AddAutoHideScrollViewer(FrameworkElement ele)
        {
            FrameworkElement scroller = ele.Parent is ScrollViewer ? (FrameworkElement)ele.Parent : ele;

            if (scroller is ScrollViewer)
                ((ScrollViewer)scroller).Style = (Style)Resources["SimpleScrollViewer"];

            if (scroller is ListView)
                ((ListView)scroller).PreviewMouseWheel += new MouseWheelEventHandler(CairoExplorerWindow_MouseWheel);
            else if (ele is ListView)
                ((ListView)ele).PreviewMouseWheel += new MouseWheelEventHandler(CairoExplorerWindow_MouseWheel);
            _scrollingElements.Add(scroller);
        }

        void CairoExplorerWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            CairoExplorerWindow_ScrollChanged(sender, null);
        }

        void ThumbnailScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            CairoExplorerWindow_ScrollChanged(sender, null);
        }

        private void CairoExplorerWindow_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            FrameworkElement ele = sender as FrameworkElement;
            FrameworkElement scroller = ele.Parent is ScrollViewer ? (FrameworkElement)ele.Parent : ele;
            if (_scrollingElements.Contains(scroller))
            {
                bool alreadyExists = _currentlyScrollingElements.ContainsKey(scroller);
                _currentlyScrollingElements[scroller] = DateTime.Now.AddSeconds(1.25);
                if (!alreadyExists)
                {
                    ScrollViewer.SetVerticalScrollBarVisibility(scroller, ScrollBarVisibility.Auto);
                    SetScrollViewerAnimation(scroller, true);
                }
            }
        }

        private void SetScrollViewerAnimation(FrameworkElement ele, bool fadeIn, EventHandler handler=null)
        {
            // Get the border of the listview (first child of a listview)

            Decorator border = null;
            Grid scrollGrid = null;
            if (VisualTreeHelper.GetChildrenCount(ele) > 0)
            {
                border = VisualTreeHelper.GetChild(ele, 0) as Decorator;
                // Get scrollviewer
                ScrollViewer scrollViewer = ele is ScrollViewer ? ele as ScrollViewer : border.Child as ScrollViewer;

                scrollGrid = VisualTreeHelper.GetChild(scrollViewer, 0) as Grid;
            }
            else
                scrollGrid = ele.Parent as Grid;

            var c2 = VisualTreeHelper.GetChild(scrollGrid, 1);
            System.Windows.Controls.Primitives.ScrollBar bar = c2 as System.Windows.Controls.Primitives.ScrollBar;
            foreach (var c in scrollGrid.Children)
            {
                if (c is System.Windows.Controls.Primitives.ScrollBar)
                {
                    DoubleAnimation ani = new DoubleAnimation(fadeIn ? 0 : 1, fadeIn ? 1 : 0, new Duration(TimeSpan.FromSeconds(0.75)));
                    ((System.Windows.Controls.Primitives.ScrollBar)c).BeginAnimation(System.Windows.Window.OpacityProperty, ani);
                    if(handler != null)
                        ani.Completed += handler;
                }
            }
        }

        private void ScrollCheck(FrameworkElement ele, MouseEventArgs e)
        {
            if (ele.Visibility != System.Windows.Visibility.Visible)
                return;
            Point p = e.MouseDevice.GetPosition(ele);
            var captured = Mouse.Captured;
            bool alreadySet = ScrollViewer.GetVerticalScrollBarVisibility(ele) != ScrollBarVisibility.Hidden;
            if ((Math.Abs(p.X - ele.ActualWidth) < 50))
            {
                if (!alreadySet)
                {
                    ScrollViewer.SetVerticalScrollBarVisibility(ele, ScrollBarVisibility.Auto);
                    SetScrollViewerAnimation(ele, true);
                }
            }
            else if (captured == null || !(captured is System.Windows.Controls.Primitives.Thumb))
            {
                if (_currentlyScrollingElements.ContainsKey(ele))
                {
                    if (_currentlyScrollingElements[ele] < DateTime.Now)
                    {
                        _currentlyScrollingElements.Remove(ele);
                        _currentlyEndingScrollingElements[ele] = DateTime.Now.Add(TimeSpan.FromSeconds(0.75));
                        SetScrollViewerAnimation(ele, false, delegate(object _s, EventArgs _e)
                        {
                            ScrollViewer.SetVerticalScrollBarVisibility(ele, ScrollBarVisibility.Hidden);
                        });
                    }
                    return;
                }
                else if (alreadySet && !_currentlyEndingScrollingElements.ContainsKey(ele))
                {
                    _currentlyEndingScrollingElements[ele] = DateTime.Now.Add(TimeSpan.FromSeconds(0.75));
                    SetScrollViewerAnimation(ele, false, delegate(object _s, EventArgs _e)
                    {
                        ScrollViewer.SetVerticalScrollBarVisibility(ele, ScrollBarVisibility.Hidden);
                    });
                }
                if (_currentlyEndingScrollingElements.ContainsKey(ele))
                {
                    if (_currentlyEndingScrollingElements[ele] < DateTime.Now)
                        _currentlyEndingScrollingElements.Remove(ele);
                    else
                        return;
                }
                ScrollViewer.SetVerticalScrollBarVisibility(ele, ScrollBarVisibility.Hidden);
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PreviewControls.Preview.ForcePreviewOpen();
        }

        private void Window_Activation_Changed(object sender, EventArgs e)
        {
            if (Window.IsActive)
                PreviewControls.Preview.ForcePreviewOpen();
            else
                PreviewControls.Preview.PreviewHide();
        }

        #endregion

        #region Top Bar Helpers

        #region Click events

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = (FrameworkElement)sender;
            while(element != null && (!(element is Window)))
                element = element.Parent as FrameworkElement;
            Window w = (Window)element;
            w.Close();
            if (Application.Current.Windows.Count == 0)
                ThreadPool.Close();
        }

        #endregion

        #region Icon Activation

        private void ActivateTopBarIcons()
        {
            Close_btn_right.Visibility = System.Windows.Visibility.Visible;
            Min_btn_right.Visibility = System.Windows.Visibility.Visible;
            Max_btn_right.Visibility = System.Windows.Visibility.Visible;
        }

        #region Right Icons

        private void Activate_Title_Icons_right(object sender, MouseEventArgs e)
        {
            //hover effect, make sure your grid is named "Main" or replace "Main" with the name of your grid
            Close_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Close_act"];
            Min_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Min_act"];
            Max_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Max_act"];
        }

        private void Deactivate_Title_Icons_right(object sender, MouseEventArgs e)
        {
            Close_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Close_inact"];
            Min_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Min_inact"];
            Max_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Max_inact"];
        }

        private void Close_pressing_right(object sender, MouseButtonEventArgs e)
        {
            Close_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Close_pr"];
        }

        private void Min_pressing_right(object sender, MouseButtonEventArgs e)
        {
            Min_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Min_pr"];
        }

        private void Max_pressing_right(object sender, MouseButtonEventArgs e)
        {
            Max_btn_right.Fill = (ImageBrush)LayoutRoot.Resources["Max_pr"];
        }

        #endregion

        #endregion

        #region Moving/Resizing

        private System.Windows.Interop.WindowInteropHelper _windowHelper = null;
        public void move_window(object sender, MouseButtonEventArgs e)
        {
            if ((e.Device.Target is Grid || e.Device.Target is TextBlock) && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        void win_SourceInitialized(object sender, EventArgs e)
        {
            _windowHelper = new WindowInteropHelper(this);
            System.Windows.Interop.HwndSource.FromHwnd(_windowHelper.Handle).AddHook(new HwndSourceHook(WindowProc));
        }

        private IntPtr WindowProc(
              IntPtr hwnd,
              int msg,
              IntPtr wParam,
              IntPtr lParam,
              ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return (System.IntPtr)0;
        }

        private void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
        {
            MinMaxInfo mmi = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));

            System.Windows.Forms.Screen s = System.Windows.Forms.Screen.FromHandle(_windowHelper.Handle);
            mmi.ptMaxPosition.x = 0;
            mmi.ptMaxPosition.y = 0;
            mmi.ptMaxSize.x = Math.Abs(s.WorkingArea.Right - s.WorkingArea.Left);
            mmi.ptMaxSize.y = Math.Abs(s.WorkingArea.Bottom - s.WorkingArea.Top);

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        #endregion

        #endregion

        #region Sidebar Visibility

        private void StackPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetStackPanelIsOpen(sender as StackPanel, DriveTreeView);
        }

        private void StackPanel_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            SetStackPanelIsOpen(sender as StackPanel, FavoritesList);
        }

        private void StackPanel_MouseLeftButtonDown_2(object sender, MouseButtonEventArgs e)
        {
            SetStackPanelIsOpen(sender as StackPanel, DeviceList);
        }

        private void SetStackPanelIsOpen(StackPanel panel, FrameworkElement list)
        {
            int millisecondAnimation = 75;
            list.Visibility = list.Visibility == System.Windows.Visibility.Collapsed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Visible;
            DoubleAnimation upAnim = new DoubleAnimation(332, new Duration(TimeSpan.FromMilliseconds(millisecondAnimation)));
            if (double.IsNaN(list.Height) && list.ActualHeight != 0)
                list.Height = 332;
            else
                list.Height = 0;
            list.BeginAnimation(HeightProperty, upAnim);
            ((TextBlock)panel.Children[0]).Foreground = list.Visibility == System.Windows.Visibility.Collapsed ? Settings.NonSelectedItemTextColorBrush : Settings.TextColorBrush;
            if (list.Visibility == System.Windows.Visibility.Visible)
            {
                if (FavoritesStack != panel)
                {
                    DoubleAnimation animation = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(millisecondAnimation)));
                    animation.Completed += (obj, ev) => { FavoritesList.Visibility = Visibility.Collapsed; };
                    FavoritesList.Height = FavoritesList.RenderSize.Height;
                    FavoritesList.BeginAnimation(HeightProperty, animation);

                    ((TextBlock)FavoritesStack.Children[0]).Foreground = Settings.NonSelectedItemTextColorBrush;
                }
                if (DevicesStack != panel)
                {
                    DoubleAnimation animation = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(millisecondAnimation)));
                    animation.Completed += (obj, ev) => { DeviceList.Visibility = Visibility.Collapsed; };
                    DeviceList.Height = DeviceList.RenderSize.Height;
                    DeviceList.BeginAnimation(HeightProperty, animation);

                    ((TextBlock)DevicesStack.Children[0]).Foreground = Settings.NonSelectedItemTextColorBrush;
                }
                if (DriveTreeStack != panel)
                {
                    DoubleAnimation animation = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(millisecondAnimation)));
                    animation.Completed += (obj, ev) => { DriveTreeView.Visibility = Visibility.Collapsed; };
                    DriveTreeView.Height = DriveTreeView.RenderSize.Height;
                    DriveTreeView.BeginAnimation(HeightProperty, animation);

                    ((TextBlock)DriveTreeStack.Children[0]).Foreground = Settings.NonSelectedItemTextColorBrush;
                }
            }
            var c = Window.Resources["CairoExplorerSidebarSectionHeaderClosed"];
            var o = Window.Resources["CairoExplorerSidebarSectionHeaderOpen"];
            panel.Style = list.Visibility == System.Windows.Visibility.Collapsed ? (Style)c : (Style)o;
        }

        #endregion

        #region Preview helpers

        private void PreviewSelectedFiles(List<WrapperFileSystemInfo> openedFiles = null, Point? p = null)
        {
            openedFiles = openedFiles == null ? GetSelectedFileWrappers() : openedFiles;
            foreach (WrapperFileSystemInfo file in openedFiles)
                PreviewControls.Preview.PreviewFile(file.Info.Path, file, this, p);
        }

        #endregion

        #region Helpers

        private static Point GetScreenPointForPreview(FrameworkElement v)
        {
            Point relativePoint = new Point();
            if (v is ListView)
            {
                ListView view = (ListView)v;
                var listViewItem = view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex) as ListViewItem;
                relativePoint = ElementPointToScreenPoint(listViewItem, new Point());
                if (view.View != null && ((GridView)view.View).Columns.Count > 0)
                    relativePoint.X += ((GridView)view.View).Columns[0].ActualWidth + 50;
            }
            return relativePoint;
        }

        public static Point ElementPointToScreenPoint(UIElement element, Point pointOnElement)
        {
            return element.PointToScreen(pointOnElement);
        }

        public List<T> GetVisualChildCollection<T>(object parent) where T : Visual
        {
            List<T> visualCollection = new List<T>();
            GetVisualChildCollection(parent as DependencyObject, visualCollection);
            return visualCollection;
        }

        private void GetVisualChildCollection<T>(DependencyObject parent, List<T> visualCollection) where T : Visual
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T)
                {
                    visualCollection.Add(child as T);
                }
                else if (child != null)
                {
                    GetVisualChildCollection(child, visualCollection);
                }
            }
        }

        private T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        #endregion

        #region Key Helpers

        private void ColumnView_KeyDown(object sender, KeyEventArgs e)
        {
            ListView view = sender as ListView;
            List<string> selectedFiles = GetSelectedFiles(view);
            if (e.Key == Key.Space)
            {
                if (PreviewControls.Preview.OpenPreview != null)
                    PreviewControls.Preview.PreviewClose();
                else if (view.SelectedIndex >= 0)
                {
                    var listViewItem = view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex) as ListViewItem;
                    Point relativePoint = ElementPointToScreenPoint(listViewItem, new Point());
                    if (view.View != null && ((GridView)view.View).Columns.Count > 0)
                        relativePoint.X += ((GridView)view.View).Columns[0].ActualWidth + 50;

                    PreviewSelectedFiles(GetSelectedFileWrappers(view), relativePoint);
                }
            }
            if (e.Key == Key.Return)
                if (selectedFiles.Count > 0)
                {
                    string file = selectedFiles[0];
                    if (File.Exists(file))
                        OpenFile(file);
                    else
                        NavigateTo(selectedFiles[0]);
                }
            ModifierKeys k = Keyboard.Modifiers;
            if (e.Key == Key.C && (k & (ModifierKeys.Control)) == (ModifierKeys.Control))
                CopySelectedFiles(selectedFiles);
            if (e.Key == Key.X && (k & (ModifierKeys.Control)) == (ModifierKeys.Control))
                CutSelectedFiles(selectedFiles);
            if (e.Key == Key.V && (k & (ModifierKeys.Control)) == (ModifierKeys.Control))
                PasteClipboardContents(selectedFiles);
            if (e.Key == Key.Back || e.Key == Key.Delete)
                DeleteSelectedFiles(selectedFiles);
        }

        #endregion

        #region Size Changed

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Window.ActualHeight < 112)
            {
                Window.MinHeight = 112;
                Window.Height = 112;
            }
            FixFlowSizes();
        }

        #endregion

        private void thumbSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(_hasFinishedInit)
                PushNotification(Notifications.FolderChanged, "");
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            if (item.ToolTip.ToString() == "Details")
                _currentFlow = Flow.Detail;
            if (item.ToolTip.ToString() == "Coverflow")
                _currentFlow = Flow.CoverFlow;
            if (item.ToolTip.ToString() == "Thumbnail")
                _currentFlow = Flow.Thumbnail;
            if (item.ToolTip.ToString() == "Column")
                _currentFlow = Flow.Column;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        private void ThumbnailHideNames_Checked_1(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            Settings.ShowThumbnailNames = (sender as CheckBox).IsChecked == true;
            UpdateFlows(FlowRebuild.RebuildAllFlows);
        }

        private void LeftEdge_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Sidebar.Visibility = Sidebar.Visibility == System.Windows.Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (Sidebar.Visibility == System.Windows.Visibility.Collapsed)
            {
                FolderNameText.SetValue(Grid.ColumnSpanProperty, 2);
                FolderNameText.SetValue(Grid.ColumnProperty, 1);
            }
            else
            {
                FolderNameText.SetValue(Grid.ColumnSpanProperty, 1);
                FolderNameText.SetValue(Grid.ColumnProperty, 2);
            }
            FixFlowSizes();
        }
    }

    #region Wrapper Classes

    public class DriveFileSystemInfo : GenericFileSystemInfo
    {
        public DriveInfo Drive;
        public DriveFileSystemInfo(DriveInfo d)
            : base(d.Name, (d.IsReady ? (d.VolumeLabel == "" ? "Local Disk" : d.VolumeLabel) : d.DriveType.ToString()) + " (" + d.Name.Substring(0, d.Name.Length - 1) + ")"/*d.Name + (d.IsReady ? (" - " + d.VolumeLabel) : "")*/, DateTime.Now)
        {
            Drive = d;
            IsReady = d.IsReady;
        }
    }

    public class GenericFileSystemInfo
    {
        protected bool _Ready = true;

        public GenericFileSystemInfo(string Path, string Name, DateTime DateModified)
        {
            Init(Path, Name, DateModified);
        }

        private void Init(string Path, string Name, DateTime DateModified, string NameWithoutExtension = "")
        {
            this.Path = Path;
            this.Name = Name;
            this.Type = new TypeDefinition(Path);
            this.DateModified = DateModified;
            if (NameWithoutExtension == "")
                this.NameWithoutExtension = Name;
            else
                this.NameWithoutExtension = NameWithoutExtension;
        }

        public GenericFileSystemInfo(FileInfo fileInfo)
        {
            Init(fileInfo.FullName, fileInfo.Name, fileInfo.LastWriteTime, System.IO.Path.GetFileNameWithoutExtension(fileInfo.Name));
            FileLength = fileInfo.Length;
        }

        public GenericFileSystemInfo(DirectoryInfo directory)
        {
            Init(directory.FullName, directory.Name, directory.LastWriteTime);
        }

        public DateTime DateModified { get; private set; }
        public string DateModifiedString { get { return DateModified.ToFileDateTime(); } }

        public string Path
        {
            get;
            private set;
        }

        public bool Exists
        {
            get { return true; }
        }

        public TypeDefinition Type
        {
            get;
            private set;
        }

        public bool IsReady
        {
            get { return _Ready; }
            set { _Ready = value; }
        }

        public string Name
        {
            get;
            private set;
        }

        public string NameWithoutExtension
        {
            get;
            private set;
        }

        public double FileLength
        {
            get;
            private set;
        }
    }

    public static class Extensions
    {
        public static string ToFileDateTime(this DateTime t)
        {
            if ((DateTime.Now - t).Days < 1)
            {
                var ms = (DateTime.Now - t).Seconds;
                if (ms < 10)
                    return "Right now";
                else
                    return "Today " + t.ToShortTimeString();
            }
            if ((DateTime.Now - t).Days < 2)
                return "Yesterday " + t.ToShortTimeString();
            return t.ToLongDateString() + " " + t.ToShortTimeString();
        }
    }

    public class TypeDefinition
    {
        public static DriveInfo[] _drives = DriveInfo.GetDrives();
        private Dictionary<string, string> _typeConverter = new Dictionary<string, string>() 
        { 
            { ".lnk", "Shortcut" },
            { ".exe", "Application" }
        };

        private bool _File;
        private bool _Folder;
        private bool _Drive;
        private bool _Special;
        private string _BaseExtension;
        private string _Extension;

        public bool File { get { return this._File; } }
        public bool Folder { get { return this._Folder; } }
        public bool Drive { get { return this._Drive; } }
        public bool Special { get { return this._Special; } }
        public string BaseExtension { get { return this._BaseExtension; } }
        public string Extension { get { return this._Extension; } }

        public TypeDefinition(string path)
        {
            if (System.IO.File.Exists(path))
            {
                _Extension = Path.GetExtension(path);
                if(_typeConverter.ContainsKey(_Extension))
                    _BaseExtension = _typeConverter[_Extension];
                else if(_Extension.Length > 0)
                    _BaseExtension = _Extension.Substring(1);
                _File = true;
            }
            else if ((from d in _drives where d.Name == path select d).Count() > 0)
            {
                _Extension = "";
                _BaseExtension = "Drive";
                _Drive = true;
            }
            else if (Directory.Exists(path))
            {
                _Extension = "";
                _BaseExtension = "Folder";
                _Folder = true;
            }
            else
            {
                _Extension = "";
                _BaseExtension = "Special";
                _Special = true;
            }
        }
    }

    public class WrapperFileSystemInfo
    {
        private static FileToIconConverter _iconConverter = new FileToIconConverter();
        private AsyncDirectorySizeCallBack AsyncSizeUpdate = null;
        private static Dictionary<string, BitmapImage> _DefaultBitmaps = new Dictionary<string, BitmapImage>()
        {
            { "Folder", new BitmapImage(new Uri("pack://application:,,,/CairoExplorer;component/UI_RES/Folder-Closed-icon.png")) },
            { "Drive", new BitmapImage(new Uri("pack://application:,,,/CairoExplorer;component/UI_RES/disk.png")) },
            { "File", new BitmapImage(new Uri("pack://application:,,,/CairoExplorer;component/UI_RES/file-icon.png")) }
        };

        public WrapperFileSystemInfo(GenericFileSystemInfo info, double size = 0, AsyncDirectorySizeCallBack AsyncSizeUpdate = null)
        {
            this.Info = info;
            this.AsyncSizeUpdate = AsyncSizeUpdate;
            if (size == 0)
            {
                if (info.Type.File)
                    this.ByteSize = info.FileLength;
            }
            else
                this.ByteSize = size;
        }

        public void GetSizeAsync()
        {
            if (!Info.Type.File && !Info.Type.Special)
                CairoExplorerWindow.AsyncGetSizeOfDirectory(Info.Path, this, AsyncSizeUpdate == null ? ((f, s, p) => ByteSize = s) : AsyncSizeUpdate);
        }

        public GenericFileSystemInfo Info { get; private set; }

        public TypeDefinition Type
        {
            get { return Info.Type; }
        }

        public string NameNoExtension
        {
            get { return Settings.ShowExtensions || !Type.File ? Info.Name : Path.GetFileNameWithoutExtension(Info.Name); }
        }

        public override string ToString()
        {
            return Info.ToString();
        }

        public BitmapImage Thumbnail
        {
            get
            {
                return GetIcon(IconSize.thumbnail);
            }
        }

        public BitmapImage Icon
        {
            get
            {
                return GetIcon(IconSize.thumbnail);
            }
        }

        public BitmapImage GetIcon(IconSize size)
        {
            if (Type.Folder)
                return _DefaultBitmaps["Folder"];
            if (Type.Drive || Type.Special)
                return _DefaultBitmaps["Drive"];
            if (Info.Type.BaseExtension == "")
                return _DefaultBitmaps["File"];
            string ext = Info.Type.Extension;
            switch (ext.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".tiff":
                case ".tif":
                case ".gif":
                case ".bmp":
                case ".png":
                    try
                    {
                        return new BitmapImage(new Uri(Info.Path));
                    }
                    catch { return null; }
            }
            if (!File.Exists("IconCache/" + Info.Type.BaseExtension + size.ToString() + ".jpg"))
            {
                if (!Directory.Exists("IconCache/"))
                    Directory.CreateDirectory("IconCache/");
                var s = _iconConverter.GetImage(Info.Path, size);
                BitmapSource source = s as BitmapSource;
                using (FileStream fileStream = new FileStream("IconCache/" + Info.Type.BaseExtension + size.ToString() + ".jpg", FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.QualityLevel = 100;
                    encoder.Save(fileStream);
                }
            }
            Uri u = new Uri(Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "IconCache/" + Info.Type.BaseExtension + size.ToString() + ".jpg"));
            return new BitmapImage(u);
        }

        private string GetDriveType(DriveFileSystemInfo info)
        {
            if (info.Drive.DriveType == DriveType.Fixed)
                return "Hard Drive";
            if (info.Drive.DriveType == DriveType.CDRom)
                return "CD Rom";
            return info.Drive.DriveType.ToString();
        }
        public string Size { get; private set; }
        private double _byteSize = 0;
        public double ByteSize
        {
            get { return _byteSize; }
            set
            {
                _byteSize = value;
                Size = CairoExplorerWindow.GetFileSize(_byteSize / CairoExplorerWindow.ByteCount);
            }
        }
    }

    public class ExtraGridViewColumn : GridViewColumn
    {
        public bool sortingUp = false;
    }

    #endregion

    #region Settings

    public class Settings
    {
        public static bool ShowHiddenFilesAndFolders = false;
        public static bool DetailedThumbnailView = false;
        public static bool ShowThumbnailNames = true;
        public static bool OpenFoldersInNewWindow = true;
        public static bool ShowExtensions = false;
        public static double FadeInTime = 0.25;
        public static double WindowFadeInTime = 0.4;
        public static double FontSize = 12;
        public static FontStretch FontStretch = FontStretches.UltraCondensed;
        public static string FontName = "Segoe UI";
        public static Brush TextColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF292728")/*Colors.Black*/);
        public static Brush InvertTextColorBrush = new SolidColorBrush(Colors.White);
        public static Brush SelectedTextColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE05A37"));
        public static Brush NonSelectedItemTextColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9A9693")/*Colors.Black*/);
        public static Color ItemBackColor1 = Colors.Transparent;//(Color)ColorConverter.ConvertFromString("#FFEEF7FA");
        public static Color ItemBackColor2 = Colors.Transparent;//(Color)ColorConverter.ConvertFromString("#00FFFFFF");
    }

    #endregion

    #region Helper classes

    public class GridLengthAnimation : AnimationTimeline
    {
        /// <summary>
        /// Returns the type of object to animate
        /// </summary>
        public override Type TargetPropertyType
        {
            get
            {
                return typeof(GridLength);
            }
        }

        /// <summary>
        /// Creates an instance of the animation object
        /// </summary>
        /// <returns>Returns the instance of the GridLengthAnimation</returns>
        protected override System.Windows.Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        /// <summary>
        /// Dependency property for the From property
        /// </summary>
        public static readonly DependencyProperty FromProperty = DependencyProperty.Register("From", typeof(GridLength),
                typeof(GridLengthAnimation));

        /// <summary>
        /// CLR Wrapper for the From depenendency property
        /// </summary>
        public GridLength From
        {
            get
            {
                return (GridLength)GetValue(GridLengthAnimation.FromProperty);
            }
            set
            {
                SetValue(GridLengthAnimation.FromProperty, value);
            }
        }

        /// <summary>
        /// Dependency property for the To property
        /// </summary>
        public static readonly DependencyProperty ToProperty = DependencyProperty.Register("To", typeof(GridLength),
                typeof(GridLengthAnimation));

        /// <summary>
        /// CLR Wrapper for the To property
        /// </summary>
        public GridLength To
        {
            get
            {
                return (GridLength)GetValue(GridLengthAnimation.ToProperty);
            }
            set
            {
                SetValue(GridLengthAnimation.ToProperty, value);
            }
        }

        /// <summary>
        /// Animates the grid let set
        /// </summary>
        /// <param name="defaultOriginValue">The original value to animate</param>
        /// <param name="defaultDestinationValue">The final value</param>
        /// <param name="animationClock">The animation clock (timer)</param>
        /// <returns>Returns the new grid length to set</returns>
        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = ((GridLength)GetValue(GridLengthAnimation.FromProperty)).Value;
            //check that from was set from the caller
            if (fromVal == 1)
                //set the from as the actual value
                fromVal = ((GridLength)defaultDestinationValue).Value;

            double toVal = ((GridLength)GetValue(GridLengthAnimation.ToProperty)).Value;

            if (fromVal > toVal)
                return new GridLength((1 - animationClock.CurrentProgress.Value) * (fromVal - toVal) + toVal, GridUnitType.Star);
            else
                return new GridLength(animationClock.CurrentProgress.Value * (toVal - fromVal) + fromVal, GridUnitType.Star);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo
    {
        public UnmanagedPoint ptReserved;
        public UnmanagedPoint ptMaxSize;
        public UnmanagedPoint ptMaxPosition;
        public UnmanagedPoint ptMinTrackSize;
        public UnmanagedPoint ptMaxTrackSize;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedPoint
    {
        public int x;
        public int y;

        public UnmanagedPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class ThreadPool
    {
        private Stack<Action> _actions = new Stack<Action>();
        private List<System.Threading.Thread> _threads = new List<System.Threading.Thread>();
        private bool _closing = false;

        public ThreadPool()
        {
            for (int i = 0; i < 5; i++)
            {
                System.Threading.Thread _thread = new System.Threading.Thread(Run);
                _thread.Start();
                _threads.Add(_thread);
            }
        }

        public void Close()
        {
            _closing = true;
            foreach (System.Threading.Thread _thread in _threads)
            {
                try
                {
                    _thread.Resume();
                    _thread.Abort();
                }
                catch
                {
                }
            }
        }

        public void Push(Action a)
        {
            lock (_actions)
                _actions.Push(a);
            Poke();
        }

        private void Poke()
        {
            try
            {
                foreach (System.Threading.Thread _thread in _threads)
                    _thread.Resume();
            }
            catch { }
        }

        private void Run()
        {
        restart:
            System.Threading.Thread.CurrentThread.Suspend();
        restartLoop:
            if (_closing)
                return;
            Action a = null;
            lock (_actions)
                if (_actions.Count > 0)
                    a = _actions.Pop();
            if (a != null)
            {
                a();
                goto restartLoop;
            }
            else
                goto restart;
        }

        public void CheckClose()
        {
            if (_closing)
                System.Threading.Thread.CurrentThread.Abort();
        }
    }

    #endregion
}
