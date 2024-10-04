using AvaloniaDemo.Models;
using System.Collections.Generic;

namespace AvaloniaDemo
{
    public interface IUsbService
    {
        List<UsbDeviceInfo> GetUsbDeviceInfos();
        void Open(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity);
        void Send(byte[] buffer);
        byte[]? Receive();
        void Close();
        bool IsConnection();
    }
}