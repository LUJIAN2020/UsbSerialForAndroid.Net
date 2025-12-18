using System;
using System.Threading;
using System.Threading.Tasks;

namespace UsbSerialForAndroid.Net.Helper;

public class BaseDisposable : IAnyDisposable
{
    private int _isDisposed = 0;
    public bool IsDisposed => 0 != _isDisposed;

    ~BaseDisposable()
    {
        if (IsDisposed)
            return;
        DisposeInternal(false).SynchronousWait();
    }
    public async ValueTask DisposeAsync()
    {
        await DisposeInternal(true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
    public async void Dispose()
    {
        await DisposeInternal(true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
    private async Task DisposeInternal(bool disposing)
    {
        if (0 != Interlocked.Exchange(ref _isDisposed, 1))
            return;
        try
        {
            if (!disposing)
                Console.WriteLine($"MEMORY LEAK: {GetType().FullName}");
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED Dispose {ex}");
        }
    }
    protected virtual void Dispose(bool disposing) { }
    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
