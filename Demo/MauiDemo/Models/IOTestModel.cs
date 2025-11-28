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



    public async Task StartTestAsync(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity,
        CancellationToken ct)
    {
        using var usbDriver = UsbDriverFactory.CreateUsbDriver(deviceId);
        var _stopBits = (UsbSerialForAndroid.Net.Enums.StopBits)stopBits;
        var _parity = (UsbSerialForAndroid.Net.Enums.Parity)parity;
        await usbDriver.OpenAsync(baudRate, dataBits, _stopBits, _parity);
        await Flush(usbDriver, ct);
        await Task.WhenAny(ExecReadAsync(usbDriver, ct), ExecWriteAsync(usbDriver, ct));
    }
    public async Task Flush(UsbDriverBase usbDriver, CancellationToken ct)
    {
        try
        {
            // flush all from ic
            byte[] buf = new byte[SampleBufLength];
            using var toCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            toCt.CancelAfter(500);
            int currReadLen = await usbDriver.ReadAsync(buf, 0, SampleBufLength, toCt.Token);
        }
        catch (Exception)
        {
            //ReadSpeed = $"{ex.Message}";
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
            ReadSpeed = $"{ex.Message}";
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
            WriteSpeed = $"{ex.Message}";
        }
    }
    public const int SampleBufLength = 256;
    public async Task WriteAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        byte[] writeBuf = new byte[SampleBufLength];
        // fill buf
        for (int i = 0; i < writeBuf.Length; i++)
            writeBuf[i] = (byte)i;
        TimeSpan UpdatePreiod = TimeSpan.FromMilliseconds(1000);
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
                WriteSpeed = $"{speed:N0} byte/sec";// {difBytes:N0}  {difTime}
                tickPrev = now;
                sentPrev = sentTotal;
            }
            if (SampleBufLength != await usbDriver.WriteAsync(writeBuf, 0, writeBuf.Length, ct))
                throw new Exception("Something write wrong");
            sentTotal += SampleBufLength;
            //await Task.Delay(1, ct);
        }
    }
    private async Task ReadAsync(UsbDriverBase usbDriver, CancellationToken ct)
    {
        byte[] buf = new byte[SampleBufLength];
        byte[] testDataSample = new byte[SampleBufLength];
        // fill buf
        for (int i = 0; i < testDataSample.Length; i++)
            testDataSample[i] = (byte)i;
        TimeSpan UpdatePreiod = TimeSpan.FromMilliseconds(1000);
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
                ReadSpeed = $"{speed:N0} byte/sec";// {difBytes:N0}  {difTime}
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
            if (!testDataSample.SequenceEqual(buf))
            {
                usbDriver.Logger.Error(BitConverter.ToString(buf));
                usbDriver.Logger.Error($"Read {readTotal} not equal write sequence");
                throw new Exception($"Read {readTotal} not equal write sequence");
            }
        }
    }

}
