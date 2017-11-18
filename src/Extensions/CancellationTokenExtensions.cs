using System;
using System.Threading;

namespace FileSeeker.Extensions
{
    internal static class CancellationTokenExtensions
    {
        public static bool CheckIfCancelled<T>(this CancellationToken c, IObserver<T> observer)
        {
            try
            {
                c.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                return true;
            }
            return false;
        }

    }
}
