using FileSeeker.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FileSeeker
{
    public class ViewModel : Model, IObserver<SearchResult>
    {
        IDisposable activeOperation;

        public ViewModel()
        {
            AvailableEncodings = new List<EncodingOption>(Encoding.GetEncodings().OrderBy(e => e.DisplayName).Select(e => new EncodingOption(e)));
            AvailableEncodings.Insert(0, new EncodingOption(null));

            LoadPreferences();
        }

        IDisposable ActiveOperation
        {
            get { return activeOperation; }
            set
            {
                activeOperation = value;
                cancelCommand.RaiseCanExecuteChanged();
            }
        }

        public Dispatcher Dispatcher { get; set; }

        public void OnNext(SearchResult result)
        {
            if (result.Status != SearchStatus.Error)
            {
                if (result.Status == SearchStatus.Searching)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ActiveFile = result.File;
                    }));
                }
                else if (result.Status == SearchStatus.Complete && result.Occurrences > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SearchResults.Add(result);
                        OnPropertyChanged("TotalOccurrences");
                    }));
                }
            }
        }
        public void OnCompleted()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IsBusy = false;
            }));
        }
        public void OnError(Exception error)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IsBusy = false;
                if (!(error is OperationCanceledException))
                {
                    MessageBox.Show(error.Message, "Unable to complete operation.");
                }
            }));
        }

        bool isBusy;
        public bool IsBusy
        {
            get { return isBusy; }
            set
            {
                isBusy = value;
                OnPropertyChanged("IsBusy");
                cancelCommand.RaiseCanExecuteChanged();
                searchCommand.RaiseCanExecuteChanged();
                replaceCommand.RaiseCanExecuteChanged();
            }
        }

        bool IsCancelling
        {
            get
            {
                return IsBusy && ActiveOperation == null;
            }
        }

        private void LoadPreferences()
        {
            searchPattern = Properties.Settings.Default.SearchPattern;
            searchFor = Properties.Settings.Default.SearchFor;
            replaceWith = Properties.Settings.Default.ReplaceWith;
            path = Properties.Settings.Default.Path;
            recursive = Properties.Settings.Default.Recursive;
            matchCase = Properties.Settings.Default.MatchCase;
            useRegularExpressions = Properties.Settings.Default.UseRegularExpressions;
            multipleValues = Properties.Settings.Default.MultipleValues;
            var encodingName = Properties.Settings.Default.SelectedEncoding;
            if (!string.IsNullOrEmpty(encodingName))
            {
                selectedEncoding = AvailableEncodings.FirstOrDefault(e => e.EncodingInfo != null && e.EncodingInfo.Name == encodingName);
            }
            if (selectedEncoding == null)
            {
                selectedEncoding = AvailableEncodings.FirstOrDefault();
            }
        }

        private void SavePreferences()
        {
            Properties.Settings.Default.SearchPattern = SearchPattern;
            Properties.Settings.Default.SearchFor = SearchFor;
            Properties.Settings.Default.ReplaceWith = ReplaceWith;
            Properties.Settings.Default.Path = Path;
            Properties.Settings.Default.Recursive = Recursive;
            Properties.Settings.Default.MatchCase = MatchCase;
            Properties.Settings.Default.UseRegularExpressions = UseRegularExpressions;
            Properties.Settings.Default.MultipleValues = MultipleValues;
            Properties.Settings.Default.SelectedEncoding = SelectedEncoding.EncodingInfo != null ? SelectedEncoding.EncodingInfo.Name : string.Empty;
            Properties.Settings.Default.Save();
        }

        RelayCommand cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                if (cancelCommand == null)
                    cancelCommand = new RelayCommand(() =>
                    {
                        if (ActiveOperation != null)
                        {
                            ActiveOperation.Dispose();
                            ActiveOperation = null;
                        }
                    }, () => !IsCancelling);
                return cancelCommand;
            }
        }

        RelayCommand openCommand;
        public ICommand OpenCommand
        {
            get
            {
                if (openCommand == null)
                    openCommand = new RelayCommand(arg =>
                    {
                        var searchResult = arg as SearchResult;
                        if (searchResult != null)
                        {
                            Process.Start("notepad.exe", searchResult.File);
                        }
                    });
                return openCommand;
            }
        }

        RelayCommand searchCommand;
        public ICommand SearchCommand
        {
            get
            {
                if (searchCommand == null)
                    searchCommand = new RelayCommand(() =>
                    {
                        IsBusy = true;

                        SavePreferences();

                        string[] searchForValues;
                        if (MultipleValues)
                        {
                            searchForValues = SearchFor.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        }
                        else
                        {
                            searchForValues = new[] { SearchFor };
                        }
                        var results = SearchEngine.Search(searchForValues, SearchPattern, Path, Recursive, MatchCase, UseRegularExpressions, SelectedEncoding.EncodingInfo != null ? SelectedEncoding.EncodingInfo.GetEncoding() : null);
                        SearchResults = new ObservableCollection<SearchResult>();
                        ActiveOperation = results.Subscribe(this);
                    }, () => !IsBusy);
                return searchCommand;
            }
        }

        RelayCommand replaceCommand;
        public ICommand ReplaceCommand
        {
            get
            {
                if (replaceCommand == null)
                    replaceCommand = new RelayCommand(() =>
                    {
                        IsBusy = true;

                        SavePreferences();

                        string[] searchForValues;
                        if (MultipleValues)
                        {
                            searchForValues = SearchFor.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        }
                        else
                        {
                            searchForValues = new[] { SearchFor };
                        }
                        var results = SearchEngine.Replace(searchForValues, ReplaceWith, SearchPattern, Path, Recursive, MatchCase, UseRegularExpressions, SelectedEncoding.EncodingInfo != null ? SelectedEncoding.EncodingInfo.GetEncoding() : null);
                        SearchResults = new ObservableCollection<SearchResult>();
                        ActiveOperation = results.Subscribe(this);
                    }, () => !IsBusy);
                return replaceCommand;
            }
        }

        string searchPattern;
        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                searchPattern = value;
                OnPropertyChanged("SearchPattern");
            }
        }

        string path;
        public string Path
        {
            get { return path; }
            set
            {
                path = value;
                OnPropertyChanged("Path");
            }
        }

        string searchFor;
        public string SearchFor
        {
            get { return searchFor; }
            set
            {
                searchFor = value;
                OnPropertyChanged("SearchFor");
            }
        }

        string replaceWith;
        public string ReplaceWith
        {
            get { return replaceWith; }
            set
            {
                replaceWith = value;
                OnPropertyChanged("ReplaceWith");
            }
        }

        bool multipleValues;
        public bool MultipleValues
        {
            get { return multipleValues; }
            set
            {
                multipleValues = value;
                OnPropertyChanged("MultipleValues");
            }
        }

        bool matchCase;
        public bool MatchCase
        {
            get { return matchCase; }
            set
            {
                matchCase = value;
                OnPropertyChanged("MatchCase");
            }
        }

        bool recursive = true;
        public bool Recursive
        {
            get { return recursive; }
            set
            {
                recursive = value;
                OnPropertyChanged("Recursive");
            }
        }

        bool useRegularExpressions;
        public bool UseRegularExpressions
        {
            get { return useRegularExpressions; }
            set
            {
                useRegularExpressions = value;
                OnPropertyChanged("UseRegularExpressions");
            }
        }

        EncodingOption selectedEncoding;
        public EncodingOption SelectedEncoding
        {
            get { return selectedEncoding; }
            set
            {
                selectedEncoding = value;
                OnPropertyChanged("SelectedEncoding");
            }
        }

        IList<SearchResult> searchResults;
        public IList<SearchResult> SearchResults
        {
            get { return searchResults; }
            private set
            {
                searchResults = value;
                OnPropertyChanged("SearchResults");
            }
        }

        public int TotalOccurrences
        {
            get { return searchResults == null ? 0 : searchResults.Sum(sr => sr.Occurrences); }
        }

        public IList<EncodingOption> AvailableEncodings { get; private set; }

        string activeFile;
        public string ActiveFile
        {
            get { return activeFile; }
            set
            {
                activeFile = value;
                OnPropertyChanged("ActiveFile");
            }
        }
    }

    public class EncodingOption
    {
        internal EncodingOption(EncodingInfo encodingInfo)
        {
            EncodingInfo = encodingInfo;
        }

        public EncodingInfo EncodingInfo { get; private set; }

        public override string ToString()
        {
            return EncodingInfo != null ? EncodingInfo.DisplayName : "Unknown";
        }
    }
}
