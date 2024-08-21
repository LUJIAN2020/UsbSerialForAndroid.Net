using AvaloniaDemo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Helper;

namespace AvaloniaDemo.Android
{
    public class UsbService : IUsbService
    {
        public UsbService()
        {
            UsbDriverFactory.RegisterUsbBroadcastReceiver();
        }
        private UsbDriverBase? usbDriver;
        public List<UsbDeviceInfo> GetUsbDeviceInfos()
        {
            var items = UsbManagerHelper.GetAllUsbDevices();
            foreach (var item in items)
            {
                if (!UsbManagerHelper.HasPermission(item))
                {
                    UsbManagerHelper.RequestPermission(item);
                }
            }
            return items.Select(item => new UsbDeviceInfo()
            {
                DeviceId = item.DeviceId,
                DeviceName = item.DeviceName,
                ProductName = item.ProductName,
                ManufacturerName = item.ManufacturerName,
                VendorId = item.VendorId,
                ProductId = item.ProductId,
                SerialNumber = item.SerialNumber,
                DeviceProtocol = item.DeviceProtocol,
                ConfigurationCount = item.ConfigurationCount,
                InterfaceCount = item.InterfaceCount,
                Version = item.Version
            }).ToList();
        }
        public void Open(int deviceId, int baudRate, byte dataBits, byte stopBits, byte parity)
        {
            usbDriver = UsbDriverFactory.CreateUsbDriver(deviceId);
            var _stopBits = (UsbSerialForAndroid.Net.Enums.StopBits)stopBits;
            var _parity = (UsbSerialForAndroid.Net.Enums.Parity)parity;
            usbDriver.Open(baudRate, dataBits, _stopBits, _parity);
        }
        public byte[] Receive()
        {
            ArgumentNullException.ThrowIfNull(usbDriver);
            return usbDriver.Read() ?? throw new Exception("没有可读数据");
        }
        public int Send(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(usbDriver);
            return usbDriver.Write(buffer);
        }
        public void Close()
        {
            ArgumentNullException.ThrowIfNull(usbDriver);
            usbDriver.Close();
        }
    }
}