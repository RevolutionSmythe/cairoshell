using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace CairoDesktop
{
    public interface ICairoNotifyPropertyChanged : INotifyPropertyChanged
    {
        void OnPropertyChanged(string PropertyName);
    }
}
