using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileSeeker.Common;
using Windows.Storage;
using Windows.UI.Core;

namespace FileSeeker
{
    public class SearchRequest
    {
        public string[] SearchFor { get; set; }
        public string Path { get; set; }
        public string[] FileTypeFilter { get; set; }
        public bool Recursive { get; set; }
        public bool MatchCase { get; set; }
        public bool UseRegularExpression { get; set; }
        public Encoding Encoding { get; set; }
    }

    public class SearchAndReplaceRequest : SearchRequest
    {
        public string[] ReplaceWith { get; set; }
    }

    public static class SearchEngine
    {
        public static async Task SearchAsync(SearchRequest request, CoreDispatcher dispatcher, IProgress<SearchResult> progress, CancellationToken cancellationToken)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(request.Path).AsTask(cancellationToken).ConfigureAwait(false);
            var allFiles = GetAllFiles(folder, request.FileTypeFilter, request.Recursive);

            var searchTasks = new List<Task<SearchResult>>();

            await allFiles.ForEachAsync(file =>
            {
                searchTasks.Add(SearchFileAndReportProgressAsync(file, cancellationToken));
            }, cancellationToken);

            await Task.WhenAll(searchTasks);

            async Task<SearchResult> SearchFileAndReportProgressAsync(IStorageFile file, CancellationToken c)
            {
                var result = new SearchResult(file, dispatcher);
                result.Status = SearchStatus.Searching;
                progress.Report(result);

                try
                {
                    int occurrences = await SearchFileAsync(file, request, c);
                    result.Occurrences = occurrences;
                    result.Status = SearchStatus.Complete;
                }
                catch (OperationCanceledException)
                {
                    result.Status = SearchStatus.Canceled;
                    throw;
                }
                catch
                {
                    result.Status = SearchStatus.Error;
                }
                finally
                {
                    progress.Report(result);
                }

                return result;
            }
        }

        static IObservable<IStorageFile> GetAllFiles(IStorageFolder folder, string[] fileTypeFilter, bool recursive)
        {
            return Observable.Create<IStorageFile>(async (observer, c) =>
            {
                await FindAllFilesAsync(observer, folder, c).ConfigureAwait(false);
            });

            async Task FindAllFilesAsync(IObserver<IStorageFile> observer, IStorageFolder inFolder, CancellationToken cancellationToken)
            {
                foreach (var file in await inFolder.GetFilesAsync().AsTask(cancellationToken).ConfigureAwait(false))
                {
                    if (fileTypeFilter.Contains(file.FileType))
                    {
                        observer.OnNext(file);
                    }
                }

                if (recursive)
                {
                    foreach (var childFolder in await inFolder.GetFoldersAsync().AsTask(cancellationToken).ConfigureAwait(false))
                    {
                        await FindAllFilesAsync(observer, childFolder, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        static async Task<int> SearchFileAsync(IStorageFile file, SearchRequest request, CancellationToken cancellationToken)
        {
            var streamToRead = await file.OpenStreamForReadAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            StreamReader GetStreamReader()
            {
                if (request.Encoding == null)
                    return new StreamReader(streamToRead, true);
                else
                    return new StreamReader(streamToRead, request.Encoding, true);
            };

            int occurrences = 0;
            string data;
            Encoding detectedEncoding;
            using (var streamReader = GetStreamReader())
            {
                detectedEncoding = streamReader.CurrentEncoding;
                data = streamReader.ReadToEnd();
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < request.SearchFor.Length; i++)
                {
                    var searchText = request.SearchFor[i];
                    string searchString;
                    if (!request.UseRegularExpression)
                        searchString = Regex.Escape(searchText);
                    else
                        searchString = searchText;
                    var regex = new Regex(searchString, request.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);

                    var matches = regex.Matches(data);
                    if (matches.Count > 0)
                    {
                        occurrences += matches.Count;
                        if (request is SearchAndReplaceRequest replaceRequest)
                        {
                            var replaceText = replaceRequest.ReplaceWith[i];
                            data = regex.Replace(data, replaceText);
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            if (request is SearchAndReplaceRequest)
            {
                if (occurrences > 0)
                {
                    var streamToWrite = await file.OpenStreamForWriteAsync().ConfigureAwait(false);
                    streamToWrite.SetLength(0);
                    using (var streamWriter = new StreamWriter(streamToWrite, detectedEncoding))
                    {
                        streamWriter.Write(data);
                        streamWriter.Flush();
                    }
                }
            }
            return occurrences;
        }
    }

    public sealed class SearchResult : Model
    {
        internal SearchResult(IStorageFile file, CoreDispatcher dispatcher)
            : base(dispatcher)
        {
            File = file;
        }

        public IStorageFile File { get; private set; }

        int occurrences;
        public int Occurrences
        {
            get { return occurrences; }
            internal set
            {
                occurrences = value;
                OnPropertyChanged(nameof(Occurrences));
            }
        }

        SearchStatus status;
        public SearchStatus Status
        {
            get { return status; }
            internal set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(IsFound));
            }
        }

        public bool IsFound
        {
            get { return Occurrences > 0 && Status == SearchStatus.Complete; }
        }

        public bool IsSearching
        {
            get { return Status == SearchStatus.Searching; }
        }
    }

    public enum SearchStatus
    {
        Searching,
        Complete,
        Error,
        Canceled
    }
}
