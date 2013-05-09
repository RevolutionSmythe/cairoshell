using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CairoDesktop.Interop;

namespace CairoDesktop
{
    /// <summary>
    /// Used by XAML to convert images
    /// </summary>
    [ValueConversion(typeof(System.Drawing.Icon), typeof(ImageSource))]
    public class IconConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return Imaging.GetImageFromHIcon(IntPtr.Zero);

            if (value is Icon)
                return Imaging.GetImageFromHIcon((value as Icon).Handle);
            else
                return Imaging.CreateBitmapSourceFromBitmap(value as Bitmap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    public static class Imaging
    {
        public static System.Drawing.Icon ExtractIcon(string fileName, IntPtr HWnd)
        {
            System.Drawing.Icon ico = null;

            try
            {
                ico = Etier.IconHelper.IconReader.GetFileIcon(fileName, Etier.IconHelper.IconReader.IconSize.Large, false);
                return ico;
            }
            catch { }
            Debug.Write("Failed to get permissions to access process, falling back to small ico only.");
            uint iconHandle = NativeMethods.GetIconForWindow(HWnd);

            try
            {
                ico = System.Drawing.Icon.FromHandle(new IntPtr(iconHandle));
            }
            catch (Exception)
            {
                ico = null;
            }
            return ico;
        }

        /// <summary>
        /// Converts Bitmap to BitmapSource
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static BitmapSource CreateBitmapSourceFromBitmap(System.Drawing.Image bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException("bitmap");

            if (System.Windows.Application.Current.Dispatcher == null)
                return null; // Is it possible?

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    // You need to specify the image format to fill the stream. 
                    // I'm assuming it is PNG
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    // Make sure to create the bitmap in the UI thread
                    return (BitmapSource)System.Windows.Application.Current.Dispatcher.Invoke(
                            new Func<Stream, BitmapSource>(CreateBitmapSourceFromBitmap),
                            System.Windows.Threading.DispatcherPriority.Normal,
                            memoryStream);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static BitmapSource CreateBitmapSourceFromBitmap(Stream stream)
        {
            BitmapDecoder bitmapDecoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            // This will disconnect the stream from the image completely...
            WriteableBitmap writable = new WriteableBitmap(bitmapDecoder.Frames.Single());
            writable.Freeze();
            return writable;
        }

        /// <summary>
        /// Retrieves the Icon for the file name as an ImageSource
        /// </summary>
        /// <param name="filename">The filename of the file to query the Icon for.</param>
        /// <returns>The icon as an ImageSource, otherwise a default image.</returns>
        public static ImageSource GetImageFromAssociatedIcon(string filename)
        {
            BitmapSource bs = null;

            try
            {
                // Disposes of icon automagically when done.
                using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filename))
                {
                    if (icon == null || icon.Handle == null)
                    {
                        return GetDefaultIcon();
                    }

                    bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch (Exception)
            {
                bs = GetDefaultIcon();
            }

            return bs;
        }

        /// <summary>
        /// Retrieves the Icon for the Handle provided as an ImageSource.
        /// </summary>
        /// <param name="hIcon">The icon's handle (HICON).</param>
        /// <returns>The Icon, or a default icon if not found.</returns>
        public static ImageSource GetImageFromHIcon(IntPtr hIcon)
        {
            BitmapSource bs = null;
            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                catch (Exception)
                {
                    bs = GetDefaultIcon();
                }
            }
            else
            {
                bs = GetDefaultIcon();
            }

            return bs;
        }

        /// <summary>
        /// Creates an empty bitmap source in the size of an Icon.
        /// </summary>
        /// <returns>Empty icon bitmap.</returns>
        private static BitmapSource GenerateEmptyBitmapSource()
        {
            int width = 16;
            int height = width;
            int stride = width / 4;
            byte[] pixels = new byte[height * stride];

            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Indexed1,
                BitmapPalettes.WebPalette, pixels, stride);
        }

        /// <summary>
        /// Gets the default icon from the resources.
        /// If this fails (e.g. the resource is missing or corrupt) the empty icon is returned.
        /// </summary>
        /// <returns>The default icon as a BitmapSource.</returns>
        public static BitmapSource GetDefaultIcon()
        {
            BitmapImage img = new BitmapImage();
            try
            {
                img.BeginInit();
                img.UriSource = new Uri("resources\\folderIcon.png", UriKind.RelativeOrAbsolute);
                img.EndInit();
            }
            catch (Exception)
            {
                return GenerateEmptyBitmapSource();
            }

            return img;
        }
    }

    /// <summary>
    /// Provides static conversion methods to change Win32 Icons into ImageSources.
    /// </summary>
    public class WpfWin32ImageConverter
    {
        
    }
}
