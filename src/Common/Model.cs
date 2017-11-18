using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileSeeker.Common
{
    public abstract class Model : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
