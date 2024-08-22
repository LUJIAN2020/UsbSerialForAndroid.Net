using MauiDemo.Models;
using MauiDemo.Services;
using UsbSerialForAndroid.Net;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Helper;

namespace MauiDemo.Platforms.Android
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
            return usbDriver.Read();
        }
        public void Send(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(usbDriver);
            usbDriver.Write(buffer);
        }
        public void Close()
        {
            ArgumentNullException.ThrowIfNull(usbDriver);
            usbDriver.Close();
        }
    }
}
