using FileSeeker.Common;
using FileSeeker.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileSeeker
{
    public static class SearchEngine
    {
        public static IObservable<SearchResult> Search(string[] searchFor, string searchPattern, string path, bool recursive, bool matchCase, bool useRegularExpression, Encoding encoding)
        {
            return new ObserverableSearcher()
            {
                SearchFor = searchFor,
                Encoding = encoding,
                Path = path,
                Recursive = recursive,
                SearchPattern = searchPattern,
                MatchCase = matchCase,
                UseRegularExpression = useRegularExpression
            };
        }
        public static IObservable<SearchResult> Replace(string[] searchFor, string replaceWith, string searchPattern, string path, bool recursive, bool matchCase, bool useRegularExpression, Encoding encoding)
        {
            return new ObservableReplacer()
            {
                SearchFor = searchFor,
                ReplaceWith = replaceWith,
                Encoding = encoding,
                Path = path,
                Recursive = recursive,
                SearchPattern = searchPattern,
                MatchCase = matchCase,
                UseRegularExpression = useRegularExpression
            };
        }
    }

    internal class ObservableReplacer : ObserverableSearcher
    {
        public string ReplaceWith { get; set; }

        protected override void OnMatchesFound(Regex regex, ref string data)
        {
            data = regex.Replace(data, ReplaceWith);
            base.OnMatchesFound(regex, ref data);
        }

        protected override void OnSearchComplete(string file, Encoding detectedEncoding, ref string data)
        {
            using (var streamWriter = new StreamWriter(file, false, detectedEncoding))
            {
                streamWriter.Write(data);
                streamWriter.Flush();
            }
            base.OnSearchComplete(file, detectedEncoding, ref data);
        }
    }

    internal class ObserverableSearcher : IObservable<SearchResult>
    {
        public string[] SearchFor { get; set; }
        public string Path { get; set; }
        public string SearchPattern { get; set; }
        public bool Recursive { get; set; }
        public bool MatchCase { get; set; }
        public bool UseRegularExpression { get; set; }
        public Encoding Encoding { get; set; }

        public IDisposable Subscribe(IObserver<SearchResult> observer)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => Go(observer, cancellationTokenSource.Token));
            return new DisposableCancelationTokenSource(cancellationTokenSource);
        }

        protected virtual void OnSkipDirectory(string path)
        { }

        protected virtual void OnSkipSubDirectories(string path)
        { }

        protected virtual void OnSkipFile(string path)
        { }

        IEnumerable<string> GetAllFiles(string path)
        {
            IEnumerable<string> files = null;
            try
            {
                files = System.IO.Directory.GetFiles(path, SearchPattern);
            }
            catch (UnauthorizedAccessException)
            {
                OnSkipDirectory(path);
            }
            if (files != null)
            {
                foreach (var file in files)
                {
                    yield return file;
                }
            }

            IEnumerable<string> directories = null;
            try
            {
                directories = System.IO.Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                OnSkipSubDirectories(path);
            }
            if (directories != null)
            {
                foreach (var directory in directories)
                {
                    foreach (var file in GetAllFiles(directory))
                    {
                        yield return file;
                    }
                }
            }
        }

        void Go(IObserver<SearchResult> observer, CancellationToken cancellationToken)
        {
            try
            {
                // this will throw if any folder has permission issues
                //Directory.EnumerateFiles(Path, SearchPattern, Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                foreach (var file in GetAllFiles(Path))
                {
                    var result = new SearchResult(file);
                    if (cancellationToken.CheckIfCancelled(observer)) return;
                    try
                    {
                        observer.OnNext(result);
                        SearchFile(file, result);
                        result.Status = SearchStatus.Complete;
                    }
                    catch (IOException)
                    {
                        OnSkipFile(file);
                        result.Status = SearchStatus.Error;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        OnSkipFile(file);
                        result.Status = SearchStatus.Error;
                    }
                    if (cancellationToken.CheckIfCancelled(observer)) return;
                    observer.OnNext(result);
                }

                if (cancellationToken.CheckIfCancelled(observer)) return;
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        }

        void SearchFile(string file, SearchResult result)
        {
            Func<StreamReader> GetStreamReader = () =>
            {
                if (Encoding == null)
                    return new StreamReader(file, true);
                else
                    return new StreamReader(file, Encoding, true);
            };

            Encoding detectedEncoding;
            string data;

            using (var streamReader = GetStreamReader())
            {
                data = streamReader.ReadToEnd();
                detectedEncoding = streamReader.CurrentEncoding;
                foreach (var searchText in SearchFor)
                {
                    string searchString;
                    if (!UseRegularExpression)
                        searchString = Regex.Escape(searchText);
                    else
                        searchString = searchText;
                    var regex = new Regex(searchString, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);

                    var matches = regex.Matches(data);
                    if (matches.Count > 0)
                    {
                        result.Occurrences += matches.Count;
                        OnMatchesFound(regex, ref data);
                    }
                }
            }

            if (result.Occurrences > 0)
            {
                OnSearchComplete(file, detectedEncoding, ref data);
            }
        }

        protected virtual void OnSearchComplete(string file, Encoding detectedEncoding, ref string data)
        { }

        protected virtual void OnMatchesFound(Regex regex, ref string data)
        { }
    }

    internal class DisposableCancelationTokenSource : IDisposable
    {
        CancellationTokenSource cts;

        internal DisposableCancelationTokenSource(CancellationTokenSource cts)
        {
            this.cts = cts;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
                IsDisposed = true;
            }
        }

        public bool IsDisposed { get; private set; }
    }

    public sealed class SearchResult : Model
    {
        internal SearchResult(string file)
        {
            File = file;
            Status = SearchStatus.Searching;
        }
        public string File { get; private set; }

        int occurrences;
        public int Occurrences
        {
            get { return occurrences; }
            internal set
            {
                occurrences = value;
                OnPropertyChanged("Occurrences");
            }
        }

        SearchStatus status;
        public SearchStatus Status
        {
            get { return status; }
            internal set
            {
                status = value;
                OnPropertyChanged("Status"); 
            }
        }
    }

    public enum SearchStatus
    {
        Searching,
        Complete,
        Error
    }
}
