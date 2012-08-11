﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using ManagedWinapi.Windows;

namespace CairoDesktop
{
    [ValueConversion(typeof(bool), typeof(Style))]
    public class TaskbuttonStyleConverter : IMultiValueConverter
    {
        #region IMultiValueConverter Members

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var fxElement = values[0] as FrameworkElement;
            if (fxElement == null) return null;

            // Default style is Inactive...
            var fxStyle = fxElement.FindResource("CairoTaskbarButtonInactiveStyle");
            if (values[1] == null)
            {
                // Default - couldn't get window state.
                return fxStyle;
            }

            //switch (winState)
            //{
            //    case ApplicationWindow.WindowState.Active:
                    fxStyle = fxElement.FindResource("CairoTaskbarButtonActiveStyle");
            //        break;
            //
            //    case ApplicationWindow.WindowState.Flashing:
            //        fxStyle = fxElement.FindResource("CairoTaskbarButtonFlashingStyle");
            //        break;
            //
            //    case ApplicationWindow.WindowState.Hidden:
            //        fxStyle = fxElement.FindResource("CairoTaskbarButtonActiveStyle");
            //        //fxStyle = fxElement.FindResource("CairoTaskbarButtonHiddenStyle");
            //        break;
            //}

            return fxStyle;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public static class EnumUtility
    {
        public static bool TryCast<T>(object value, out T result, T defaultValue)
           where T : struct // error CS0702: Constraint cannot be special class 'System.Enum'
        {
            result = defaultValue;
            try
            {
                result = (T)value;
                return true;
            }
            catch { }

            return false;
        }
    }
}
