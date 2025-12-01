using System;
using System.Diagnostics;
using System.Threading;

namespace UsbSerialForAndroid.Net.Logging;

public interface ILogger
{
    void Debug(string msg);
    void Trace(string msg);
    void Warning(string msg);
    void Error(string msg);
    void Error(Exception ex);
}

public class Logger
{
    public ILogger Impl
    {
        get => _impl;
        set => Interlocked.Exchange(ref _impl, value);
    }
    private ILogger _impl;
    public Logger(ILogger impl)
    {
        _impl = impl;
    }
    [Conditional("DEBUG")]
    public void Debug(string msg) => _impl.Debug(msg);
    [Conditional("TRACE")]
    public void Trace(string msg) => _impl.Trace(msg);
    public void Warning(string msg) => _impl.Warning(msg);
    public void Error(string msg) => _impl.Error(msg);
    public void Error(Exception ex) => _impl.Error(ex);
}
