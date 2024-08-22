using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// Silicon Labs
    /// </summary>
    public class SiliconLabsSerialDriver : UsbDriverBase
    {
        public const int RequestTypeHostToDevice = 0x41;

        public const int SilabserIcfEnableRquestCode = 0x00;
        public const int SilabserSetMhsRequestCode = 0x07;
        public const int SilabserSetBauddivRequestCode = 0x01;
        public const int SilabserSetLineCtlRequestCode = 0x03;
        public const int SilabserSetBaudRate = 0x1E;

        public const int UartEnable = 0x0001;
        public const int BaudRateGenFreq = 0x384000;
        public const int McrAll = 0x0003;

        public const int ControlDtrEnable = 0x0101;
        public const int ControlDtrDisable = 0x0100;
        public const int ControlRtsEnable = 0x0202;
        public const int ControlRtsDisable = 0x0200;
        public SiliconLabsSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        public override void Open(int baudRate = DefaultBaudRate, byte dataBits = DefaultDataBits, StopBits stopBits = DefaultStopBits, Parity parity = DefaultParity)
        {
            UsbDeviceConnection = UsbManager.OpenDevice(UsbDevice);
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            UsbInterface = UsbDevice.GetInterface(UsbInterfaceIndex);
            ArgumentNullException.ThrowIfNull(UsbInterface);

            bool isClaim = UsbDeviceConnection.ClaimInterface(UsbInterface, true);
            if (!isClaim)
                throw new Exception($"Could not claim interface {UsbInterfaceIndex}");

            for (int i = 0; i < UsbInterface.EndpointCount; i++)
            {
                var ep = UsbInterface.GetEndpoint(i);
                if (ep?.Type == UsbAddressing.XferBulk)
                {
                    if (ep.Direction == UsbAddressing.In)
                    {
                        UsbEndpointRead = ep;
                    }
                    else
                    {
                        UsbEndpointWrite = ep;
                    }
                }
            }

            SetUartEnable();
            SetConfigSingle(SilabserSetMhsRequestCode, McrAll | ControlDtrDisable | ControlRtsDisable);
            SetConfigSingle(SilabserSetBauddivRequestCode, BaudRateGenFreq / DefaultBaudRate);
            SetParameter(baudRate, dataBits, stopBits, parity);
        }
        private void SetUartEnable()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int ret = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SilabserIcfEnableRquestCode, UartEnable, 0, null, 0, ControlTimeout);
            if (ret != 0)
                throw new ControlTransferException("Set uart enable failed", ret, RequestTypeHostToDevice, SilabserIcfEnableRquestCode, UartEnable, 0, null, 0, ControlTimeout);
        }
        private void SetConfigSingle(int request, int value)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int ret = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, request, value, 0, null, 0, ControlTimeout);
            if (ret != 0)
                throw new ControlTransferException("Set config single error", ret, RequestTypeHostToDevice, request, value, 0, null, 0, ControlTimeout);
        }
        private void SetParameter(int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            int configDataBits = 0;
            switch (dataBits)
            {
                case 5:
                    configDataBits |= 0x0500;
                    break;
                case 6:
                    configDataBits |= 0x0600;
                    break;
                case 7:
                    configDataBits |= 0x0700;
                    break;
                case 8:
                    configDataBits |= 0x0800;
                    break;
                default:
                    configDataBits |= 0x0800;
                    break;
            }

            switch (parity)
            {
                case Parity.Odd:
                    configDataBits |= 0x0010;
                    break;
                case Parity.Even:
                    configDataBits |= 0x0020;
                    break;
            }

            switch (stopBits)
            {
                case StopBits.One:
                    configDataBits |= 0;
                    break;
                case StopBits.Two:
                    configDataBits |= 2;
                    break;
            }

            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int ret = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SilabserSetLineCtlRequestCode, configDataBits, 0, null, 0, ControlTimeout);
            if (ret < 0)
                throw new ControlTransferException("`DataBits` `Parity` `StopBits` set error", ret, RequestTypeHostToDevice, SilabserSetLineCtlRequestCode, configDataBits, 0, null, 0, ControlTimeout);
        }
        private void SetBaudRate(int baudRate)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            byte[] data = new byte[]
            {
                (byte) ( baudRate & 0xff),
                (byte) ((baudRate >> 8 ) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff)
            };
            int ret = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SilabserSetBaudRate, 0, 0, data, data.Length, ControlTimeout);
            if (ret < 0)
                throw new ControlTransferException("Set baud rate error", ret, RequestTypeHostToDevice, SilabserSetBaudRate, 0, 0, data, data.Length, ControlTimeout);
        }
        public override void SetDtrEnable(bool value)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            DtrEnable = value;
            int inValue = DtrEnable ? ControlDtrEnable : ControlDtrDisable;
            int index = 0;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SilabserSetMhsRequestCode, inValue, index, null, 0, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set Dtr failed", result, RequestTypeHostToDevice, SilabserSetMhsRequestCode, inValue, index, null, 0, ControlTimeout);
        }
        public override void SetRtsEnable(bool value)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            RtsEnable = value;
            int inValue = RtsEnable ? ControlRtsEnable : ControlRtsDisable;
            int index = 0;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SilabserSetMhsRequestCode, inValue, index, null, 0, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set Rts failed", result, RequestTypeHostToDevice, SilabserSetMhsRequestCode, inValue, index, null, 0, ControlTimeout);
        }
    }
}