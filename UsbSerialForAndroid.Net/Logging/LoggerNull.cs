using System;

namespace UsbSerialForAndroid.Net.Logging;

public class LoggerNull : ILogger
{
    public void Debug(string msg) { }
    public void Error(string msg) { }
    public void Error(Exception ex) { }
    public void Trace(string msg) { }
    public void Warning(string msg) { }
}
