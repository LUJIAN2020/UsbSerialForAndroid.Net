using System;

namespace UsbSerialForAndroid.Net.Logging;

public interface ILogger
{
#if DEBUG
    public void Debug(string msg);
#endif
    void Trace(string msg);
    void Warning(string msg);
    void Error(string msg);
    void Error(Exception ex);
}

public interface IHaveLogger
{
    ILogger Logger { get; }
}