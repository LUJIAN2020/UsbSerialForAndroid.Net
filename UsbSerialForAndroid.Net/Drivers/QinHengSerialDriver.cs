using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// QinHeng Electronics
    /// </summary>
    public class QinHengSerialDriver : UsbDriverBase
    {
        private readonly int[] baud = new int[] { 50, 0x1680, 0x0024, 75, 0x6480, 0x0018, 100, 0x8B80, 0x0012,
                110, 0x9580, 0x00B4,  150, 0xB280, 0x000C, 300, 0xD980, 0x0006, 600, 0x6481, 0x0018,
                900, 0x9881, 0x0010, 1200, 0xB281, 0x000C, 1800, 0xCC81, 0x0008, 2400, 0xD981, 0x0006,
                3600, 0x3082, 0x0020, 4800, 0x6482, 0x0018, 9600, 0xB282, 0x000C, 14400, 0xCC82, 0x0008,
                19200, 0xD982, 0x0006, 33600, 0x4D83, 0x00D3, 38400, 0x6483, 0x0018, 56000, 0x9583, 0x0018,
                57600, 0x9883, 0x0010, 76800, 0xB283, 0x000C, 115200, 0xCC83, 0x0008, 128000, 0xD183, 0x003B,
                153600, 0xD983, 0x0006, 230400, 0xE683, 0x0004, 460800, 0xF383, 0x0002, 921600, 0xF387, 0x0000,
                1500000, 0xFC83, 0x0003, 2000000, 0xFD83, 0x0002 };
        public const int SclDtr = 0x20;
        public const int SclRts = 0x40;

        public const int RequestTypeHostToDeviceIn = UsbConstants.UsbTypeVendor | (int)UsbAddressing.In;
        public const int RequestTypeHostToDeviceOut = UsbConstants.UsbTypeVendor | (int)UsbAddressing.Out;

        public QinHengSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        public override void Open(int baudRate = DefaultBaudRate, byte dataBits = DefaultDataBits, StopBits stopBits = DefaultStopBits, Parity parity = DefaultParity)
        {
            UsbDeviceConnection = UsbManager.OpenDevice(UsbDevice);
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            for (int i = 0; i < UsbDevice.InterfaceCount; i++)
            {
                UsbInterface usbIface = UsbDevice.GetInterface(i);
                bool isClaim = UsbDeviceConnection.ClaimInterface(usbIface, true);
                if (!isClaim)
                    throw new Exception($"Could not claim interface {i}");
            }

            UsbInterface = UsbDevice.GetInterface(UsbDevice.InterfaceCount - 1);
            for (int i = 0; i < UsbInterface.EndpointCount; i++)
            {
                var ep = UsbInterface.GetEndpoint(i);
                if (ep is not null)
                {
                    if (ep.Type == UsbAddressing.XferBulk)
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
            }

            Initialize();
            SetParameter(baudRate, dataBits, stopBits, parity);
        }
        private void Initialize()
        {
            int request = 0x5f;
            int value = 0;
            int index = 0;
            var data = new int[] { -1 /* 0x27, 0x30 */, 0x00 };
            CheckState("init #1", request, value, data);

            request = 0xa1;
            value = 0;
            index = 0;
            int ret = ControlOut(request, value, index);
            if (ret < 0)
                throw new ControlTransferException("init failed! #2", ret, RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);

            SetBaudRate(DefaultBaudRate);

            request = 0x95;
            value = 0x2518;
            data = new int[] { -1 /* 0x56, c3*/, 0x00 };
            CheckState("init #4", request, value, data);

            request = 0x9a;
            value = 0x2518;
            index = 0x0050;
            ret = ControlOut(0x9a, 0x2518, 0x0050);
            if (ret < 0)
                throw new ControlTransferException("init failed! #5", ret, RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);

            request = 0x95;
            value = 0x0706;
            data = new int[] { -1 /*0xf?*/, -1 /*0xec,0xee*/};
            CheckState("init #6", request, value, data);

            request = 0xa1;
            value = 0x501f;
            index = 0xd90a;
            ret = ControlOut(request, value, index);
            if (ret < 0)
                throw new ControlTransferException("init failed! #7", ret, RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);

            SetBaudRate(DefaultBaudRate);

            SetControlLines();

            request = 0x95;
            value = 0x0706;
            data = new int[] { -1 /* 0x9f, 0xff*/, -1/*0xec,0xee*/ };
            CheckState("init #10", request, value, data);
        }
        private void CheckState(string msg, int request, int value, int[] expected)
        {
            byte[] buffer = new byte[expected.Length];
            int ret = ControlIn(request, value, 0, buffer);
            if (ret < 0)
                throw new ControlTransferException($"Failed send cmd [{msg}]", ret, RequestTypeHostToDeviceIn, request, value, 0, buffer, buffer.Length, ControlTimeout);

            if (ret != expected.Length)
                throw new ControlTransferException($"Expected {expected.Length} bytes, but get {ret} [{msg}]", ret, RequestTypeHostToDeviceIn, request, value, 0, buffer, buffer.Length, ControlTimeout);

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == -1) continue;

                int current = buffer[i] & 0xff;
                if (expected[i] != current)
                    throw new Exception($"Expected 0x{expected[i]:X} bytes, but get 0x{current:X} [ {msg} ]");
            }
        }
        private int ControlIn(int request, int value, int index, byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            return UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDeviceIn, request, value, index, buffer, buffer.Length, ControlTimeout);
        }
        private int ControlOut(int request, int value, int index)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            return UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);
        }
        private void SetControlLines()
        {
            const int request = 0xA4;
            int value = ~((DtrEnable ? SclDtr : 0) | (RtsEnable ? SclRts : 0));
            const int index = 0;
            int ret = ControlOut(request, value, 0);
            if (ret < 0)
                throw new ControlTransferException("Failed to set control lines", ret, RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);
        }
        private void SetBaudRate(int baudRate)
        {
            for (int i = 0; i < baud.Length / 3; i++)
            {
                if (baud[i * 3] == baudRate)
                {
                    const int request = 0x9a;

                    const int value1 = 0x1312;
                    int index1 = baud[i * 3 + 1];
                    int ret = ControlOut(request, value1, index1);
                    if (ret < 0)
                        throw new ControlTransferException("Error setting baud rate. #1", ret, RequestTypeHostToDeviceOut, request, value1, index1, null, 0, ControlTimeout);

                    const int value2 = 0x0f2c;
                    int index2 = baud[i * 3 + 2];
                    ret = ControlOut(request, value2, index2);
                    if (ret < 0)
                        throw new ControlTransferException("Error setting baud rate. #2", ret, RequestTypeHostToDeviceOut, request, value2, index2, null, 0, ControlTimeout);

                    return;
                }
            }

            throw new Exception($"Baud rate {baudRate} currently not supported");
        }
        private void SetParameter(int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);
            const int LcrEnableRx = 0x80;
            const int LcrEnableTx = 0x40;
            int lcr = LcrEnableRx | LcrEnableTx;

            lcr |= dataBits switch
            {
                5 => 0,
                6 => 1,
                7 => 2,
                8 => 3,
                _ => throw new Exception($"Invalid data bits: {dataBits}"),
            };
            const int LcrMarkSpace = 0x20;
            const int LcrParEven = 0x10;
            const int LcrEnablePar = 0x08;

            lcr |= parity switch
            {
                Parity.None => lcr,
                Parity.Odd => LcrEnablePar,
                Parity.Even => LcrEnablePar | LcrParEven,
                Parity.Mark => LcrEnablePar | LcrMarkSpace,
                Parity.Space => LcrEnablePar | LcrMarkSpace | LcrParEven,
                _ => throw new Exception($"Invalid parity: {parity}"),
            };
            const int LcrStopBits2 = 0x04;
            lcr |= stopBits switch
            {
                StopBits.One => lcr,
                StopBits.OnePointFive => throw new Exception("Unsupported stop bits: 1.5"),
                StopBits.Two => LcrStopBits2,
                _ => throw new Exception($"Invalid stop bits: {stopBits}")
            };

            const int request = 0x9A;
            const int value = 0x2518;
            int ret = ControlOut(request, value, lcr);
            if (ret < 0)
                throw new ControlTransferException("Error setting control byte", ret, RequestTypeHostToDeviceOut, request, value, lcr, null, 0, ControlTimeout);
        }
        public override void SetDtrEnable(bool value)
        {
            DtrEnable = value;
            SetControlLines();
        }
        public override void SetRtsEnable(bool value)
        {
            RtsEnable = value;
            SetControlLines();
        }
    }
}