using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Widget;
using System;
using UsbSerialForAndroid.Net.Resources.Langs;

namespace UsbSerialForAndroid.Net.Receivers
{
    /// <summary>
    /// USB broadcast receiver
    /// </summary>
    internal class UsbBroadcastReceiver : BroadcastReceiver
    {
        /// <summary>
        /// USB device attached callback
        /// </summary>
        public Action<UsbDevice>? UsbDeviceAttached;
        /// <summary>
        /// USB device detached callback
        /// </summary>
        public Action<UsbDevice>? UsbDeviceDetached;
        /// <summary>
        /// Internal error callback
        /// </summary>
        public Action<Exception>? ErrorCallback;
        /// <summary>
        /// Show toast message
        /// </summary>
        public bool IsShowToast { get; set; } = true;
        /// <summary>
        /// USB broadcast receiver
        /// </summary>
        /// <param name="context"></param>
        /// <param name="intent"></param>
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
                                    msg = AppResources.UsbDeviceAttached + msg;
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
                                    msg = AppResources.UsbDeviceDetached + msg;
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
