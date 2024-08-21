using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Helper;
using UsbSerialForAndroid.Net.Receivers;

namespace UsbSerialForAndroid.Net
{
    /// <summary>
    /// USB驱动工厂
    /// </summary>
    public static class UsbDriverFactory
    {
        /// <summary>
        /// 创建USB驱动
        /// </summary>
        /// <param name="usbDevice">USB设备</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedDriverException"></exception>
        private static UsbDriverBase CreateUsbDriver(UsbDevice usbDevice)
        {
            if (!HasSupportedDriver(usbDevice.VendorId, usbDevice.ProductId))
                throw new NotSupportedDriverException(usbDevice);

            return usbDevice.VendorId switch
            {
                (int)VendorIds.FTDI => new FtdiSerialDriver(usbDevice),
                (int)VendorIds.Prolific => new ProlificSerialDriver(usbDevice),
                (int)VendorIds.QinHeng => new QinHengSerialDriver(usbDevice),
                (int)VendorIds.SiliconLabs => new SiliconLabsSerialDriver(usbDevice),
                _ => throw new NotSupportedDriverException(usbDevice)
            };
        }
        /// <summary>
        /// 创建USB驱动
        /// </summary>
        /// <param name="vendorId">厂商Id</param>
        /// <param name="productId">产品Id</param>
        /// <returns></returns>
        public static UsbDriverBase CreateUsbDriver(int vendorId, int productId)
        {
            var device = UsbManagerHelper.GetUsbDevice(vendorId, productId);
            return CreateUsbDriver(device);
        }
        /// <summary>
        /// 创建USB驱动
        /// </summary>
        /// <param name="deviceId">设备Id</param>
        /// <returns></returns>
        public static UsbDriverBase CreateUsbDriver(int deviceId)
        {
            var device = UsbManagerHelper.GetUsbDevice(deviceId);
            return CreateUsbDriver(device);
        }
        /// <summary>
        /// 是否有支持驱动
        /// </summary>
        /// <param name="vendorId">厂商Id</param>
        /// <param name="productId">产品Id</param>
        /// <returns></returns>
        public static bool HasSupportedDriver(int vendorId, int productId)
        {
            var vid = (VendorIds)vendorId;
            switch (vid)
            {
                case VendorIds.FTDI:
                    {
                        var pid = (Ftdi)productId;
                        switch (pid)
                        {
                            case Ftdi.FT232R:
                            case Ftdi.FT2232H:
                            case Ftdi.FT4232H:
                            case Ftdi.FT232H:
                            case Ftdi.FT231X:
                                return true;
                        }
                        break;
                    }
                case VendorIds.Prolific:
                    {
                        var pid = (Prolific)productId;
                        switch (pid)
                        {
                            case Prolific.PL2303:
                            case Prolific.PL2303GC:
                            case Prolific.PL2303GB:
                            case Prolific.PL2303GT:
                            case Prolific.PL2303GL:
                            case Prolific.PL2303GE:
                            case Prolific.PL2303GS:
                                return true;
                        }
                        break;
                    }
                case VendorIds.QinHeng:
                    {
                        var pid = (QinHeng)productId;
                        switch (pid)
                        {
                            case QinHeng.HL340:
                            case QinHeng.CH341A:
                                return true;
                        }
                        break;
                    }
                case VendorIds.SiliconLabs:
                    {
                        var pid = (SiliconLabs)productId;
                        switch (pid)
                        {
                            case SiliconLabs.CP2102:
                            case SiliconLabs.CP2105:
                            case SiliconLabs.CP2108:
                            case SiliconLabs.CP2110:
                                return true;
                        }
                        break;
                    }
            }
            return false;
        }
        /// <summary>
        /// 注册USB广播接收器
        /// </summary>
        /// <param name="isShowToast">true=toast显示</param>
        /// <param name="attached">USB插入回调</param>
        /// <param name="detached">USB拔出回调</param>
        /// <param name="errorCallback">内部回调错误</param>
        public static void RegisterUsbBroadcastReceiver(bool isShowToast = true,
            Action<UsbDevice>? attached = default, Action<UsbDevice>? detached = default,
            Action<Exception>? errorCallback = default)
        {
            UsbBroadcastReceiverHelper.RegisterUsbBroadcastReceiver(isShowToast, attached, detached, errorCallback);
        }
        /// <summary>
        /// 取消USB广播接收器
        /// </summary>
        public static void UnRegisterUsbBroadcastReceiver()
        {
            UsbBroadcastReceiverHelper.UnRegisterUsbBroadcastReceiver();
        }
    }
}
