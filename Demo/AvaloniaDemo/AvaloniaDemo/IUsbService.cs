using System.Collections.Generic;
using AvaloniaDemo.Models;

namespace AvaloniaDemo
{
    public interface IUsbService
    {
        List<UsbDeviceInfo> GetUsbDeviceInfos();
        void Open(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity);
        void Send(byte[] buffer);
        byte[] Receive();
        void Close();
    }
}