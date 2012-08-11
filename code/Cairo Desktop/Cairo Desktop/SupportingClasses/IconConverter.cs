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

namespace CairoDesktop
{
    [ValueConversion(typeof(System.Drawing.Icon), typeof(ImageSource))]
    public class IconConverter : IValueConverter
    {

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return AppGrabber.WpfWin32ImageConverter.GetImageFromHIcon(IntPtr.Zero);

            if (value is Icon)
                return AppGrabber.WpfWin32ImageConverter.GetImageFromHIcon((value as Icon).Handle);
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
    }
}
