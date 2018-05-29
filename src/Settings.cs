using Windows.Storage;

namespace FileSeeker
{
    internal static class Settings 
    {
        public static string SearchFor
        {
            get
            {
                return (string)(ApplicationData.Current.LocalSettings.Values["SearchFor"]);
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SearchFor"] = value;
            }
        }
        
        public static string Path
        {
            get
            {
                return ((string)(ApplicationData.Current.LocalSettings.Values["Path"]));
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["Path"] = value;
            }
        }
        
        public static string ReplaceWith
        {
            get
            {
                return ((string)(ApplicationData.Current.LocalSettings.Values["ReplaceWith"]));
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ReplaceWith"] = value;
            }
        }
        
        public static string SearchPattern
        {
            get
            {
                return ((string)(ApplicationData.Current.LocalSettings.Values["SearchPattern"]));
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SearchPattern"] = value;
            }
        }
        
        public static bool MatchCase
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("MatchCase", out var value))
                {
                    return (bool)value;
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["MatchCase"] = value;
            }
        }
        
        public static bool Recursive
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("Recursive", out var value))
                {
                    return (bool)value;
                }
                return true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["Recursive"] = value;
            }
        }
        
        public static bool UseRegularExpressions
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("UseRegularExpressions", out var value))
                {
                    return (bool)value;
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["UseRegularExpressions"] = value;
            }
        }
        
        public static string SelectedEncoding
        {
            get
            {
                return ((string)(ApplicationData.Current.LocalSettings.Values["SelectedEncoding"]));
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SelectedEncoding"] = value;
            }
        }
        
        public static bool MultipleValues
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("MultipleValues", out var value))
                {
                    return (bool)value;
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["MultipleValues"] = value;
            }
        }
    }
}
