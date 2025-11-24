using System;
using UsbSerialForAndroid.Net.Logging;

namespace UsbSerialForAndroid.Net.Helper;

public interface IAnyDisposable : IDisposable, IAsyncDisposable
{
}