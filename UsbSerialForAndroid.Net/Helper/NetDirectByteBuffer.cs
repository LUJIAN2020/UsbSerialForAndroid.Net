using Android.Runtime;
using Java.Nio;
using System;
using System.Runtime.InteropServices;

namespace UsbSerialForAndroid.Net.Helper;

// Direct buffer does not require additional copying: line 320
// https://android.googlesource.com/platform/frameworks/base/+/master/core/java/android/hardware/usb/UsbRequest.java

public class NetDirectByteBuffer : Java.Lang.Object, IDisposable
{
    public object? ClientData;
    /// <summary>
    /// it`s not copy its just wrapper for array
    /// </summary>
    /// <param name="array"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    public NetDirectByteBuffer(byte[] array, int offset, int length)
        : base()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, array.Length - offset);
        _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        MemBuffer = MemoryMarshal.CreateFromPinnedArray(array, offset, length);
        IntPtr ndb = JNIEnv.NewDirectByteBuffer(_handle.AddrOfPinnedObject() + offset, length);
        ByteBuffer? jdb = Java.Lang.Object.GetObject<ByteBuffer>(ndb, JniHandleOwnership.TransferLocalRef);
        ArgumentNullException.ThrowIfNull(jdb);
        JavaBuffer = jdb;
    }
    /// <summary>
    /// creates a Net array, pinned it and makes a DirectByteBuffer from it
    /// </summary>
    /// <param name="capacity"></param>
    public NetDirectByteBuffer(int capacity = 512)
        : this(new byte[capacity], 0, capacity)
    {
    }

    public readonly Memory<byte> MemBuffer;
    public readonly ByteBuffer JavaBuffer;
    GCHandle _handle;

    public Java.Nio.Buffer? Rewind() => JavaBuffer.Rewind();
    public int Position
    {
        get => JavaBuffer.Position();
        set => JavaBuffer.Position(value);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            JavaBuffer.Dispose();
            _handle.Free();
        }
    }
}
