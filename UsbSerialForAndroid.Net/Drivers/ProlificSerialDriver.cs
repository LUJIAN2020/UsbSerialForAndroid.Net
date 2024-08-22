using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// Prolific Technology, Inc.
    /// </summary>
    public sealed class ProlificSerialDriver : UsbDriverBase
    {
        private enum DeviceType
        {
            DeviceType01, DeviceTypeT, DeviceTypeHX, DeviceTypeHXN
        }

        private DeviceType deviceType = DeviceType.DeviceTypeHX;

        public const int VendorInRequestType = 0xC0;
        public const int VendorOutRequestType = 0x40;

        public const int VendorReadRequest = 0x01;
        public const int VendorWriteRequest = 0x01;

        public const int SetControlRequest = 0x22;

        public const int ControlDtr = 0x01;
        public const int ControlRts = 0x02;

        private int controlLinesValue = 0;

        public const int UsbRecipInterface = 0x01;
        public const int CtrlOutReqtype = (int)UsbAddressing.Out | UsbConstants.UsbTypeClass | UsbRecipInterface;

        public const int VendorReadHxnRequest = 0x80;
        public const int VendorWriteHxnRequest = 0x80;

        public const int ResetHxnRequest = 0x07;
        public const int FlushRxRequest = 0x08;
        public const int FlushTxRequest = 0x09;

        public const int RestHxnRxPipe = 1;
        public const int RestHxnTxPipe = 2;

        public UsbEndpoint? UsbEndpointInterupt { get; private set; }
        public new bool DtrEnable => (controlLinesValue & ControlDtr) != 0;
        public new bool RtsEnable => (controlLinesValue & ControlRts) != 0;
        public ProlificSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        public override void Open(int baudRate = DefaultBaudRate, byte dataBits = DefaultDataBits, StopBits stopBits = DefaultStopBits, Parity parity = DefaultParity)
        {
            UsbDeviceConnection = UsbManager.OpenDevice(UsbDevice);
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            UsbInterface = UsbDevice.GetInterface(UsbInterfaceIndex);
            bool isClaim = UsbDeviceConnection.ClaimInterface(UsbInterface, true);
            if (!isClaim)
                throw new Exception($"Could not claim interface {UsbInterfaceIndex}");

            for (int i = 0; i < UsbInterface.EndpointCount; ++i)
            {
                var ep = UsbInterface.GetEndpoint(i);
                const int ReadEndpoint = 0x83;
                const int WriteEndpoint = 0x02;
                const int InteruptEndpoint = 0x81;
                if (ep is not null)
                {
                    switch ((int)ep.Address)
                    {
                        case ReadEndpoint:
                            UsbEndpointRead = ep;
                            break;
                        case WriteEndpoint:
                            UsbEndpointWrite = ep;
                            break;
                        case InteruptEndpoint:
                            UsbEndpointInterupt = ep;
                            break;
                        default:
                            break;
                    }
                }
            }

            var rawDescriptors = UsbDeviceConnection.GetRawDescriptors();
            if (rawDescriptors is null || rawDescriptors.Length < 14)
                throw new Exception("Could not get device descriptors");

            int usbVersion = (rawDescriptors[3] << 8) + rawDescriptors[2];
            int deviceVersion = (rawDescriptors[13] << 8) + rawDescriptors[12];
            byte maxPacketSize = rawDescriptors[7];
            if (UsbDevice.DeviceClass == UsbClass.Comm || maxPacketSize != 64)
            {
                deviceType = DeviceType.DeviceType01;
            }
            else if (usbVersion == 0x200)
            {
                if (deviceVersion == 0x300 && TestHxStatus())
                {
                    deviceType = DeviceType.DeviceTypeT; // TA
                }
                else if (deviceVersion == 0x500 && TestHxStatus())
                {
                    deviceType = DeviceType.DeviceTypeT; // TB
                }
                else
                {
                    deviceType = DeviceType.DeviceTypeHXN;
                }
            }
            else
            {
                deviceType = DeviceType.DeviceTypeHX;
            }

            ResetDevice();
            DoBlackMagic();
            SetControlLines(controlLinesValue);
            SetFlowControl(FlowControl);

            SetParameter(baudRate, dataBits, stopBits, parity);
        }
        private void SetParameter(int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            var para = new byte[7];
            para[0] = (byte)(baudRate & 0xFF);
            para[1] = (byte)(baudRate >> 8 & 0xFF);
            para[2] = (byte)(baudRate >> 16 & 0xFF);
            para[3] = (byte)(baudRate >> 24 & 0xFF);
            switch (stopBits)
            {
                case StopBits.None:
                case StopBits.One:
                    para[4] = 0;
                    break;
                case StopBits.Two:
                    para[4] = 2;
                    break;
                case StopBits.OnePointFive:
                    para[4] = 1;
                    break;
            }
            para[5] = (byte)parity;
            para[6] = dataBits;
            const int SetLineRequest = 0x20;
            OutControlTransfer(CtrlOutReqtype, SetLineRequest, 0, 0, para);
            ResetDevice();
        }
        private bool TestHxStatus()
        {
            try
            {
                InControlTransfer(VendorInRequestType, VendorReadRequest, 0x8080, 0, 1);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void ResetDevice()
        {
            PurgeHwBuffers(true, true);
        }
        private void PurgeHwBuffers(bool purgeWriteBuffers, bool purgeReadBuffers)
        {
            if (deviceType == DeviceType.DeviceTypeHXN)
            {
                int index = 0;
                if (purgeWriteBuffers) index |= RestHxnRxPipe;
                if (purgeReadBuffers) index |= RestHxnTxPipe;
                if (index != 0)
                    VendorOut(ResetHxnRequest, index, null);
            }
            else
            {
                if (purgeWriteBuffers)
                    VendorOut(FlushRxRequest, 0, null);
                if (purgeReadBuffers)
                    VendorOut(FlushTxRequest, 0, null);
            }
        }
        private void DoBlackMagic()
        {
            if (deviceType == DeviceType.DeviceTypeHXN) return;

            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 0, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 1, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorOut(0, 1, null);
            VendorOut(1, 0, null);
            VendorOut(2, (deviceType == DeviceType.DeviceType01) ? 0x24 : 0x44, null);
        }
        public void SetFlowControl(FlowControl flowControl)
        {
            switch (flowControl)
            {
                case FlowControl.NONE:
                    if (deviceType == DeviceType.DeviceTypeHXN)
                        VendorOut(0x0a, 0xff, null);
                    else
                        VendorOut(0, 0, null);
                    break;
                case FlowControl.RTS_CTS:
                    if (deviceType == DeviceType.DeviceTypeHXN)
                        VendorOut(0x0a, 0xfa, null);
                    else
                        VendorOut(0, 0x61, null);
                    break;
                case FlowControl.XON_XOFF_INLINE:
                    if (deviceType == DeviceType.DeviceTypeHXN)
                        VendorOut(0x0a, 0xee, null);
                    else
                        VendorOut(0, 0xc1, null);
                    break;
                default:
                    break;
            }
            FlowControl = flowControl;
        }
        private void SetControlLines(int value)
        {
            OutControlTransfer(CtrlOutReqtype, SetControlRequest, value, 0, null);
            controlLinesValue = value;
        }
        private byte[] VendorIn(int value, int index, int length)
        {
            int request = (deviceType == DeviceType.DeviceTypeHXN) ? VendorReadHxnRequest : VendorReadRequest;
            return InControlTransfer(VendorInRequestType, request, value, index, length);
        }
        private void VendorOut(int value, int index, byte[]? data)
        {
            int request = (deviceType == DeviceType.DeviceTypeHXN) ? VendorWriteHxnRequest : VendorWriteRequest;
            OutControlTransfer(VendorOutRequestType, request, value, index, data);
        }
        private byte[] InControlTransfer(int requestType, int request, int value, int index, int length)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            byte[] data = new byte[length];
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)requestType, request, value, index, data, length, ControlTimeout);
            if (result != length)
                throw new ControlTransferException("InControlTransfer failed", result, requestType, request, value, index, data, length, ControlTimeout);

            return data;
        }
        private void OutControlTransfer(int requestType, int request, int value, int index, byte[]? data)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int length = (data == null) ? 0 : data.Length;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)requestType, request, value, index, data, length, ControlTimeout);
            if (result != length)
                throw new ControlTransferException($"OutControlTransfer failed", result, requestType, request, value, index, data, length, ControlTimeout);
        }
        public override void SetRtsEnable(bool value)
        {
            int newControlLinesValue = value
                ? (controlLinesValue | ControlRts)
                : (controlLinesValue & ~ControlRts);
            SetControlLines(newControlLinesValue);
        }
        public override void SetDtrEnable(bool value)
        {
            int newControlLinesValue = value
                ? (controlLinesValue | ControlDtr)
                : (controlLinesValue & ~ControlDtr);
            SetControlLines(newControlLinesValue);
        }
    }
}