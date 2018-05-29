using System.ComponentModel;
using Windows.UI.Core;

namespace FileSeeker.Common
{
    public abstract class Model : INotifyPropertyChanged
    {
        readonly CoreDispatcher dispatcher;

        protected Model(CoreDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (dispatcher.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => OnPropertyChanged(propertyName));
            }
        }
    }
}
