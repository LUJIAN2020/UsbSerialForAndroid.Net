using System;

namespace UsbSerialForAndroid.Net.Logging;

public class LoggerAndroid : ILogger
{
    public void Debug(string msg)
    {
#if DEBUG
        Android.Util.Log.Debug("Debug", msg);
#endif
    }
    public void Error(string msg) => Android.Util.Log.Error("Error", msg);
    public void Error(Exception ex) => Android.Util.Log.Error("Error", ex.ToString());
    public void Trace(string msg) => Android.Util.Log.Verbose("Trace", msg);
    public void Warning(string msg) => Android.Util.Log.Warn("Warning", msg);
}