using Android.Hardware.Usb;
using System;

namespace UsbSerialForAndroid.Net.Exceptions
{
    public class NotSupportedDriverException : Exception
    {
        public NotSupportedDriverException(UsbDevice usbDevice)
            : base($"Driver not supported,VendorId={usbDevice.VendorId},ProductId={usbDevice.ProductId},DeviceId={usbDevice.DeviceId}")
        {

        }
    }
}
