using System;
using Windows.Storage;
using Windows.UI.Xaml.Data;

namespace FileSeeker.Converters
{
    public class FilenameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((IStorageFile)value).Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
