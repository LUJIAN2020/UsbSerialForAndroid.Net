using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace UsbSerialForAndroid.Net.Extensions;

public static class UsbRequestExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueueReq(this UsbRequest req, ByteBuffer buffer)
    {
        if (!(OperatingSystem.IsAndroidVersionAtLeast(26) ?
            req.Queue(buffer) : req.Queue(buffer, buffer.Capacity())))
            throw new IOException("Error queueing request.");
    }
}
