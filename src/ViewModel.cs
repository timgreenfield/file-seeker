using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FileSeeker.Common;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace FileSeeker
{
    public class ViewModel : Model
    {
        CancellationTokenSource cts;
        bool isBusy;
        RelayCommand cancelCommand;
        RelayCommand openCommand;
        RelayCommand searchCommand;
        RelayCommand replaceCommand;
        string searchPattern;
        string path;
        string searchFor;
        string replaceWith;
        bool multipleValues;
        bool matchCase;
        bool recursive = true;
        bool useRegularExpressions;
        EncodingOption selectedEncoding;
        IList<SearchResult> searchResults;

        public ViewModel(CoreDispatcher dispatcher)
            : base(dispatcher)
        {
            Dispatcher = dispatcher;

            AvailableEncodings = new List<EncodingOption>(Encoding.GetEncodings().OrderBy(e => e.DisplayName).Select(e => new EncodingOption(e)));
            AvailableEncodings.Insert(0, new EncodingOption(null));

            LoadPreferences();
        }

        public CoreDispatcher Dispatcher { get; private set; }

        public bool IsBusy
        {
            get { return isBusy; }
            set
            {
                isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                cancelCommand.RaiseCanExecuteChanged();
                searchCommand.RaiseCanExecuteChanged();
                replaceCommand.RaiseCanExecuteChanged();
            }
        }

        bool IsCancelling
        {
            get
            {
                return IsBusy && cts?.IsCancellationRequested == true;
            }
        }

        private void LoadPreferences()
        {
            searchPattern = Settings.SearchPattern;
            searchFor = Settings.SearchFor;
            replaceWith = Settings.ReplaceWith;
            path = Settings.Path;
            recursive = Settings.Recursive;
            matchCase = Settings.MatchCase;
            useRegularExpressions = Settings.UseRegularExpressions;
            multipleValues = Settings.MultipleValues;
            var encodingName = Settings.SelectedEncoding;
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
            Settings.SearchPattern = SearchPattern;
            Settings.SearchFor = SearchFor;
            Settings.ReplaceWith = ReplaceWith;
            Settings.Path = Path;
            Settings.Recursive = Recursive;
            Settings.MatchCase = MatchCase;
            Settings.UseRegularExpressions = UseRegularExpressions;
            Settings.MultipleValues = MultipleValues;
            Settings.SelectedEncoding = SelectedEncoding.EncodingInfo != null ? SelectedEncoding.EncodingInfo.Name : string.Empty;
        }

        public ICommand CancelCommand
        {
            get
            {
                if (cancelCommand == null)
                    cancelCommand = new RelayCommand(() =>
                    {
                        if (cts != null)
                        {
                            cts.Cancel();
                            cancelCommand.RaiseCanExecuteChanged();
                        }
                    }, () => !IsCancelling);
                return cancelCommand;
            }
        }

        public ICommand OpenCommand
        {
            get
            {
                if (openCommand == null)
                    openCommand = new RelayCommand(arg =>
                    {
                        if (arg is SearchResult searchResult)
                        {
                            _ = Launcher.LaunchFileAsync(searchResult.File);
                        }
                    });
                return openCommand;
            }
        }

        public ICommand SearchCommand
        {
            get
            {
                if (searchCommand == null)
                    searchCommand = new RelayCommand(async () =>
                    {
                        if (string.IsNullOrWhiteSpace(Path))
                        {
                            await new MessageDialog("Please provide a path to search before proceeding.").ShowAsync();
                            return;
                        }

                        var request = CreateSearchRequest<SearchRequest>();

                        using (cts = new CancellationTokenSource())
                        {
                            IsBusy = true;
                            try
                            {
                                await SearchAsync(request, cts.Token);
                            }
                            finally
                            {
                                IsBusy = false;
                            }
                        }
                        cts = null;
                    }, () => !IsBusy);
                return searchCommand;
            }
        }

        public ICommand ReplaceCommand
        {
            get
            {
                if (replaceCommand == null)
                    replaceCommand = new RelayCommand(async () =>
                    {
                        if (string.IsNullOrWhiteSpace(Path))
                        {
                            await new MessageDialog("Please provide a path to search before proceeding.").ShowAsync();
                            return;
                        }

                        var msgDialog = new MessageDialog("This is a very powerful tool that you can use to alter data in any file that you have permission to modify. Therefore, Be VERY careful when doing replace operations to ensure you don’t accidentally modify files you don't intend to. Replace operations cannot be undone so double check your settings and backup your files before proceeding.", "WARNING");
                        var proceedCommand = new UICommand("Proceed");
                        msgDialog.Commands.Add(proceedCommand);
                        msgDialog.Commands.Add(new UICommand("Cancel"));
                        msgDialog.DefaultCommandIndex = 1;
                        if (await msgDialog.ShowAsync() == proceedCommand)
                        {
                            var request = CreateSearchRequest<SearchAndReplaceRequest>();
                            if (MultipleValues)
                            {
                                request.ReplaceWith = ReplaceWith.Replace(Environment.NewLine, "\r").Split('\r', StringSplitOptions.RemoveEmptyEntries);
                            }
                            else
                            {
                                request.ReplaceWith = new[] { ReplaceWith };
                            }

                            using (cts = new CancellationTokenSource())
                            {
                                IsBusy = true;
                                try
                                {
                                    await SearchAsync(request, cts.Token);
                                }
                                finally
                                {
                                    IsBusy = false;
                                }
                            }
                            cts = null;
                        }
                    }, () => !IsBusy);
                return replaceCommand;
            }
        }

        private async Task SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            SavePreferences();

            SearchResults = new ObservableCollection<SearchResult>();
            var progress = new Progress<SearchResult>(result =>
            {
                switch (result.Status)
                {
                    case SearchStatus.Searching:
                        SearchResults.Add(result);
                        break;
                    case SearchStatus.Complete:
                        if (result.Occurrences == 0)
                        {
                            SearchResults.Remove(result);
                        }
                        else
                        {
                            OnPropertyChanged(nameof(TotalOccurrences));
                        }
                        break;
                    case SearchStatus.Error:
                        SearchResults.Remove(result);
                        break;
                    case SearchStatus.Canceled:
                        SearchResults.Remove(result);
                        break;
                }
            });

            try
            {
                await SearchEngine.SearchAsync(request, Dispatcher, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignore, we aborted operation
            }
            catch (Exception x)
            {
                await new MessageDialog(x.Message, "Unable to complete operation.").ShowAsync();
            }
        }

        private T CreateSearchRequest<T>() where T : SearchRequest, new()
        {
            string[] searchForValues;
            if (MultipleValues)
            {
                searchForValues = SearchFor.Replace(Environment.NewLine, "\r").Split('\r', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                searchForValues = new[] { SearchFor };
            }

            var request = new T()
            {
                SearchFor = searchForValues,
                Encoding = SelectedEncoding.EncodingInfo?.GetEncoding(),
                Path = path,
                Recursive = recursive,
                FileTypeFilter = string.IsNullOrWhiteSpace(searchPattern) ? new string[] { "*" } : searchPattern.Split(','),
                MatchCase = matchCase,
                UseRegularExpression = UseRegularExpressions
            };
            return request;
        }

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                searchPattern = value;
                OnPropertyChanged(nameof(SearchPattern));
            }
        }

        public string Path
        {
            get { return path; }
            set
            {
                path = value;
                OnPropertyChanged(nameof(Path));
            }
        }

        public string SearchFor
        {
            get { return searchFor; }
            set
            {
                searchFor = value;
                OnPropertyChanged(nameof(SearchFor));
            }
        }

        public string ReplaceWith
        {
            get { return replaceWith; }
            set
            {
                replaceWith = value;
                OnPropertyChanged(nameof(ReplaceWith));
            }
        }

        public bool MultipleValues
        {
            get { return multipleValues; }
            set
            {
                multipleValues = value;
                OnPropertyChanged(nameof(MultipleValues));
            }
        }

        public bool MatchCase
        {
            get { return matchCase; }
            set
            {
                matchCase = value;
                OnPropertyChanged(nameof(MatchCase));
            }
        }

        public bool Recursive
        {
            get { return recursive; }
            set
            {
                recursive = value;
                OnPropertyChanged(nameof(Recursive));
            }
        }

        public bool UseRegularExpressions
        {
            get { return useRegularExpressions; }
            set
            {
                useRegularExpressions = value;
                OnPropertyChanged(nameof(UseRegularExpressions));
            }
        }

        public EncodingOption SelectedEncoding
        {
            get { return selectedEncoding; }
            set
            {
                selectedEncoding = value;
                OnPropertyChanged(nameof(SelectedEncoding));
            }
        }

        public IList<SearchResult> SearchResults
        {
            get { return searchResults; }
            private set
            {
                searchResults = value;
                OnPropertyChanged(nameof(SearchResults));
            }
        }

        public int TotalOccurrences
        {
            get { return searchResults == null ? 0 : searchResults.Sum(sr => sr.Occurrences); }
        }

        public IList<EncodingOption> AvailableEncodings { get; private set; }
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
