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
using GlassLib;
using Microsoft.Win32;

namespace CairoExplorer
{
    public delegate void AsyncDirectorySizeCallBack(WrapperFileSystemInfo f, double size, object param);
    public delegate void RoutedEventHandlerListView(object sender, RoutedEventArgs e, ListView view);
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

        private ObservableCollection<WrapperFileSystemInfo> _fileSysList = new ObservableCollection<WrapperFileSystemInfo>();

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
            foreach (var i in DisplaySidebarDirectoryTreeView("//"))
                DriveTreeView.Items.Add(i);

            PopulateDevices();

            PopulateFavorites();

            SetUpColumnView();
            SetUpCoverFlowView();
            SetUpThumbnailView();
            PushNotification(Notifications.ChangingFolder, _currentWindowPath);
            ActivateTopBarIcons();

            if(!PreviewControls.Preview.AdditionalPaths.ContainsKey("*"))
                PreviewControls.Preview.AdditionalPaths.Add("*", typeof(PropertiesPreview));

            FadeInWindowAnimation();


            AddAutoHideScrollViewer(ColumnView);
            AddAutoHideScrollViewer(CoverFlowView);
        }

        private void SetUpThumbnailView()
        {
            if(Settings.DetailedThumbnailView)
                thumbSize.Minimum = 70;
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
            ColumnView.ItemContainerGenerator.StatusChanged += new EventHandler(ColumnView_ContainerStatusChanged);
            ColumnView.ContextMenu = new ContextMenu();

            var gridView = ColumnView.View as GridView;
            //Add a few extra spaces just to add some additional space to the grid (it looks way better)
            AddColumn(gridView, "Name  ", "NameClean", true, "Icon");
            AddColumn(gridView, "Date Modified  ", "Info.DateModified");
            AddColumn(gridView, "Type  ", "TypeClean");
            AddColumn(gridView, "Size  ", "Size");

            List<GridViewColumnHeader> columns = GetVisualChildCollection<GridViewColumnHeader>(ColumnView);
            foreach (GridViewColumnHeader col in columns)
            {
                if (col.Column != null)
                {
                    col.Width = Double.NaN;
                    col.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                }
            }
        }

        private void SetUpCoverFlowView()
        {
            CoverFlowView.Visibility = System.Windows.Visibility.Collapsed;
            CoverFlowView.ItemContainerGenerator.StatusChanged += new EventHandler(CoverFlowView_ContainerStatusChanged);
            CoverFlowView.ContextMenu = new ContextMenu();

            var gridView = CoverFlowView.View as GridView;
            //Add a few extra spaces just to add some additional space to the grid (it looks way better)
            AddColumn(gridView, "Name  ", "NameClean", true, "Icon");
            AddColumn(gridView, "Date Modified  ", "Info.DateModified");
            AddColumn(gridView, "Type  ", "TypeClean");
            AddColumn(gridView, "Size  ", "Size");

            List<GridViewColumnHeader> columns = GetVisualChildCollection<GridViewColumnHeader>(CoverFlowView);
            foreach (GridViewColumnHeader col in columns)
            {
                if (col.Column != null)
                {
                    col.Width = Double.NaN;
                    col.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                }
            }
        }

        private void AddColumn(GridView gridView, string header, string binding, bool image = false, string imageBinding = "", double width = 0)
        {
            ExtraGridViewColumn gvc = new ExtraGridViewColumn();
            gvc.Header = header;

            FrameworkElementFactory dp = new FrameworkElementFactory(typeof(DockPanel));
            dp.SetValue(DockPanel.LastChildFillProperty, true);
            if (image)
            {
                double iconSize = 16.0;
                FrameworkElementFactory icon = new FrameworkElementFactory(typeof(Image));
                icon.SetBinding(Image.SourceProperty, new Binding(imageBinding));
                icon.SetValue(Image.WidthProperty, iconSize);
                icon.SetValue(Image.HeightProperty, iconSize);
                icon.SetValue(Image.MarginProperty, new Thickness(15, 0, 5, 0));
                icon.SetValue(Grid.ColumnProperty, 0);
                dp.AppendChild(icon);
            }
            FrameworkElementFactory tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding(binding));
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
            tb.SetValue(Grid.ColumnProperty, 1);
            DataTemplate dt = new DataTemplate();
            dp.AppendChild(tb);
            dt.VisualTree = dp;
            gvc.CellTemplate = dt;

            if (width != 0)
                gvc.Width = width;
            gridView.Columns.Add(gvc);
        }

        #endregion

        #region Devices Sidebar

        private void PopulateDevices()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
                AddDeviceToSidebar(d);
        }

        private void AddDeviceToSidebar(DriveInfo f)
        {
            ListViewItem item = new ListViewItem() { Content = _blankSpace + f.Name };
            item.Selected += new RoutedEventHandler(device_Selected);
            SetFont(item);
            DeviceList.Items.Add(item);
        }

        void device_Selected(object sender, RoutedEventArgs e)
        {
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
            _favorites.Add(new Favorite() { Name = "Network Places", Path = "Network" });
            _favorites.Add(new Favorite() { Name = userName, Path = "C:/Users/" + userName });
            _favorites.Add(new Favorite() { Name = "Desktop", Path = "C:/Users/" + userName + "/Desktop" });
            _favorites.Add(new Favorite() { Name = "Documents", Path = "C:/Users/" + userName + "/Documents" });
            _favorites.Add(new Favorite() { Name = "Downloads", Path = "C:/Users/" + userName + "/Downloads" });
            _favorites.Add(new Favorite() { Name = "Dropbox", Path = "C:/Users/" + userName + "/Dropbox" });
            _favorites.Add(new Favorite() { Name = "Music", Path = "C:/Users/" + userName + "/Music" });
            _favorites.Add(new Favorite() { Name = "Pictures", Path = "C:/Users/" + userName + "/Pictures" });
            _favorites.Add(new Favorite() { Name = "Videos", Path = "C:/Users/" + userName + "/Videos" });
            BuildFavoritesPanel();

            FavoritesList.ContextMenu = new ContextMenu();
            FavoritesList.ContextMenuOpening += new ContextMenuEventHandler(FavoritesList_ContextMenuOpening);
        }

        private bool _hasAddedFavoritesStatusChangedEvent = false;
        private void BuildFavoritesPanel()
        {
            #region Factory
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
            tb.SetBinding(TextBlock.TextProperty, new Binding("SidebarName"));
            tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left);
            tb.SetValue(Grid.ColumnProperty, 1);
            DataTemplate dt = new DataTemplate();
            dp.AppendChild(tb);
            dt.VisualTree = dp;
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

        void favoritesList_ContextMenu(object sender, RoutedEventArgs e, ListView view)
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
            public string SidebarName
            {
                get { return _blankSpace + Name; }
            }
            public BitmapImage Icon
            {
                get
                {
                    return new WrapperFileSystemInfo(new GenericFileSystemInfo(Path, Path, null)).Icon;
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
            NavigateTo(f.Path, f.Name);
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
            item.FontFamily = new FontFamily("Calibri");
            item.FontSize = 15;
            item.FontWeight = FontWeights.SemiBold;
            item.FontStretch = FontStretches.UltraCondensed;
            if (item is ListViewItem)
            {
                item.Height = 24;
                item.Padding = new Thickness();
            }
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

            item = GetRealSender(item, DriveTreeView.Items);
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
            NavigateTo(newPath, "", false);
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
            NavigateTo(newPath, "", false);
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

        private void PushNotification(Notifications notifications, string p)
        {
            if (notifications == Notifications.FolderChanged)
            {
                if (string.IsNullOrEmpty(p) || Path.GetFullPath(p) == Path.GetFullPath(_currentWindowPath))
                    UpdateFlows(FlowRebuild.NoRebuild);
            }
            else if (notifications == Notifications.ChangingFolder)
            {
                NavigateTo(p);
            }
        }

        private void NavigateTo(string path, string name = "", bool updateBackForwardQueues = true)
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

            UpdateFlows(FlowRebuild.NoRebuildPathChanged);
            CalcBottomBarStats();
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
                    thumbSize.Visibility = System.Windows.Visibility.Collapsed;
                    thumbSizeText.Visibility = System.Windows.Visibility.Collapsed;
                    ThumbnailScroll.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    thumbSize.Visibility = System.Windows.Visibility.Visible;
                    thumbSizeText.Visibility = System.Windows.Visibility.Visible;
                    ThumbnailScroll.Visibility = System.Windows.Visibility.Visible;
                }
                if (_currentFlow != Flow.Detail)
                    ColumnView.Visibility = System.Windows.Visibility.Collapsed;
                else
                    ColumnView.Visibility = System.Windows.Visibility.Visible;
                if (_currentFlow != Flow.CoverFlow)
                {
                    CoverFlowSplit.Visibility = System.Windows.Visibility.Collapsed;
                    CoverFlowViewer.Visibility = System.Windows.Visibility.Collapsed;
                    CoverFlowView.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    CoverFlowSplit.Visibility = System.Windows.Visibility.Visible;
                    CoverFlowViewer.Visibility = System.Windows.Visibility.Visible;
                    CoverFlowView.Visibility = System.Windows.Visibility.Visible;
                }
                if (_currentFlow != Flow.Column)
                {
                    foreach (ListView v in _columnViews)
                        ColumnScroller.Children.Remove(v);
                    _columnViews.Clear();
                    _columnViewPath.Clear();
                    ColumnScroller.ColumnDefinitions.Clear();
                    ColumnScroll.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                    ColumnScroll.Visibility = System.Windows.Visibility.Visible;
                FixFlowSizes();
            }
            if (flowRebuild == FlowRebuild.NoRebuildPathChanged)
                PreviewControls.Preview.PreviewClose();
            if (_currentFlow == Flow.Thumbnail)
                UpdateThumbnailFlow();
            if (_currentFlow == Flow.Detail)
                UpdateDetailsFlow();
            if (_currentFlow == Flow.Column)
                UpdateColumnFlow(flowRebuild);
            if (_currentFlow == Flow.CoverFlow)
                UpdateCoverFlowFlow();
        }

        private void FixFlowSizes()
        {
            ColumnView.Height = FolderNameText.ActualHeight - 10;
            ColumnView.Width = FolderNameText.ActualWidth - 10;

            CoverFlowView.Height = (FolderNameText.ActualHeight - 10) / 2 - 2;
            CoverFlowView.Width = (FolderNameText.ActualWidth - 10);
            CoverFlowViewer.Height = (FolderNameText.ActualHeight - 10) / 2 - 2;
            CoverFlowViewer.Width = (FolderNameText.ActualWidth - 10);
        }

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

            ColumnScroller.ColumnDefinitions.Clear();

            int colNum = 0, gridColNum = 0;
            foreach (string path in _columnViewPath)
            {
                if(File.Exists(path))
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
                tb.SetBinding(TextBlock.TextProperty, new Binding("NameClean"));
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

                view.MouseDoubleClick += new MouseButtonEventHandler(_columnView_MouseDoubleClick);
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
                c.InitWithFile(new WrapperFileSystemInfo(new FileInfo(_columnViewPath[_columnViewPath.Count - 1])), Window);
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

            if(gridColNum * 200 > FolderNameText.RenderSize.Width)
                ColumnScroll.ScrollToRightEnd();
        }

        void view_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ColumnViewList col = sender as ColumnViewList;
            ScrollViewer scroll = col.Parent as ScrollViewer;
            scroll.ScrollToVerticalOffset(scroll.ContentVerticalOffset + (e.Delta < 0 ? 1 : -1) * 50);
        }

        void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView view = sender as ListView;
            if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed)
            {
                CalcBottomBarStats();

                if (view.SelectedItem != null)
                {
                    if (view.Name != "" || File.Exists(((WrapperFileSystemInfo)view.SelectedItem).Info.FullName))
                    {
                        PreviewControls.Preview.PreviewFileChanged(((WrapperFileSystemInfo)view.SelectedItem).Info.FullName, ((WrapperFileSystemInfo)view.SelectedItem), this, GetScreenPointForPreview(view));

                        if (_currentFlow == Flow.Column)
                        {
                            ColumnViewList cview = sender as ColumnViewList;
                            _columnViewPath.RemoveRange(cview.ColumnNumber + 1, _columnViewPath.Count - cview.ColumnNumber - 1);
                            _columnViewPath.Add((cview.SelectedItem as WrapperFileSystemInfo).Info.FullName);
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
            _currentWindowPath = ((WrapperFileSystemInfo)view.SelectedItem).Info.FullName;
            if ((view.SelectedItem as WrapperFileSystemInfo).IsFolder)
            {
                _columnViewPath.RemoveRange(view.ColumnNumber + 1, _columnViewPath.Count - view.ColumnNumber - 1);
                _columnViewPath.Add((view.SelectedItem as WrapperFileSystemInfo).Info.FullName);
                UpdateFlows(FlowRebuild.NoRebuild);
            }
            else if ((view.SelectedItem as WrapperFileSystemInfo).IsDrive)
            {
                if (((WrapperFileSystemInfo)view.SelectedItem).Info.IsReady)
                {
                    _columnViewPath.RemoveRange(view.ColumnNumber + 1, _columnViewPath.Count - view.ColumnNumber - 1);
                    _columnViewPath.Add((view.SelectedItem as WrapperFileSystemInfo).Info.FullName);
                    UpdateFlows(FlowRebuild.NoRebuild);
                }
                else
                    MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
            }
            else if ((view.SelectedItem as WrapperFileSystemInfo).IsSpecial)
            {
                _columnViewPath.Clear();
                _columnViewPath.Add((view.SelectedItem as WrapperFileSystemInfo).Info.FullName);
                UpdateFlows(FlowRebuild.NoRebuild);
            }
            else
                OpenFile(((WrapperFileSystemInfo)view.SelectedItem).Info.FullName);
        }

        private class ColumnViewList : ListView
        {
            public int ColumnNumber = 0;
        }

        #endregion

        #region Update Cover Flow

        private void UpdateCoverFlowFlow()
        {
            _fileSysList =
               new ObservableCollection<WrapperFileSystemInfo>(GetFilesAndFoldersForDirectory(ref _currentWindowPath));

            CoverFlowView.ItemsSource = _fileSysList;
            foreach (var f in new List<WrapperFileSystemInfo>(_fileSysList))
            {
                if (((f.IsDrive && f.Info.IsReady) || f.IsFolder) && f.Info.FullName != null)
                    AsyncGetSizeOfDirectory(f.Info.FullName, f, AsyncSizeUpdate, CoverFlowView);
            }
            ColumnSort(CoverFlowView, _sortingByName, _sortingBySort);
        }

        private void CoverFlowView_ContainerStatusChanged(object sender, EventArgs e)
        {
            if (CoverFlowView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < CoverFlowView.Items.Count; i++)
                {
                    ListViewItem item = CoverFlowView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
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

        #endregion

        #region Update Details Flow

        private void UpdateDetailsFlow()
        {
            _fileSysList =
               new ObservableCollection<WrapperFileSystemInfo>(GetFilesAndFoldersForDirectory(ref _currentWindowPath));

            ColumnView.ItemsSource = _fileSysList;
            foreach (var f in new List<WrapperFileSystemInfo>(_fileSysList))
            {
                if (((f.IsDrive && f.Info.IsReady) || f.IsFolder) && f.Info.FullName != null)
                    AsyncGetSizeOfDirectory(f.Info.FullName, f, AsyncSizeUpdate, ColumnView);
            }
            ColumnSort(ColumnView, _sortingByName, _sortingBySort);
        }

        private void AsyncSizeUpdate(WrapperFileSystemInfo f, double size, object v)
        {
            ListView view = v as ListView;
            view.Dispatcher.Invoke(new Action(delegate()
            {
                int index = GetIndex(_fileSysList, f);
                if (index >= 0)
                {
                    _fileSysList.RemoveAt(index);
                    f.ByteSize = size;
                    f.Size = GetFileSize(size / ByteCount);
                    _fileSysList.Insert(index, f);
                }
                else
                {
                }
            }));
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
            ColumnSort(ColumnView, target.Column.Header.ToString(), !((ExtraGridViewColumn)target.Column).sortingUp);
            ((ExtraGridViewColumn)target.Column).sortingUp = !((ExtraGridViewColumn)target.Column).sortingUp;
        }

        void ColumnView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListView list = sender as ListView;

            if (e.MouseDevice.Target is GridViewColumnHeader)
            {
                GridViewColumnHeader target = e.MouseDevice.Target as GridViewColumnHeader;
                ColumnSort(ColumnView, target.Column.Header.ToString(), !((ExtraGridViewColumn)target.Column).sortingUp);
                ((ExtraGridViewColumn)target.Column).sortingUp = !((ExtraGridViewColumn)target.Column).sortingUp;
            }
            else if (e.MouseDevice.Target is Border && !(((Border)e.MouseDevice.Target).Child is GridViewRowPresenter))
            {
                //Autosizing of the headers, just pass it through
            }
            else
            {
                if (ColumnView.SelectedItem == null)
                    return;
                if ((ColumnView.SelectedItem as WrapperFileSystemInfo).IsFolder ||
                    (ColumnView.SelectedItem as WrapperFileSystemInfo).IsSpecial)
                    PushNotification(Notifications.ChangingFolder, ((WrapperFileSystemInfo)ColumnView.SelectedItem).Info.FullName);
                else if ((ColumnView.SelectedItem as WrapperFileSystemInfo).IsDrive)
                {
                    if (((WrapperFileSystemInfo)ColumnView.SelectedItem).Info.IsReady)
                        PushNotification(Notifications.ChangingFolder, ((DriveFileSystemInfo)((WrapperFileSystemInfo)ColumnView.SelectedItem).Info).Drive.Name);
                    else
                        MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
                }
                else
                    OpenFile(((WrapperFileSystemInfo)ColumnView.SelectedItem).Info.FullName);
            }
        }

        private void ColumnSort(ListView view, string name, bool sortUp)
        {
            List<WrapperFileSystemInfo> lists = new List<WrapperFileSystemInfo>(_fileSysList);
            List<WrapperFileSystemInfo> folders = new List<WrapperFileSystemInfo>();
            List<WrapperFileSystemInfo> files = new List<WrapperFileSystemInfo>();

            foreach (var f in lists)
                if (f.IsFolder || f.IsSpecial || f.IsDrive)
                    folders.Add(f);
                else
                    files.Add(f);

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
            lists.Sort(delegate(WrapperFileSystemInfo a, WrapperFileSystemInfo b)
            {
                if (name == "Name")
                {
                    if (a.IsDrive && b.IsDrive)
                        return ((DriveFileSystemInfo)a.Info).Drive.Name.CompareTo(((DriveFileSystemInfo)b.Info).Drive.Name);
                    else
                        return a.NameClean.CompareTo(b.NameClean);
                }
                if (name == "Date Modified")
                    return a.Info.LastWriteTime.CompareTo(b.Info.LastWriteTime);
                if (name == "Type")
                    if (a.TypeClean == b.TypeClean)
                        return a.NameClean.CompareTo(b.NameClean);
                    else
                        return a.TypeClean.CompareTo(b.TypeClean);
                if (name == "Size")
                    return a.ByteSize.CompareTo(b.ByteSize);
                return 1;
            });
        }

        #endregion

        #region Thumbnail Flow

        private void UpdateThumbnailFlow()
        {
            ThumbnailDock.Children.Clear();
            ThumbnailDock.ColumnDefinitions.Clear();
            ThumbnailDock.RowDefinitions.Clear();
            double width = thumbSize.Value * 20;
            double height = thumbSize.Value * 20;
            List<WrapperFileSystemInfo> infos = GetFilesAndFoldersForDirectory(ref _currentWindowPath);
            int col = 0, row = 0;
            int maxCol = (int)Math.Round(FolderNameText.ActualWidth / width) - 1;
            int rows = (int)Math.Round((double)infos.Count / maxCol) + 1;
            for (int i = 0; i < maxCol; i++)
                ThumbnailDock.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(width) });
            ThumbnailDock.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(height) });
            foreach (var i in infos)
            {
                var ui = CreateDockPanel(i, width, height);
                Grid.SetColumn(ui, col++);
                Grid.SetRow(ui, row);
                if (col == maxCol)
                {
                    col = 0;
                    row++;
                    ThumbnailDock.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(width) });
                }
                ThumbnailDock.Children.Add(ui);
            }
        }

        private UIElement CreateDockPanel(WrapperFileSystemInfo info, double height, double width)
        {
            ThumbnailViewElement ele = new ThumbnailViewElement();
            ele.Image.Source = info.GetIcon(IconSize.large);
            ele.File = info;
            ele.Name.Text = info.NameClean;
            ele.Description.Text = "";// info.Info.DateModified;
            ele.Size.Text = "";// info.Size;
            ele.Type.Text = "";// info.TypeClean;
            ele.MouseDoubleClick += new MouseButtonEventHandler(ele_MouseDoubleClick);
            ele.PreviewKeyDown += ColumnView_KeyDown;
            ele.Width = width;
            ele.Height = height;
            ele.grid1.Width = width;
            ele.grid1.Height = height;
            return ele;
        }

        void ele_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThumbnailViewElement ele = sender as ThumbnailViewElement;
            if (ele != null)
            {
                if (ele.File.IsFolder ||
                    ele.File.IsSpecial)
                    PushNotification(Notifications.ChangingFolder, ele.File.Info.FullName);
                else if (ele.File.IsDrive)
                {
                    if (ele.File.Info.IsReady)
                        PushNotification(Notifications.ChangingFolder, ((DriveFileSystemInfo)(ele.File.Info)).Drive.Name);
                    else
                        MessageBox.Show("This device is not ready, please insert media into it and try again", "Device not ready", MessageBoxButton.OK);
                }
                else
                    OpenFile(ele.File.Info.FullName);
            }
        }

        #endregion

        #endregion

        #region Context Menu

        new void ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ListView view = sender as ListView;
            if (view.SelectedItem == null)
            {
                view.ContextMenu = null;
                return;
            }
            view.ContextMenu = new ContextMenu();
            view.ContextMenu.Items.Add(createMenuItem("Open", item_Click, view));
            if (!(view.SelectedItem as WrapperFileSystemInfo).IsFolder && 
                !(view.SelectedItem as WrapperFileSystemInfo).IsSpecial && 
                !(view.SelectedItem as WrapperFileSystemInfo).IsDrive)
                view.ContextMenu.Items.Add(createMenuItem("Preview", item_Click, view));
            if (IsDirectoryOrSpecialFolder(view.SelectedItem))
                view.ContextMenu.Items.Add(createMenuItem("Open in new window", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Restore previous versions - N/A", item_Click, view));
            view.ContextMenu.Items.Add(new Separator());
            view.ContextMenu.Items.Add(createMenuItem("Cut", item_Click, view));
            view.ContextMenu.Items.Add(createMenuItem("Copy", item_Click, view));
            if (IsDirectoryOrSpecialFolder(view.SelectedItem))
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

        MenuItem createMenuItem(string text, RoutedEventHandlerListView handler, ListView view)
        {
            MenuItem item = new MenuItem() { Header = text };
            item.Click += delegate(object sender, RoutedEventArgs e)
            {
                handler(sender, e, view);
            };
            return item;
        }

        void item_Click(object sender, RoutedEventArgs e, ListView view)
        {
            MenuItem menuItem = sender as MenuItem;
            List<string> selectedItems = GetSelectedFiles(view);
            switch (menuItem.Header.ToString())
            {
                case "Open":
                    if (selectedItems.Count > 0)
                        OpenItem(((WrapperFileSystemInfo)view.SelectedItem).Info.FullName, this);
                    break;
                case "Open in new window":
                    if (selectedItems.Count > 0)
                        OpenItem(((WrapperFileSystemInfo)view.SelectedItem).Info.FullName, this, true);
                    break;
                case "Preview":
                    if (selectedItems.Count > 0)
                    {
                        PreviewControls.Preview.PreviewFile(selectedItems[0], view.SelectedItem, this, GetScreenPointForPreview(view));
                    }
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
                    window.PushNotification(Notifications.ChangingFolder, item);
            }
        }

        #endregion

        #region File Management

        private bool IsDirectoryOrSpecialFolder(object p)
        {
            return p is WrapperFileSystemInfo && (((WrapperFileSystemInfo)p).IsFolder || (((WrapperFileSystemInfo)p).IsDrive || ((WrapperFileSystemInfo)p).IsSpecial));
        }

        private void DeleteSelectedFiles(List<string> selectedItems = null)
        {
            selectedItems = selectedItems == null ? GetSelectedFiles() : selectedItems;
            if (MessageBox.Show(string.Format("Are you sure you want to delete {1} {0} file{2}?", selectedItems.Count, selectedItems.Count > 1 ? "these" : "this", selectedItems.Count > 1 ? "s" : ""), "Delete?", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                List<string> notificationsRecentlyPushed = new List<string>();
                foreach (string file in selectedItems)
                {
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
                        File.Copy(file, Path.Combine(folderBeingPastedInfo, Path.GetFileName(fileName)));
                        if (doCut)
                            File.Delete(file);
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
            if (_currentFlow == Flow.Detail)
            {
                foreach (var o in ColumnView.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo).Info.FullName);
            }
            if (_currentFlow == Flow.CoverFlow)
            {
                foreach (var o in CoverFlowView.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo).Info.FullName);
            }
            if (_currentFlow == Flow.Column)
            {
                int max = _columnViews.Count - 1;
                var column = _columnViews[max];
                if (view as ColumnViewList != null)
                    column = view as ColumnViewList;

                foreach (var o in column.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo).Info.FullName);
            }
            return files;
        }

        private List<WrapperFileSystemInfo> GetSelectedFileWrappers(object view = null)
        {
            List<WrapperFileSystemInfo> files = new List<WrapperFileSystemInfo>();
            if (_currentFlow == Flow.Detail)
            {
                foreach (var o in ColumnView.SelectedItems)
                    files.Add((o as WrapperFileSystemInfo));
            }
            if (_currentFlow == Flow.CoverFlow)
            {
                foreach (var o in CoverFlowView.SelectedItems)
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
            return files;
        }

        #endregion

        #region Folder Helpers

        private List<WrapperFileSystemInfo> GetFilesAndFoldersForDirectory(ref string path)
        {
            List<WrapperFileSystemInfo> fs = new List<WrapperFileSystemInfo>();
            if (path == "//" || path == "Computer" || path == "My Computer")
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives)
                {
                    if (d.IsReady)
                    {
                        WrapperFileSystemInfo f = new WrapperFileSystemInfo(new DriveFileSystemInfo(d));
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
                    if (_specialPaths.Contains(f.Path) && f.Name != "Favorites" && f.Name != "Home")
                        fs.Add(new WrapperFileSystemInfo(new GenericFileSystemInfo(f.Path, f.Name, ""), 0));
                    else if(f.Name != "Favorites" && f.Name != "Home")
                        fs.Add(new WrapperFileSystemInfo(new DirectoryInfo(f.Path), 0));
                return fs;
            }
            else if (path == "Desktop")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            else if (path == "Network")
            {
                path = "Network";
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
                WrapperFileSystemInfo f = new WrapperFileSystemInfo(d);
                fs.Add(f);
            }
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                var fi = new FileInfo(file);
                if (!Settings.ShowHiddenFilesAndFolders)
                    if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                WrapperFileSystemInfo f = new WrapperFileSystemInfo(fi, fi.Length);
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

        private void CalcBottomBarStats()
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
                    BottomBarItemSelectedText.Text = string.Format("{0} Item{1} Selected, {2} {3} Total", numItems, numItems != 1 ? "s" : "", size, sizeAmt);
                }
                catch { }
            }
        }

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
                if (f.Info.FullName == file.Info.FullName)
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
            Decorator border = VisualTreeHelper.GetChild(ele, 0) as Decorator;

            // Get scrollviewer
            ScrollViewer scrollViewer = ele is ScrollViewer ? ele as ScrollViewer : border.Child as ScrollViewer;

            Grid scrollGrid = VisualTreeHelper.GetChild(scrollViewer, 0) as Grid;
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
            DriveTreeView.Visibility = DriveTreeView.Visibility == System.Windows.Visibility.Collapsed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            var c = Window.Resources["CairoExplorerSidebarSectionHeaderClosed"];
            var o = Window.Resources["CairoExplorerSidebarSectionHeaderOpen"];
            DriveTreeStack.Style = DriveTreeView.Visibility == System.Windows.Visibility.Collapsed ? (Style)c : (Style)o;
        }

        private void StackPanel_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            FavoritesList.Visibility = FavoritesList.Visibility == System.Windows.Visibility.Collapsed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            var c = Window.Resources["CairoExplorerSidebarSectionHeaderClosed"];
            var o = Window.Resources["CairoExplorerSidebarSectionHeaderOpen"];
            FavoritesStack.Style = FavoritesList.Visibility == System.Windows.Visibility.Collapsed ? (Style)c : (Style)o;
        }

        private void StackPanel_MouseLeftButtonDown_2(object sender, MouseButtonEventArgs e)
        {
            DeviceList.Visibility = DeviceList.Visibility == System.Windows.Visibility.Collapsed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            var c = Window.Resources["CairoExplorerSidebarSectionHeaderClosed"];
            var o = Window.Resources["CairoExplorerSidebarSectionHeaderOpen"];
            DevicesStack.Style = DeviceList.Visibility == System.Windows.Visibility.Collapsed ? (Style)c : (Style)o;
        }

        #endregion

        #region Preview helpers

        private void PreviewSelectedFiles(List<WrapperFileSystemInfo> openedFiles = null, Point? p = null)
        {
            openedFiles = openedFiles == null ? GetSelectedFileWrappers() : openedFiles;
            foreach (WrapperFileSystemInfo file in openedFiles)
                PreviewControls.Preview.PreviewFile(file.Info.FullName, file, this, p);
        }

        #endregion

        #region Helpers

        private static Point GetScreenPointForPreview(ListView view)
        {
            var listViewItem = view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex) as ListViewItem;
            Point relativePoint = ElementPointToScreenPoint(listViewItem, new Point());
            if (view.View != null && ((GridView)view.View).Columns.Count > 0)
                relativePoint.X += ((GridView)view.View).Columns[0].ActualWidth + 50;
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
                else
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
            FixFlowSizes();

            SearchText.Width = SearchDockPanel.ActualWidth - 20;
        }

        #endregion

        private void thumbSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(_hasFinishedInit)
                PushNotification(Notifications.FolderChanged, "");
        }
    }

    #region Wrapper Classes

    public class DriveFileSystemInfo : GenericFileSystemInfo
    {
        public DriveInfo Drive;
        public DriveFileSystemInfo(DriveInfo d)
            : base(d.Name, (d.IsReady ? (d.VolumeLabel == "" ? "Local Disk" : d.VolumeLabel) : d.DriveType.ToString()) + " (" + d.Name.Substring(0, d.Name.Length - 1) + ")"/*d.Name + (d.IsReady ? (" - " + d.VolumeLabel) : "")*/, "", "Drive")
        {
            Drive = d;
            IsReady = d.IsReady;
        }
    }

    public class GenericFileSystemInfo : FileSystemInfo
    {
        protected string name;
        protected string fullName;
        protected string lastModified;
        protected bool _ready = true;

        public GenericFileSystemInfo(string fullName, string Name, string lastModified, string Type = "Special")
        {
            this.fullName = fullName;
            name = Name;
            this.lastModified = lastModified;
            if (Type == "Special")
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                if (File.Exists(fullName))
                    Type = Path.GetExtension(fullName);
                else if ((from d in drives where d.Name == fullName select d).Count() > 0)
                    Type = "Drive";
                else if (Directory.Exists(fullName))
                    Type = "Folder";
            }
            this.Type = Type;
        }

        public string DateModified
        {
            get { return lastModified; }
        }

        public override string FullName
        {
            get
            {
                return fullName;
            }
        }

        public override bool Exists
        {
            get { return true; }
        }

        public ImageSource Icon
        {
            get;
            set;
        }

        public string Type
        {
            get;
            set;
        }

        public bool IsReady
        {
            get { return _ready; }
            set { _ready = value; }
        }

        public override string Name
        {
            get { return name; }
        }

        public override void Delete()
        {
        }
    }

    public static class Extensions
    {
        public static string ToFileDateTime(this DateTime t)
        {
            return t.ToShortDateString() + " " + t.ToShortTimeString();
        }
    }

    public class WrapperFileSystemInfo
    {
        private static FileToIconConverter _iconConverter = new FileToIconConverter();
        private Dictionary<string, string> _typeConverter = new Dictionary<string,string>();

        public WrapperFileSystemInfo(FileSystemInfo info, double size = 0)
        {
            _typeConverter.Add(".lnk", "Shortcut");
            _typeConverter.Add(".exe", "Application");

            Info = info is GenericFileSystemInfo ? (GenericFileSystemInfo)info : new GenericFileSystemInfo(info.FullName, info.Name, info.LastWriteTime.ToFileDateTime(), info is DirectoryInfo ? "Folder" : info.Extension);
            ByteSize = size;
            Size = CairoExplorerWindow.GetFileSize(size / CairoExplorerWindow.ByteCount);
        }

        public GenericFileSystemInfo Info { get; private set; }

        public string Type
        {
            get { return Info.Type; }
        }

        public string NameClean
        {
            get { return Settings.ShowExtensions || IsDrive || IsSpecial || IsFolder ? Info.Name : Path.GetFileNameWithoutExtension(Info.Name); }
        }

        public string TypeClean
        {
            get { return _typeConverter.ContainsKey(Info.Type) ? _typeConverter[Info.Type] : Info.Type; }
        }

        public bool IsFolder
        {
            get { return Type == "Folder"; }
        }

        public bool IsSpecial
        {
            get { return Type == "Special"; }
        }

        public bool IsDrive
        {
            get { return Info is DriveFileSystemInfo; }
        }

        public override string ToString()
        {
            return Info.ToString();
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
            if (IsFolder)
                return new BitmapImage(new Uri("http://icons.iconarchive.com/icons/deleket/scrap/256/Folder-Closed-icon.png"));
            else
            {
                if (IsSpecial)
                    return new BitmapImage(new Uri("http://prodev.wsd.wednet.edu/users/fmstech/weblog/d25cd/images/81e15.jpg"));
                string ext = Info.Type;
                switch (ext)
                {
                    case ".jpg":
                    case ".jpeg":
                    case ".tiff":
                    case ".tif":
                    case ".gif":
                    case ".bmp":
                    case ".png":
                        return new BitmapImage(new Uri(Info.FullName));
                }
                //System.Drawing.Icon icon =
                //    System.Drawing.Icon.ExtractAssociatedIcon(Info.FullName);
                if (Info.Type == "")
                    return new BitmapImage(new Uri("http://icons.iconarchive.com/icons/gakuseisean/radium/256/file-icon.png"));
                if (!File.Exists("IconCache/" + Info.Type.Substring(1) + size.ToString() + ".jpg"))
                {
                    var s = _iconConverter.GetImage(Info.FullName, size);
                    BitmapSource source = s as BitmapSource;
                    using (FileStream fileStream = new FileStream("IconCache/" + Info.Type.Substring(1) + size.ToString() + ".jpg", FileMode.Create))
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        encoder.QualityLevel = 100;
                        encoder.Save(fileStream);
                    }
                }
                Uri u = new Uri(Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "IconCache/" + Info.Type.Substring(1) + size.ToString() + ".jpg"));
                return new BitmapImage(u);
                /*if (icon == null || Info.Type == "")
                    return new BitmapImage(new Uri("http://icons.iconarchive.com/icons/gakuseisean/radium/256/file-icon.png"));
                if (!File.Exists("IconCache/" + Info.Type.Substring(1) + ".png"))
                {
                    FileStream s = File.OpenWrite("IconCache/" + Info.Type.Substring(1) + ".png");
                    icon.(s);
                    s.Close();
                }
                Uri u = new Uri(Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "IconCache/" + Info.Type.Substring(1) + ".png"));
                return new BitmapImage(u);*/
            }
        }

        private string GetDriveType(DriveFileSystemInfo info)
        {
            if (info.Drive.DriveType == DriveType.Fixed)
                return "Hard Drive";
            if (info.Drive.DriveType == DriveType.CDRom)
                return "CD Rom";
            return info.Drive.DriveType.ToString();
        }
        public string Size { get; set; }
        public double ByteSize { get; set; }
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
        public static bool OpenFoldersInNewWindow = true;
        public static bool ShowExtensions = false;
        public static double FadeInTime = 0.25;
        public static double WindowFadeInTime = 0.4;
        public static double FontSize = 12;
        public static FontStretch FontStretch = FontStretches.UltraCondensed;
        public static string FontName = "Segoe UI";
        public static Brush TextColorBrush = new SolidColorBrush(Colors.Black);
        public static Color ItemBackColor1 = (Color)ColorConverter.ConvertFromString("#FFEEF7FA");
        public static Color ItemBackColor2 = (Color)ColorConverter.ConvertFromString("#00FFFFFF");
    }

    #endregion

    #region Helper classes

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
