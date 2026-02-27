using Android.Util;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;

namespace MauiDemo.Models;

public partial class IOTestModel : ObservableObject
{
    //private UsbDriverBase? _usbDriver;
    [ObservableProperty] public partial string? WriteSpeed { get; set; }
    [ObservableProperty] public partial string? ReadSpeed { get; set; }
    public IOTestModel() { }

    public static TimeSpan UpdatePreiod = TimeSpan.FromMilliseconds(1000);

    public async Task StartTestAsync(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity,
        CancellationToken ct)
    {
        using var usbDriver = UsbDriverFactory.CreateUsbDriver(deviceId);
        var _stopBits = (UsbSerialForAndroid.Net.Enums.StopBits)stopBits;
        var _parity = (UsbSerialForAndroid.Net.Enums.Parity)parity;
        await usbDriver.OpenAsync(baudRate, dataBits, _stopBits, _parity);
        await Task.Delay(100, ct);
        await Task.WhenAny(ExecReadAsync(usbDriver, ct), ExecWriteAsync(usbDriver, ct));
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
    public const int SampleBufLength = 256;
    public async Task WriteAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        //while (!ct.IsCancellationRequested) 
        //    await Task.Delay(10000, ct);
        byte[] writeBuf = new byte[SampleBufLength];
        // fill buf
        for (int i = 0; i < writeBuf.Length; i++)
            writeBuf[i] = (byte)i;

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
            if (SampleBufLength != await usbDriver.WriteAsync(writeBuf, 0, writeBuf.Length, ct))
                throw new Exception("Something write wrong");
            sentTotal += SampleBufLength;
        }
    }
    private async Task ReadAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        byte[] buf = new byte[SampleBufLength];
        byte[] testDataSample = new byte[SampleBufLength];
        // fill buf
        for (int i = 0; i < testDataSample.Length; i++)
            testDataSample[i] = (byte)i;
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
            if (!IsSeq256(buf))
            {
                PrintInf(BitConverter.ToString(buf));
                PrintErr($"Read {readTotal} not equal write sequence");
                throw new Exception($"Read {readTotal} not equal write sequence");
            }
        }
    }
    bool IsSeq256(ReadOnlySpan<byte> s1)
    {
        byte prev = s1[0];
        for (int i = 1; i < 255; i++)
        {
            if (1 != s1[i] - prev)
                return false;
            prev = s1[i];
        }
        return true;
    }

    static void PrintErr(Exception ex) => PrintErr(ex.ToString());
    static void PrintErr(string str)
    {
        Console.WriteLine($"[err] {str}");
        Log.WriteLine(LogPriority.Error, "IOTest", str);
    }
    static void PrintInf(string str)
    {
        Console.WriteLine($"[inf] {str}");
        Log.WriteLine(LogPriority.Info, "IOTest", str);
    }
}
