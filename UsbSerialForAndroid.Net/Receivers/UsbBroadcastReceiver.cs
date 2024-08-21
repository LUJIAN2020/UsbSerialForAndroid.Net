using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Widget;
using System;

namespace UsbSerialForAndroid.Net.Receivers
{
    internal class UsbBroadcastReceiver : BroadcastReceiver
    {
        public Action<UsbDevice>? UsbDeviceAttached;
        public Action<UsbDevice>? UsbDeviceDetached;
        public Action<Exception>? ErrorCallback;
        public bool IsShowToast { get; set; } = true;
        public override void OnReceive(Context? context, Intent? intent)
        {
            try
            {
                var usbService = context?.GetSystemService(Context.UsbService);
                if (usbService is UsbManager usbManager && intent is not null && intent.Extras is not null)
                {
                    if (intent.Extras.Get(UsbManager.ExtraDevice) is UsbDevice usbDevice)
                    {
                        string msg = $"PID={usbDevice.ProductId} VID={usbDevice.VendorId}";
                        switch (intent.Action)
                        {
                            case UsbManager.ActionUsbDeviceAttached:
                                {
                                    msg = "设备连接 " + msg;
                                    if (usbManager?.HasPermission(usbDevice) == false)
                                    {
                                        var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable);
                                        usbManager.RequestPermission(usbDevice, pendingIntent);
                                    }
                                    UsbDeviceAttached?.Invoke(usbDevice);
                                    break;
                                }
                            case UsbManager.ActionUsbDeviceDetached:
                                {
                                    msg = "设备断开 " + msg;
                                    UsbDeviceDetached?.Invoke(usbDevice);
                                    break;
                                }
                            default:
                                break;
                        }
                        if (IsShowToast)
                            Toast.MakeText(context, msg, ToastLength.Short)?.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsShowToast)
                    Toast.MakeText(context, ex.Message, ToastLength.Long)?.Show();
                ErrorCallback?.Invoke(ex);
            }
        }
    }
}
