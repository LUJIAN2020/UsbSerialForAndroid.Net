using System;
using System.Diagnostics;

namespace UsbSerialForAndroid.Net.Logging;

public interface ILogger
{
    void Debug(string msg);
    void Trace(string msg);
    void Warning(string msg);
    void Error(string msg);
}

public static class ILoggerExt
{
    [Conditional("DEBUG")]
    public static void DebugCond(this ILogger logger, string msg) => logger.Debug(msg);
    [Conditional("TRACE")]
    public static void TraceCond(this ILogger logger, string msg) => logger.Trace(msg);
    public static void Error(this ILogger logger, Exception ex) => logger.Error($"{ex}");
}