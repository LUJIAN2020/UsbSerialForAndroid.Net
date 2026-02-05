using System.Threading;
using System.Threading.Tasks;

namespace UsbSerialForAndroid.Net.Helper;
public static class SynchronousWaitExt
{
    public static void SynchronousWait(this Task t)
    {
        if (SynchronizationContext.Current == null && TaskScheduler.Current == TaskScheduler.Default)
            t.GetAwaiter().GetResult();
        else
            Task.Run(() => t).GetAwaiter().GetResult();
    }
    public static T SynchronousWait<T>(this Task<T> t)
    {
        if (SynchronizationContext.Current == null && TaskScheduler.Current == TaskScheduler.Default)
            return t.GetAwaiter().GetResult();
        return Task.Run(() => t).GetAwaiter().GetResult();
    }
}
