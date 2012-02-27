using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows;
using System.Runtime.InteropServices;
using vbAccelerator.Components.ImageList;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace CairoExplorer
{
    public enum IconSize
    {
        small, large, extraLarge, jumbo, thumbnail
    }

    public class FileToIconConverter
    {
        private static string imageFilter = ".jpg,.jpeg,.png,.gif";
        private static string exeFilter = ".exe,.lnk";
        private int defaultsize;

        public int DefaultSize { get { return defaultsize; } set { defaultsize = value; } }

        private class thumbnailInfo
        {
            public IconSize iconsize;
            public WriteableBitmap bitmap;
            public string fullPath;
            public thumbnailInfo(WriteableBitmap b, string path, IconSize size)
            {
                bitmap = b;
                fullPath = path;
                iconsize = size;
            }
        }


        #region Win32api
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        internal const uint SHGFI_ICON = 0x100;
        internal const uint SHGFI_TYPENAME = 0x400;
        internal const uint SHGFI_LARGEICON = 0x0; // 'Large icon
        internal const uint SHGFI_SMALLICON = 0x1; // 'Small icon
        internal const uint SHGFI_SYSICONINDEX = 16384;
        internal const uint SHGFI_USEFILEATTRIBUTES = 16;

        // <summary>
        /// Get Icons that are associated with files.
        /// To use it, use (System.Drawing.Icon myIcon = System.Drawing.Icon.FromHandle(shinfo.hIcon));
        /// hImgSmall = SHGetFileInfo(fName, 0, ref shinfo,(uint)Marshal.SizeOf(shinfo),Win32.SHGFI_ICON |Win32.SHGFI_SMALLICON);
        /// </summary>
        [DllImport("shell32.dll")]
        internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
                                                  ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        // <summary>
        /// Return large file icon of the specified file.
        /// </summary>
        internal static Icon GetFileIcon(string fileName, IconSize size)
        {
            SHFILEINFO shinfo = new SHFILEINFO();

            uint flags = SHGFI_SYSICONINDEX;
            if (fileName.IndexOf(":") == -1)
                flags = flags | SHGFI_USEFILEATTRIBUTES;
            if (size == IconSize.small)
                flags = flags | SHGFI_ICON | SHGFI_SMALLICON;
            else flags = flags | SHGFI_ICON;

            SHGetFileInfo(fileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            return Icon.FromHandle(shinfo.hIcon);
        }
        #endregion

        #region Static Tools

        private static void copyBitmap(BitmapSource source, WriteableBitmap target, bool dispatcher)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * ((source.Format.BitsPerPixel + 7) / 8);

            byte[] bits = new byte[height * stride];
            source.CopyPixels(bits, stride, 0);
            source = null;

            //original code.
            //writeBitmap.Dispatcher.Invoke(DispatcherPriority.Background,
            //    new ThreadStart(delegate
            //    {
            //        //UI Thread
            //        Int32Rect outRect = new Int32Rect(0, (int)(writeBitmap.Height - height) / 2, width, height);                    
            //        writeBitmap.WritePixels(outRect, bits, stride, 0);                                        
            //    }));

            //Bugfixes by h32

            if (dispatcher)
            {
                target.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new ThreadStart(delegate
                {
                    //UI Thread
                    var delta = target.Height - height;
                    var newWidth = width > target.Width ? (int)target.Width : width;
                    var newHeight = height > target.Height ? (int)target.Height : height;
                    Int32Rect outRect = new Int32Rect(0, (int)(delta >= 0 ? delta : 0) / 2, newWidth, newWidth);
                    try
                    {
                        target.WritePixels(outRect, bits, stride, 0);
                    }
                    catch (Exception)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }));
            }
            else
            {
                var delta = target.Height - height;
                var newWidth = width > target.Width ? (int)target.Width : width;
                var newHeight = height > target.Height ? (int)target.Height : height;
                Int32Rect outRect = new Int32Rect(0, (int)(delta >= 0 ? delta : 0) / 2, newWidth, newWidth);
                try
                {
                    target.WritePixels(outRect, bits, stride, 0);
                }
                catch (Exception)
                {
                    System.Diagnostics.Debugger.Break();
                }
            }
        }


        private static System.Drawing.Size getDefaultSize(IconSize size)
        {
            switch (size)
            {
                case IconSize.jumbo: return new System.Drawing.Size(256, 256);
                case IconSize.thumbnail: return new System.Drawing.Size(256, 256);
                case IconSize.extraLarge: return new System.Drawing.Size(48, 48);
                case IconSize.large: return new System.Drawing.Size(32, 32);
                default: return new System.Drawing.Size(16, 16);
            }

        }

        //http://blog.paranoidferret.com/?p=11 , modified a little.
        private static Bitmap resizeImage(Bitmap imgToResize, System.Drawing.Size size, int spacing)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)((sourceWidth * nPercent) - spacing * 4);
            int destHeight = (int)((sourceHeight * nPercent) - spacing * 4);

            int leftOffset = (int)((size.Width - destWidth) / 2);
            int topOffset = (int)((size.Height - destHeight) / 2);


            Bitmap b = new Bitmap(size.Width, size.Height);
            Graphics g = Graphics.FromImage((Image)b);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawLines(System.Drawing.Pens.Silver, new PointF[] {
                new PointF(leftOffset - spacing, topOffset + destHeight + spacing), //BottomLeft
                new PointF(leftOffset - spacing, topOffset -spacing),                 //TopLeft
                new PointF(leftOffset + destWidth + spacing, topOffset - spacing)});//TopRight

            g.DrawLines(System.Drawing.Pens.Gray, new PointF[] {
                new PointF(leftOffset + destWidth + spacing, topOffset - spacing),  //TopRight
                new PointF(leftOffset + destWidth + spacing, topOffset + destHeight + spacing), //BottomRight
                new PointF(leftOffset - spacing, topOffset + destHeight + spacing),}); //BottomLeft

            g.DrawImage(imgToResize, leftOffset, topOffset, destWidth, destHeight);
            g.Dispose();

            return b;
        }

        private static Bitmap resizeJumbo(Bitmap imgToResize, System.Drawing.Size size, int spacing)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = 80;
            int destHeight = 80;

            int leftOffset = (int)((size.Width - destWidth) / 2);
            int topOffset = (int)((size.Height - destHeight) / 2);


            Bitmap b = new Bitmap(size.Width, size.Height);
            Graphics g = Graphics.FromImage((Image)b);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawLines(System.Drawing.Pens.Silver, new PointF[] {
                new PointF(0 + spacing, size.Height - spacing), //BottomLeft
                new PointF(0 + spacing, 0 + spacing),                 //TopLeft
                new PointF(size.Width - spacing, 0 + spacing)});//TopRight

            g.DrawLines(System.Drawing.Pens.Gray, new PointF[] {
                new PointF(size.Width - spacing, 0 + spacing),  //TopRight
                new PointF(size.Width - spacing, size.Height - spacing), //BottomRight
                new PointF(0 + spacing, size.Height - spacing)}); //BottomLeft

            g.DrawImage(imgToResize, leftOffset, topOffset, destWidth, destHeight);
            g.Dispose();

            return b;
        }


        private static BitmapSource loadBitmap(Bitmap source)
        {
            IntPtr hBitmap = source.GetHbitmap();
            //Memory Leak fixes, for more info : http://social.msdn.microsoft.com/forums/en-US/wpf/thread/edcf2482-b931-4939-9415-15b3515ddac6/
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }

        }

        private static bool isImage(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            if (ext == "")
                return false;
            return (imageFilter.IndexOf(ext) != -1 && File.Exists(fileName));
        }

        private static bool isExecutable(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            if (ext == "")
                return false;
            return (exeFilter.IndexOf(ext) != -1 && File.Exists(fileName));
        }



        private static bool isFolder(string path)
        {
            return path.EndsWith("\\") || Directory.Exists(path);
        }

        private static string returnKey(string fileName, IconSize size)
        {
            string key = Path.GetExtension(fileName).ToLower();

            if (isExecutable(fileName))
                key = fileName.ToLower();
            if (isImage(fileName) && size == IconSize.thumbnail)
                key = fileName.ToLower();
            if (isFolder(fileName))
                key = fileName.ToLower();

            switch (size)
            {
                case IconSize.thumbnail: key += isImage(fileName) ? "+T" : "+J"; break;
                case IconSize.jumbo: key += "+J"; break;
                case IconSize.extraLarge: key += "+XL"; break;
                case IconSize.large: key += "+L"; break;
                case IconSize.small: key += "+S"; break;
            }
            return key;
        }
        #endregion

        #region Static Cache
        private static Dictionary<string, ImageSource> iconDic = new Dictionary<string, ImageSource>();
        private static SysImageList _imgList = new SysImageList(SysImageListSize.jumbo);

        private Bitmap loadJumbo(string lookup)
        {
            _imgList.ImageListSize = isVistaUp() ? SysImageListSize.jumbo : SysImageListSize.extraLargeIcons;
            Icon icon = _imgList.Icon(_imgList.IconIndex(lookup, isFolder(lookup)));
            Bitmap bitmap = icon.ToBitmap();
            icon.Dispose();

            System.Drawing.Color empty = System.Drawing.Color.FromArgb(0, 0, 0, 0);

            if (bitmap.Width < 256)
                bitmap = resizeImage(bitmap, new System.Drawing.Size(256, 256), 0);
            else
                if (bitmap.GetPixel(100, 100) == empty && bitmap.GetPixel(200, 200) == empty && bitmap.GetPixel(200, 200) == empty)
                {
                    _imgList.ImageListSize = SysImageListSize.largeIcons;
                    bitmap = resizeJumbo(_imgList.Icon(_imgList.IconIndex(lookup)).ToBitmap(), new System.Drawing.Size(200, 200), 5);
                }

            return bitmap;
        }

        #endregion

        #region Instance Cache
        private static Dictionary<string, ImageSource> thumbDic = new Dictionary<string, ImageSource>();

        public void ClearInstanceCache()
        {
            thumbDic.Clear();
            //System.GC.Collect();
        }


        private void PollIconCallback(object state)
        {


            thumbnailInfo input = state as thumbnailInfo;
            string fileName = input.fullPath;
            WriteableBitmap writeBitmap = input.bitmap;
            IconSize size = input.iconsize;

            Bitmap origBitmap = GetFileIcon(fileName, size).ToBitmap();
            Bitmap inputBitmap = origBitmap;
            if (size == IconSize.jumbo || size == IconSize.thumbnail)
                inputBitmap = resizeJumbo(origBitmap, getDefaultSize(size), 5);
            else inputBitmap = resizeImage(origBitmap, getDefaultSize(size), 0);

            BitmapSource inputBitmapSource = loadBitmap(inputBitmap);
            origBitmap.Dispose();
            inputBitmap.Dispose();

            copyBitmap(inputBitmapSource, writeBitmap, true);
        }

        private void PollThumbnailCallback(object state)
        {
            //Non UIThread
            thumbnailInfo input = state as thumbnailInfo;
            string fileName = input.fullPath;
            WriteableBitmap writeBitmap = input.bitmap;
            IconSize size = input.iconsize;

            try
            {
                Bitmap origBitmap = new Bitmap(fileName);
                Bitmap inputBitmap = resizeImage(origBitmap, getDefaultSize(size), 5);
                BitmapSource inputBitmapSource = loadBitmap(inputBitmap);
                origBitmap.Dispose();
                inputBitmap.Dispose();

                copyBitmap(inputBitmapSource, writeBitmap, true);
            }
            catch { }

        }

        private ImageSource addToDic(string fileName, IconSize size)
        {
            string key = returnKey(fileName, size);

            if (size == IconSize.thumbnail || isExecutable(fileName))
            {
                if (!thumbDic.ContainsKey(key))
                    lock (thumbDic)
                        thumbDic.Add(key, getImage(fileName, size));

                return thumbDic[key];
            }
            else
            {
                if (!iconDic.ContainsKey(key))
                    lock (iconDic)
                        iconDic.Add(key, getImage(fileName, size));
                return iconDic[key];
            }

        }

        public ImageSource GetImage(string fileName, IconSize size)
        {
            return addToDic(fileName, size);
        }

        #endregion

        #region Instance Tools

        public static bool isVistaUp()
        {
            return (Environment.OSVersion.Version.Major >= 6);
        }

        private BitmapSource getImage(string fileName, IconSize size)
        {
            Icon icon;
            string key = returnKey(fileName, size);
            string lookup = "aaa" + Path.GetExtension(fileName).ToLower();
            if (!key.StartsWith("."))
                lookup = fileName;

            if (isExecutable(fileName))
            {

                WriteableBitmap bitmap = new WriteableBitmap(addToDic("aaa.exe", size) as BitmapSource);
                System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(PollIconCallback), new thumbnailInfo(bitmap, fileName, size));
                return bitmap;
            }

            else
                switch (size)
                {
                    case IconSize.thumbnail:
                        if (isImage(fileName))
                        {
                            //Load as jumbo icon first.                         
                            WriteableBitmap bitmap = new WriteableBitmap(addToDic(fileName, IconSize.jumbo) as BitmapSource);
                            //BitmapSource bitmapSource = addToDic(fileName, IconSize.jumbo) as BitmapSource;                            
                            //WriteableBitmap bitmap = new WriteableBitmap(256, 256, 96, 96, PixelFormats.Bgra32, null);
                            //copyBitmap(bitmapSource, bitmap, false);
                            System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(PollThumbnailCallback), new thumbnailInfo(bitmap, fileName, size));
                            return bitmap;
                        }
                        else
                        {
                            return getImage(lookup, IconSize.jumbo);
                        }
                    case IconSize.jumbo:
                        return loadBitmap(loadJumbo(lookup));
                    case IconSize.extraLarge:
                        _imgList.ImageListSize = SysImageListSize.extraLargeIcons;
                        icon = _imgList.Icon(_imgList.IconIndex(lookup, isFolder(fileName)));
                        return loadBitmap(icon.ToBitmap());
                    default:
                        icon = GetFileIcon(lookup, size);
                        return loadBitmap(icon.ToBitmap());
                }
        }

        #endregion


        public FileToIconConverter()
        {
            this.defaultsize = 48;
        }
    }
}
namespace vbAccelerator.Components.ImageList
{

    #region Public Enumerations
    /// <summary>
    /// Available system image list sizes
    /// </summary>
    public enum SysImageListSize : int
    {
        /// <summary>
        /// System Large Icon Size (typically 32x32)
        /// </summary>
        largeIcons = 0x0,
        /// <summary>
        /// System Small Icon Size (typically 16x16)
        /// </summary>
        smallIcons = 0x1,
        /// <summary>
        /// System Extra Large Icon Size (typically 48x48).
        /// Only available under XP; under other OS the
        /// Large Icon ImageList is returned.
        /// </summary>
        extraLargeIcons = 0x2,
        jumbo = 0x4
    }

    /// <summary>
    /// Flags controlling how the Image List item is 
    /// drawn
    /// </summary>
    [Flags]
    public enum ImageListDrawItemConstants : int
    {
        /// <summary>
        /// Draw item normally.
        /// </summary>
        ILD_NORMAL = 0x0,
        /// <summary>
        /// Draw item transparently.
        /// </summary>
        ILD_TRANSPARENT = 0x1,
        /// <summary>
        /// Draw item blended with 25% of the specified foreground colour
        /// or the Highlight colour if no foreground colour specified.
        /// </summary>
        ILD_BLEND25 = 0x2,
        /// <summary>
        /// Draw item blended with 50% of the specified foreground colour
        /// or the Highlight colour if no foreground colour specified.
        /// </summary>
        ILD_SELECTED = 0x4,
        /// <summary>
        /// Draw the icon's mask
        /// </summary>
        ILD_MASK = 0x10,
        /// <summary>
        /// Draw the icon image without using the mask
        /// </summary>
        ILD_IMAGE = 0x20,
        /// <summary>
        /// Draw the icon using the ROP specified.
        /// </summary>
        ILD_ROP = 0x40,
        /// <summary>
        /// Preserves the alpha channel in dest. XP only.
        /// </summary>
        ILD_PRESERVEALPHA = 0x1000,
        /// <summary>
        /// Scale the image to cx, cy instead of clipping it.  XP only.
        /// </summary>
        ILD_SCALE = 0x2000,
        /// <summary>
        /// Scale the image to the current DPI of the display. XP only.
        /// </summary>
        ILD_DPISCALE = 0x4000
    }

    /// <summary>
    /// Enumeration containing XP ImageList Draw State options
    /// </summary>
    [Flags]
    public enum ImageListDrawStateConstants : int
    {
        /// <summary>
        /// The image state is not modified. 
        /// </summary>
        ILS_NORMAL = (0x00000000),
        /// <summary>
        /// Adds a glow effect to the icon, which causes the icon to appear to glow 
        /// with a given color around the edges. (Note: does not appear to be
        /// implemented)
        /// </summary>
        ILS_GLOW = (0x00000001), //The color for the glow effect is passed to the IImageList::Draw method in the crEffect member of IMAGELISTDRAWPARAMS. 
        /// <summary>
        /// Adds a drop shadow effect to the icon. (Note: does not appear to be
        /// implemented)
        /// </summary>
        ILS_SHADOW = (0x00000002), //The color for the drop shadow effect is passed to the IImageList::Draw method in the crEffect member of IMAGELISTDRAWPARAMS. 
        /// <summary>
        /// Saturates the icon by increasing each color component 
        /// of the RGB triplet for each pixel in the icon. (Note: only ever appears
        /// to result in a completely unsaturated icon)
        /// </summary>
        ILS_SATURATE = (0x00000004), // The amount to increase is indicated by the frame member in the IMAGELISTDRAWPARAMS method. 
        /// <summary>
        /// Alpha blends the icon. Alpha blending controls the transparency 
        /// level of an icon, according to the value of its alpha channel. 
        /// (Note: does not appear to be implemented).
        /// </summary>
        ILS_ALPHA = (0x00000008) //The value of the alpha channel is indicated by the frame member in the IMAGELISTDRAWPARAMS method. The alpha channel can be from 0 to 255, with 0 being completely transparent, and 255 being completely opaque. 
    }

    /// <summary>
    /// Flags specifying the state of the icon to draw from the Shell
    /// </summary>
    [Flags]
    public enum ShellIconStateConstants
    {
        /// <summary>
        /// Get icon in normal state
        /// </summary>
        ShellIconStateNormal = 0,
        /// <summary>
        /// Put a link overlay on icon 
        /// </summary>
        ShellIconStateLinkOverlay = 0x8000,
        /// <summary>
        /// show icon in selected state 
        /// </summary>
        ShellIconStateSelected = 0x10000,
        /// <summary>
        /// get open icon 
        /// </summary>
        ShellIconStateOpen = 0x2,
        /// <summary>
        /// apply the appropriate overlays
        /// </summary>
        ShellIconAddOverlays = 0x000000020,
    }
    #endregion

    #region SysImageList
    /// <summary>
    /// Summary description for SysImageList.
    /// </summary>
    public class SysImageList : IDisposable
    {
        #region UnmanagedCode
        private const int MAX_PATH = 260;

        [DllImport("shell32")]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            int dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);

        private const int FILE_ATTRIBUTE_NORMAL = 0x80;
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;

        private const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100;
        private const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x2000;
        private const int FORMAT_MESSAGE_FROM_HMODULE = 0x800;
        private const int FORMAT_MESSAGE_FROM_STRING = 0x400;
        private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;
        private const int FORMAT_MESSAGE_MAX_WIDTH_MASK = 0xFF;
        [DllImport("kernel32")]
        private extern static int FormatMessage(
            int dwFlags,
            IntPtr lpSource,
            int dwMessageId,
            int dwLanguageId,
            string lpBuffer,
            uint nSize,
            int argumentsLong);

        [DllImport("kernel32")]
        private extern static int GetLastError();

        [DllImport("comctl32")]
        private extern static int ImageList_Draw(
            IntPtr hIml,
            int i,
            IntPtr hdcDst,
            int x,
            int y,
            int fStyle);

        [DllImport("comctl32")]
        private extern static int ImageList_DrawIndirect(
            ref IMAGELISTDRAWPARAMS pimldp);

        [DllImport("comctl32")]
        private extern static int ImageList_GetIconSize(
            IntPtr himl,
            ref int cx,
            ref int cy);

        [DllImport("comctl32")]
        private extern static IntPtr ImageList_GetIcon(
            IntPtr himl,
            int i,
            int flags);

        /// <summary>
        /// SHGetImageList is not exported correctly in XP.  See KB316931
        /// http://support.microsoft.com/default.aspx?scid=kb;EN-US;Q316931
        /// Apparently (and hopefully) ordinal 727 isn't going to change.
        /// </summary>
        [DllImport("shell32.dll", EntryPoint = "#727")]
        private extern static int SHGetImageList(
            int iImageList,
            ref Guid riid,
            ref IImageList ppv
            );

        [DllImport("shell32.dll", EntryPoint = "#727")]
        private extern static int SHGetImageListHandle(
            int iImageList,
            ref Guid riid,
            ref IntPtr handle
            );

        #endregion

        #region Private Enumerations
        [Flags]
        private enum SHGetFileInfoConstants : int
        {
            SHGFI_ICON = 0x100,                // get icon 
            SHGFI_DISPLAYNAME = 0x200,         // get display name 
            SHGFI_TYPENAME = 0x400,            // get type name 
            SHGFI_ATTRIBUTES = 0x800,          // get attributes 
            SHGFI_ICONLOCATION = 0x1000,       // get icon location 
            SHGFI_EXETYPE = 0x2000,            // return exe type 
            SHGFI_SYSICONINDEX = 0x4000,       // get system icon index 
            SHGFI_LINKOVERLAY = 0x8000,        // put a link overlay on icon 
            SHGFI_SELECTED = 0x10000,          // show icon in selected state 
            SHGFI_ATTR_SPECIFIED = 0x20000,    // get only specified attributes 
            SHGFI_LARGEICON = 0x0,             // get large icon 
            SHGFI_SMALLICON = 0x1,             // get small icon 
            SHGFI_OPENICON = 0x2,              // get open icon 
            SHGFI_SHELLICONSIZE = 0x4,         // get shell size icon 
            //SHGFI_PIDL = 0x8,                  // pszPath is a pidl 
            SHGFI_USEFILEATTRIBUTES = 0x10,     // use passed dwFileAttribute 
            SHGFI_ADDOVERLAYS = 0x000000020,     // apply the appropriate overlays
            SHGFI_OVERLAYINDEX = 0x000000040     // Get the index of the overlay
        }
        #endregion

        #region Private ImageList structures
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            int left;
            int top;
            int right;
            int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            int x;
            int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;        // x offest from the upperleft of bitmap
            public int yBitmap;        // y offset from the upperleft of bitmap
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGEINFO
        {
            public IntPtr hbmImage;
            public IntPtr hbmMask;
            public int Unused1;
            public int Unused2;
            public RECT rcImage;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public int dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        #endregion

        #region Private ImageList COM Interop (XP)
        [ComImportAttribute()]
        [GuidAttribute("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        //helpstring("Image List"),
        interface IImageList
        {
            [PreserveSig]
            int Add(
                IntPtr hbmImage,
                IntPtr hbmMask,
                ref int pi);

            [PreserveSig]
            int ReplaceIcon(
                int i,
                IntPtr hicon,
                ref int pi);

            [PreserveSig]
            int SetOverlayImage(
                int iImage,
                int iOverlay);

            [PreserveSig]
            int Replace(
                int i,
                IntPtr hbmImage,
                IntPtr hbmMask);

            [PreserveSig]
            int AddMasked(
                IntPtr hbmImage,
                int crMask,
                ref int pi);

            [PreserveSig]
            int Draw(
                ref IMAGELISTDRAWPARAMS pimldp);

            [PreserveSig]
            int Remove(
                int i);

            [PreserveSig]
            int GetIcon(
                int i,
                int flags,
                ref IntPtr picon);

            [PreserveSig]
            int GetImageInfo(
                int i,
                ref IMAGEINFO pImageInfo);

            [PreserveSig]
            int Copy(
                int iDst,
                IImageList punkSrc,
                int iSrc,
                int uFlags);

            [PreserveSig]
            int Merge(
                int i1,
                IImageList punk2,
                int i2,
                int dx,
                int dy,
                ref Guid riid,
                ref IntPtr ppv);

            [PreserveSig]
            int Clone(
                ref Guid riid,
                ref IntPtr ppv);

            [PreserveSig]
            int GetImageRect(
                int i,
                ref RECT prc);

            [PreserveSig]
            int GetIconSize(
                ref int cx,
                ref int cy);

            [PreserveSig]
            int SetIconSize(
                int cx,
                int cy);

            [PreserveSig]
            int GetImageCount(
                ref int pi);

            [PreserveSig]
            int SetImageCount(
                int uNewCount);

            [PreserveSig]
            int SetBkColor(
                int clrBk,
                ref int pclr);

            [PreserveSig]
            int GetBkColor(
                ref int pclr);

            [PreserveSig]
            int BeginDrag(
                int iTrack,
                int dxHotspot,
                int dyHotspot);

            [PreserveSig]
            int EndDrag();

            [PreserveSig]
            int DragEnter(
                IntPtr hwndLock,
                int x,
                int y);

            [PreserveSig]
            int DragLeave(
                IntPtr hwndLock);

            [PreserveSig]
            int DragMove(
                int x,
                int y);

            [PreserveSig]
            int SetDragCursorImage(
                ref IImageList punk,
                int iDrag,
                int dxHotspot,
                int dyHotspot);

            [PreserveSig]
            int DragShowNolock(
                int fShow);

            [PreserveSig]
            int GetDragImage(
                ref POINT ppt,
                ref POINT pptHotspot,
                ref Guid riid,
                ref IntPtr ppv);

            [PreserveSig]
            int GetItemFlags(
                int i,
                ref int dwFlags);

            [PreserveSig]
            int GetOverlayImage(
                int iOverlay,
                ref int piIndex);
        };
        #endregion

        #region Member Variables
        private IntPtr hIml = IntPtr.Zero;
        private IImageList iImageList = null;
        private SysImageListSize size = SysImageListSize.smallIcons;
        private bool disposed = false;
        #endregion

        #region Implementation

        #region Properties
        /// <summary>
        /// Gets the hImageList handle
        /// </summary>
        public IntPtr Handle
        {
            get
            {
                return this.hIml;
            }
        }
        /// <summary>
        /// Gets/sets the size of System Image List to retrieve.
        /// </summary>
        public SysImageListSize ImageListSize
        {
            get
            {
                return size;
            }
            set
            {
                size = value;
                create();
            }

        }

        /// <summary>
        /// Returns the size of the Image List Icons.
        /// </summary>
        public System.Drawing.Size Size
        {
            get
            {
                int cx = 0;
                int cy = 0;
                if (iImageList == null)
                {
                    ImageList_GetIconSize(
                        hIml,
                        ref cx,
                        ref cy);
                }
                else
                {
                    iImageList.GetIconSize(ref cx, ref cy);
                }
                System.Drawing.Size sz = new System.Drawing.Size(
                    cx, cy);
                return sz;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns a GDI+ copy of the icon from the ImageList
        /// at the specified index.
        /// </summary>
        /// <param name="index">The index to get the icon for</param>
        /// <returns>The specified icon</returns>
        public Icon Icon(int index)
        {
            Icon icon = null;

            IntPtr hIcon = IntPtr.Zero;
            if (iImageList == null)
            {
                hIcon = ImageList_GetIcon(
                    hIml,
                    index,
                    (int)ImageListDrawItemConstants.ILD_TRANSPARENT);

            }
            else
            {
                iImageList.GetIcon(
                    index,
                    (int)ImageListDrawItemConstants.ILD_TRANSPARENT,
                    ref hIcon);
            }

            if (hIcon != IntPtr.Zero)
            {
                icon = System.Drawing.Icon.FromHandle(hIcon);
            }
            return icon;
        }

        /// <summary>
        /// Return the index of the icon for the specified file, always using 
        /// the cached version where possible.
        /// </summary>
        /// <param name="fileName">Filename to get icon for</param>
        /// <returns>Index of the icon</returns>
        public int IconIndex(string fileName)
        {
            return IconIndex(fileName, false);
        }

        /// <summary>
        /// Returns the index of the icon for the specified file
        /// </summary>
        /// <param name="fileName">Filename to get icon for</param>
        /// <param name="forceLoadFromDisk">If True, then hit the disk to get the icon,
        /// otherwise only hit the disk if no cached icon is available.</param>
        /// <returns>Index of the icon</returns>
        public int IconIndex(
            string fileName,
            bool forceLoadFromDisk)
        {
            return IconIndex(
                fileName,
                forceLoadFromDisk,
                ShellIconStateConstants.ShellIconStateNormal);
        }

        /// <summary>
        /// Returns the index of the icon for the specified file
        /// </summary>
        /// <param name="fileName">Filename to get icon for</param>
        /// <param name="forceLoadFromDisk">If True, then hit the disk to get the icon,
        /// otherwise only hit the disk if no cached icon is available.</param>
        /// <param name="iconState">Flags specifying the state of the icon
        /// returned.</param>
        /// <returns>Index of the icon</returns>
        public int IconIndex(
            string fileName,
            bool forceLoadFromDisk,
            ShellIconStateConstants iconState
            )
        {
            SHGetFileInfoConstants dwFlags = SHGetFileInfoConstants.SHGFI_SYSICONINDEX;
            int dwAttr = 0;
            if (size == SysImageListSize.smallIcons)
            {
                dwFlags |= SHGetFileInfoConstants.SHGFI_SMALLICON;
            }

            // We can choose whether to access the disk or not. If you don't
            // hit the disk, you may get the wrong icon if the icon is
            // not cached. Also only works for files.
            if (!forceLoadFromDisk)
            {
                dwFlags |= SHGetFileInfoConstants.SHGFI_USEFILEATTRIBUTES;
                dwAttr = FILE_ATTRIBUTE_NORMAL;
            }
            else
            {
                dwAttr = 0;
            }

            // sFileSpec can be any file. You can specify a
            // file that does not exist and still get the
            // icon, for example sFileSpec = "C:\PANTS.DOC"
            SHFILEINFO shfi = new SHFILEINFO();
            uint shfiSize = (uint)Marshal.SizeOf(shfi.GetType());
            IntPtr retVal = SHGetFileInfo(
                fileName, dwAttr, ref shfi, shfiSize,
                ((uint)(dwFlags) | (uint)iconState));

            if (retVal.Equals(IntPtr.Zero))
            {
                System.Diagnostics.Debug.Assert((!retVal.Equals(IntPtr.Zero)), "Failed to get icon index");
                return 0;
            }
            else
            {
                return shfi.iIcon;
            }
        }

        /// <summary>
        /// Draws an image
        /// </summary>
        /// <param name="hdc">Device context to draw to</param>
        /// <param name="index">Index of image to draw</param>
        /// <param name="x">X Position to draw at</param>
        /// <param name="y">Y Position to draw at</param>
        public void DrawImage(
            IntPtr hdc,
            int index,
            int x,
            int y
            )
        {
            DrawImage(hdc, index, x, y, ImageListDrawItemConstants.ILD_TRANSPARENT);
        }

        /// <summary>
        /// Draws an image using the specified flags
        /// </summary>
        /// <param name="hdc">Device context to draw to</param>
        /// <param name="index">Index of image to draw</param>
        /// <param name="x">X Position to draw at</param>
        /// <param name="y">Y Position to draw at</param>
        /// <param name="flags">Drawing flags</param>
        public void DrawImage(
            IntPtr hdc,
            int index,
            int x,
            int y,
            ImageListDrawItemConstants flags
            )
        {
            if (iImageList == null)
            {
                int ret = ImageList_Draw(
                    hIml,
                    index,
                    hdc,
                    x,
                    y,
                    (int)flags);
            }
            else
            {
                IMAGELISTDRAWPARAMS pimldp = new IMAGELISTDRAWPARAMS();
                pimldp.hdcDst = hdc;
                pimldp.cbSize = Marshal.SizeOf(pimldp.GetType());
                pimldp.i = index;
                pimldp.x = x;
                pimldp.y = y;
                pimldp.rgbFg = -1;
                pimldp.fStyle = (int)flags;
                iImageList.Draw(ref pimldp);
            }

        }

        /// <summary>
        /// Draws an image using the specified flags and specifies
        /// the size to clip to (or to stretch to if ILD_SCALE
        /// is provided).
        /// </summary>
        /// <param name="hdc">Device context to draw to</param>
        /// <param name="index">Index of image to draw</param>
        /// <param name="x">X Position to draw at</param>
        /// <param name="y">Y Position to draw at</param>
        /// <param name="flags">Drawing flags</param>
        /// <param name="cx">Width to draw</param>
        /// <param name="cy">Height to draw</param>
        public void DrawImage(
            IntPtr hdc,
            int index,
            int x,
            int y,
            ImageListDrawItemConstants flags,
            int cx,
            int cy
            )
        {
            IMAGELISTDRAWPARAMS pimldp = new IMAGELISTDRAWPARAMS();
            pimldp.hdcDst = hdc;
            pimldp.cbSize = Marshal.SizeOf(pimldp.GetType());
            pimldp.i = index;
            pimldp.x = x;
            pimldp.y = y;
            pimldp.cx = cx;
            pimldp.cy = cy;
            pimldp.fStyle = (int)flags;
            if (iImageList == null)
            {
                pimldp.himl = hIml;
                int ret = ImageList_DrawIndirect(ref pimldp);
            }
            else
            {

                iImageList.Draw(ref pimldp);
            }
        }

        /// <summary>
        /// Draws an image using the specified flags and state on XP systems.
        /// </summary>
        /// <param name="hdc">Device context to draw to</param>
        /// <param name="index">Index of image to draw</param>
        /// <param name="x">X Position to draw at</param>
        /// <param name="y">Y Position to draw at</param>
        /// <param name="flags">Drawing flags</param>
        /// <param name="cx">Width to draw</param>
        /// <param name="cy">Height to draw</param>
        /// <param name="foreColor">Fore colour to blend with when using the 
        /// ILD_SELECTED or ILD_BLEND25 flags</param>
        /// <param name="stateFlags">State flags</param>
        /// <param name="glowOrShadowColor">If stateFlags include ILS_GLOW, then
        /// the colour to use for the glow effect.  Otherwise if stateFlags includes 
        /// ILS_SHADOW, then the colour to use for the shadow.</param>
        /// <param name="saturateColorOrAlpha">If stateFlags includes ILS_ALPHA,
        /// then the alpha component is applied to the icon. Otherwise if 
        /// ILS_SATURATE is included, then the (R,G,B) components are used
        /// to saturate the image.</param>
        public void DrawImage(
            IntPtr hdc,
            int index,
            int x,
            int y,
            ImageListDrawItemConstants flags,
            int cx,
            int cy,
            System.Drawing.Color foreColor,
            ImageListDrawStateConstants stateFlags,
            System.Drawing.Color saturateColorOrAlpha,
            System.Drawing.Color glowOrShadowColor
            )
        {
            IMAGELISTDRAWPARAMS pimldp = new IMAGELISTDRAWPARAMS();
            pimldp.hdcDst = hdc;
            pimldp.cbSize = Marshal.SizeOf(pimldp.GetType());
            pimldp.i = index;
            pimldp.x = x;
            pimldp.y = y;
            pimldp.cx = cx;
            pimldp.cy = cy;
            pimldp.rgbFg = System.Drawing.Color.FromArgb(0,
                foreColor.R, foreColor.G, foreColor.B).ToArgb();
            Console.WriteLine("{0}", pimldp.rgbFg);
            pimldp.fStyle = (int)flags;
            pimldp.fState = (int)stateFlags;
            if ((stateFlags & ImageListDrawStateConstants.ILS_ALPHA) ==
                ImageListDrawStateConstants.ILS_ALPHA)
            {
                // Set the alpha:
                pimldp.Frame = (int)saturateColorOrAlpha.A;
            }
            else if ((stateFlags & ImageListDrawStateConstants.ILS_SATURATE) ==
                ImageListDrawStateConstants.ILS_SATURATE)
            {
                // discard alpha channel:
                saturateColorOrAlpha = System.Drawing.Color.FromArgb(0,
                    saturateColorOrAlpha.R,
                    saturateColorOrAlpha.G,
                    saturateColorOrAlpha.B);
                // set the saturate color
                pimldp.Frame = saturateColorOrAlpha.ToArgb();
            }
            glowOrShadowColor = System.Drawing.Color.FromArgb(0,
                glowOrShadowColor.R,
                glowOrShadowColor.G,
                glowOrShadowColor.B);
            pimldp.crEffect = glowOrShadowColor.ToArgb();
            if (iImageList == null)
            {
                pimldp.himl = hIml;
                int ret = ImageList_DrawIndirect(ref pimldp);
            }
            else
            {

                iImageList.Draw(ref pimldp);
            }
        }

        /// <summary>
        /// Determines if the system is running Windows XP
        /// or above
        /// </summary>
        /// <returns>True if system is running XP or above, False otherwise</returns>
        private bool isXpOrAbove()
        {
            bool ret = false;
            if (Environment.OSVersion.Version.Major > 5)
            {
                ret = true;
            }
            else if ((Environment.OSVersion.Version.Major == 5) &&
                (Environment.OSVersion.Version.Minor >= 1))
            {
                ret = true;
            }
            return ret;
            //return false;
        }

        /// <summary>
        /// Creates the SystemImageList
        /// </summary>
        private void create()
        {
            // forget last image list if any:
            hIml = IntPtr.Zero;

            if (isXpOrAbove())
            {
                // Get the System IImageList object from the Shell:
                Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                int ret = SHGetImageList(
                    (int)size,
                    ref iidImageList,
                    ref iImageList
                    );
                // the image list handle is the IUnknown pointer, but 
                // using Marshal.GetIUnknownForObject doesn't return
                // the right value.  It really doesn't hurt to make
                // a second call to get the handle:
                SHGetImageListHandle((int)size, ref iidImageList, ref hIml);
            }
            else
            {
                // Prepare flags:
                SHGetFileInfoConstants dwFlags = SHGetFileInfoConstants.SHGFI_USEFILEATTRIBUTES | SHGetFileInfoConstants.SHGFI_SYSICONINDEX;
                if (size == SysImageListSize.smallIcons)
                {
                    dwFlags |= SHGetFileInfoConstants.SHGFI_SMALLICON;
                }
                // Get image list
                SHFILEINFO shfi = new SHFILEINFO();
                uint shfiSize = (uint)Marshal.SizeOf(shfi.GetType());

                // Call SHGetFileInfo to get the image list handle
                // using an arbitrary file:
                hIml = SHGetFileInfo(
                    ".txt",
                    FILE_ATTRIBUTE_NORMAL,
                    ref shfi,
                    shfiSize,
                    (uint)dwFlags);
                System.Diagnostics.Debug.Assert((hIml != IntPtr.Zero), "Failed to create Image List");
            }
        }
        #endregion

        #region Constructor, Dispose, Destructor
        /// <summary>
        /// Creates a Small Icons SystemImageList 
        /// </summary>
        public SysImageList()
        {
            create();
        }
        /// <summary>
        /// Creates a SystemImageList with the specified size
        /// </summary>
        /// <param name="size">Size of System ImageList</param>
        public SysImageList(SysImageListSize size)
        {
            this.size = size;
            create();
        }

        /// <summary>
        /// Clears up any resources associated with the SystemImageList
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Clears up any resources associated with the SystemImageList
        /// when disposing is true.
        /// </summary>
        /// <param name="disposing">Whether the object is being disposed</param>
        public virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (iImageList != null)
                    {
                        Marshal.ReleaseComObject(iImageList);
                    }
                    iImageList = null;
                }
            }
            disposed = true;
        }
        /// <summary>
        /// Finalise for SysImageList
        /// </summary>
        ~SysImageList()
        {
            Dispose(false);
        }

    }
        #endregion

        #endregion

    #endregion

    #region SysImageListHelper
    /// <summary>
    /// Helper Methods for Connecting SysImageList to Common Controls
    /// </summary>
    public class SysImageListHelper
    {
        #region UnmanagedMethods
        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETIMAGELIST = (LVM_FIRST + 3);

        private const int LVSIL_NORMAL = 0;
        private const int LVSIL_SMALL = 1;
        private const int LVSIL_STATE = 2;

        private const int TV_FIRST = 0x1100;
        private const int TVM_SETIMAGELIST = (TV_FIRST + 9);

        private const int TVSIL_NORMAL = 0;
        private const int TVSIL_STATE = 2;

        [DllImport("user32", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int wMsg,
            IntPtr wParam,
            IntPtr lParam);
        #endregion

        /// <summary>
        /// Associates a SysImageList with a ListView control
        /// </summary>
        /// <param name="listView">ListView control to associate ImageList with</param>
        /// <param name="sysImageList">System Image List to associate</param>
        /// <param name="forStateImages">Whether to add ImageList as StateImageList</param>
        public static void SetListViewImageList(
            ListView listView,
            SysImageList sysImageList,
            bool forStateImages
            )
        {
            IntPtr wParam = (IntPtr)LVSIL_NORMAL;
            if (sysImageList.ImageListSize == SysImageListSize.smallIcons)
            {
                wParam = (IntPtr)LVSIL_SMALL;
            }
            if (forStateImages)
            {
                wParam = (IntPtr)LVSIL_STATE;
            }
            SendMessage(
                listView.Handle,
                LVM_SETIMAGELIST,
                wParam,
                sysImageList.Handle);
        }

        /// <summary>
        /// Associates a SysImageList with a TreeView control
        /// </summary>
        /// <param name="treeView">TreeView control to associated ImageList with</param>
        /// <param name="sysImageList">System Image List to associate</param>
        /// <param name="forStateImages">Whether to add ImageList as StateImageList</param>
        public static void SetTreeViewImageList(
            TreeView treeView,
            SysImageList sysImageList,
            bool forStateImages
            )
        {
            IntPtr wParam = (IntPtr)TVSIL_NORMAL;
            if (forStateImages)
            {
                wParam = (IntPtr)TVSIL_STATE;
            }
            SendMessage(
                treeView.Handle,
                TVM_SETIMAGELIST,
                wParam,
                sysImageList.Handle);
        }
    }
    #endregion

}
