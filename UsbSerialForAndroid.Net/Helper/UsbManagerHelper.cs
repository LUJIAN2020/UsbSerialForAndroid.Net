using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using System;
using System.Collections.Generic;
using System.Linq;
using UsbSerialForAndroid.Net.Drivers;

namespace UsbSerialForAndroid.Net.Helper
{
    public static class UsbManagerHelper
    {
        private static readonly UsbManager usbManager = UsbDriverBase.UsbManager;
        public static UsbDevice GetUsbDevice(int deviceId)
        {
            return usbManager.DeviceList?
                .Select(c => c.Value)
                .FirstOrDefault(c => c.DeviceId == deviceId)
                ?? throw new Exception($"No usb device with id found `{deviceId}`"); ;
        }
        public static UsbDevice GetUsbDevice(int vendorId, int productId)
        {
            return usbManager.DeviceList?
                .Select(c => c.Value)
                .FirstOrDefault(c => c.VendorId == vendorId && c.VendorId == productId)
                ?? throw new Exception($"The corresponding device could not be found VendorId={vendorId} ProductId={productId}");
        }
        public static bool HasPermission(UsbDevice usbDevice)
        {
            return usbManager.HasPermission(usbDevice);
        }
        public static IEnumerable<UsbDevice> GetAllUsbDevices()
        {
            return usbManager.DeviceList?.Select(c => c.Value) ?? Array.Empty<UsbDevice>();
        }
        public static IEnumerable<UsbDevice> GetPermissionedUsbDevices()
        {
            return GetAllUsbDevices().Where(HasPermission);
        }
        public static void RequestPermission(UsbDevice usbDevice)
        {
            var intent = new Intent(UsbManager.ActionUsbDeviceAttached);
            var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.Immutable);
            usbManager.RequestPermission(usbDevice, pendingIntent);
        }
    }
}
