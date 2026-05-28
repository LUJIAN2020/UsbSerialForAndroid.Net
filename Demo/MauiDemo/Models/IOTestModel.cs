using Android.Util;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;

namespace MauiDemo.Models;

public partial class IOTestModel : ObservableObject
{
    public bool EnableWrite;
    [ObservableProperty] public partial string? WriteSpeed { get; set; }
    [ObservableProperty] public partial string? ReadSpeed { get; set; }
    public IOTestModel()
    {
        byte value = 0;
        for (int i = 0; i < SampleBuf.Length; i++)
            SampleBuf[i] = value++;
    }
    public static TimeSpan UpdatePreiod = TimeSpan.FromMilliseconds(1000);
    public const int SampleBufLength = 256;
    public readonly byte[] SampleBuf = new byte[SampleBufLength];

    public async Task StartTestAsync(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity,
        CancellationToken ct)
    {
        using var usbDriver = UsbDriverFactory.CreateUsbDriver(deviceId);
        var _stopBits = (UsbSerialForAndroid.Net.Enums.StopBits)stopBits;
        var _parity = (UsbSerialForAndroid.Net.Enums.Parity)parity;
        await usbDriver.OpenAsync(baudRate, dataBits, _stopBits, _parity);
        await Task.Delay(100, ct);
        await usbDriver.FlushAsync(ct);
        if (EnableWrite)
        {
            await Task.WhenAny(ExecReadAsync(usbDriver, ct), ExecWriteAsync(usbDriver, ct));
        }
        else
        {
            await ExecReadAsync(usbDriver, ct);
        }
    }
    public async Task ExecReadAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        try
        {
            await ReadAsync(usbDriver, ct);
        }
        catch (Exception ex)
        {
            PrintErr(ex);
        }
    }
    public async Task ExecWriteAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        try
        {
            await WriteAsync(usbDriver, ct);
        }
        catch (Exception ex)
        {
            PrintErr(ex);
        }
    }
    public async Task WriteAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        double speed = 0;
        long sentTotal = 0;
        long sentPrev = 0;
        var sw = Stopwatch.StartNew();
        TimeSpan tickPrev = sw.Elapsed;
        while (!ct.IsCancellationRequested)
        {
            TimeSpan now = sw.Elapsed;
            TimeSpan difTime = now - tickPrev;
            if (UpdatePreiod < difTime)
            {
                double difBytes = sentTotal - sentPrev;
                speed = (speed + (difBytes / difTime.TotalSeconds)) / 2;
                WriteSpeed = $"{speed:N0} byte/sec, sent total={sentTotal}";// {difBytes:N0}  {difTime}
                tickPrev = now;
                sentPrev = sentTotal;
            }
            if (SampleBufLength != await usbDriver.WriteAsync(SampleBuf, 0, SampleBuf.Length, ct))
                throw new Exception("Something write wrong");
            sentTotal += SampleBufLength;
        }
    }
    private async Task ReadAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        byte[] buf = new byte[SampleBufLength];
        double speed = 0;
        long readTotal = 0;
        long readPrev = 0;
        var sw = Stopwatch.StartNew();
        TimeSpan tickPrev = sw.Elapsed;
        while (!ct.IsCancellationRequested)
        {
            TimeSpan now = sw.Elapsed;
            TimeSpan difTime = now - tickPrev;
            if (UpdatePreiod < difTime)
            {
                double difBytes = readTotal - readPrev;
                speed = (speed + (difBytes / difTime.TotalSeconds)) / 2;
                ReadSpeed = $"{speed:N0} byte/sec read total={readTotal}";// {difBytes:N0}  {difTime}
                tickPrev = now;
                readPrev = readTotal;
            }
            int currOffset = 0;
            int currLen = SampleBufLength;
            while (0 < currLen)
            {
                int currReadLen = await usbDriver.ReadAsync(buf, currOffset, currLen, ct);
                currOffset += currReadLen;
                currLen -= currReadLen;
                readTotal += currReadLen;
            }
            //if (!testDataSample.SequenceEqual(buf))
            if (!IsSeq(buf))
                throw new Exception($"Read {readTotal} not equal write sequence " +
                    $"\n{BitConverter.ToString(buf)}");
        }
    }
    public static bool IsSeq(ReadOnlySpan<byte> s1)
    {
        byte val = s1[0];
        for (int i = 1; i < s1.Length; i++)
        {
            val++;
            if (val != s1[i])
                return false;
        }
        return true;
    }

    static void PrintErr(Exception ex) => PrintErr(ex.ToString());
    static void PrintErr(string str)
    {
        //Console.WriteLine($"[err] {str}");
        Log.WriteLine(LogPriority.Error, "IOTest", str);
    }
    static void PrintInf(string str)
    {
        Console.WriteLine($"[inf] {str}");
        Log.WriteLine(LogPriority.Info, "IOTest", str);
    }
}
