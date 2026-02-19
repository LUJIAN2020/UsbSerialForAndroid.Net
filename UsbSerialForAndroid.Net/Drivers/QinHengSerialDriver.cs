using Android.Hardware.Usb;
using System;
using System.Threading.Tasks;
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

        // info from linux
        // https://github.com/torvalds/linux/tree/master/drivers/usb/serial
        // https://github.com/nospam2000/ch341-baudrate-calculation/tree/master
        public byte ChipVersion = 0;
        public const byte CH341_REQ_READ_VERSION = 0x5F;
        public const byte CH341_REQ_WRITE_REG = 0x9A;
        public const byte CH341_REQ_READ_REG = 0x95;
        public const byte CH341_REQ_SERIAL_INIT = 0xA1;
        public const byte CH341_REQ_MODEM_CTRL = 0xA4;

        public const byte CH341_REG_DIVISOR = 0x13;
        public const byte CH341_REG_PRESCALER = 0x12;

        public const byte CH341_REG_LCR2 = 0x25;
        public const byte CH341_REG_LCR = 0x18;

        public const int RequestTypeHostToDeviceIn = UsbConstants.UsbTypeVendor | (int)UsbAddressing.In;
        public const int RequestTypeHostToDeviceOut = UsbConstants.UsbTypeVendor | (int)UsbAddressing.Out;
        public QinHengSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        /// <summary>
        /// Open the USB device
        /// </summary>
        /// <param name="baudRate"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        /// <exception cref="Exception"></exception>
        public override async ValueTask OpenAsync(int baudRate = DefaultBaudRate, byte dataBits = DefaultDataBits, StopBits stopBits = DefaultStopBits, Parity parity = DefaultParity)
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
            ArgumentNullException.ThrowIfNull(UsbEndpointWrite);
            ArgumentNullException.ThrowIfNull(UsbEndpointRead);
            UsbWriteBufLength = UsbEndpointWrite.MaxPacketSize;
            UsbReadBufLength = UsbEndpointRead.MaxPacketSize;
            UsbRequestCount = 128;
            await InitBuffersAsync();
        }
        /// <summary>
        /// Initialize the device
        /// </summary>
        /// <exception cref="ControlTransferException"></exception>
        private void Initialize()
        {
            byte[] data = [0xFF /* 0x27, 0x30 */, 0x00];
            CheckState("init #1", CH341_REQ_READ_VERSION, 0, data);
            ChipVersion = data[0];
            ControlOut("init #2", CH341_REQ_SERIAL_INIT, 0, 0);
            SetBaudRate(DefaultBaudRate);
            CheckState("init #4", CH341_REQ_READ_REG, 0x2518, [0xFF /* 0x56, c3*/, 0x00]);
            ControlOut("init #5", CH341_REQ_WRITE_REG, 0x2518, 0x0050);
            CheckState("init #6", CH341_REQ_READ_REG, 0x0706, [0xFF /*0xf?*/, 0xFF /*0xec,0xee*/]);
            ControlOut("init #7", CH341_REQ_SERIAL_INIT, 0x501f, 0xd90a);
            SetBaudRate(DefaultBaudRate);
            SetControlLines();
            CheckState("init #10", CH341_REQ_READ_REG, 0x0706, [0xFF /* 0x9f, 0xff*/, 0xFF/*0xec,0xee*/]);
        }
        /// <summary>
        /// Check the state of the device
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <param name="expected"></param>
        /// <exception cref="ControlTransferException"></exception>
        /// <exception cref="Exception"></exception>
        private void CheckState(string msg, int request, int value, byte[] expected)
        {
            byte[] buffer = new byte[expected.Length];
            int ret = ControlIn(request, value, 0, buffer);
            if (ret < 0)
                throw new ControlTransferException($"Failed send cmd [{msg}]", ret, RequestTypeHostToDeviceIn, request, value, 0, buffer, buffer.Length, ControlTimeout);
            if (ret != expected.Length)
                throw new ControlTransferException($"Expected {expected.Length} bytes, but get {ret} [{msg}]", ret, RequestTypeHostToDeviceIn, request, value, 0, buffer, buffer.Length, ControlTimeout);

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != 0xFF && expected[i] != buffer[i])
                    throw new Exception($"Expected 0x{expected[i]:X} bytes, but get 0x{buffer[i]:X} [ {msg} ]");
                expected[i] = buffer[i];
            }
        }
        /// <summary>
        /// Control transfer in
        /// </summary>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private int ControlIn(int request, int value, int index, byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            return UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDeviceIn, request, value, index, buffer, buffer.Length, ControlTimeout);
        }
        /// <summary>
        /// Control transfer out
        /// </summary>
        /// <param name="request"></param>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private void ControlOut(string msg, int request, int value, int index)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int ret = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);
            if (0 > ret)
                throw new ControlTransferException(msg, ret, RequestTypeHostToDeviceOut, request, value, index, null, 0, ControlTimeout);
        }
        /// <summary>
        /// Set the control lines
        /// </summary>
        /// <exception cref="ControlTransferException"></exception>
        private void SetControlLines()
        {
            int value = ~((DtrEnable ? SclDtr : 0) | (RtsEnable ? SclRts : 0));
            ControlOut("Failed to set control lines", CH341_REQ_MODEM_CTRL, value, 0);
        }
        /// <summary>
        /// Set the baud rate
        /// </summary>
        /// <param name="baudRate"></param>
        /// <exception cref="ControlTransferException"></exception>
        /// <exception cref="Exception"></exception>
        private void SetBaudRate(int baudRate)
        {
            for (int i = 0; i < baud.Length / 3; i++)
            {
                if (baud[i * 3] == baudRate)
                {
                    const int value1 = (CH341_REG_DIVISOR << 8) | CH341_REG_PRESCALER;
                    int index1 = baud[(i * 3) + 1];
                    if (ChipVersion > 0x27)
                        index1 |= 0x80; //  BIT(7)
                    ControlOut("Error setting baud rate. #1", CH341_REQ_WRITE_REG, value1, index1);
                    const int value2 = 0x0f2c;
                    int index2 = baud[(i * 3) + 2];
                    ControlOut("Error setting baud rate. #2", CH341_REQ_WRITE_REG, value2, index2);
                    return;
                }
            }
            throw new Exception($"Baud rate {baudRate} currently not supported");
        }
        /// <summary>
        /// Set the parameters
        /// </summary>
        /// <param name="baudRate"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ControlTransferException"></exception>
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

            const int value = (CH341_REG_LCR2 << 8) | CH341_REG_LCR;
            ControlOut("Error setting control byte", CH341_REQ_WRITE_REG, value, lcr);
        }
        /// <summary>
        /// Set DTR enabled
        /// </summary>
        /// <param name="value"></param>
        public override void SetDtrEnabled(bool value)
        {
            DtrEnable = value;
            SetControlLines();
        }
        /// <summary>
        /// Set RTS enabled
        /// </summary>
        /// <param name="value"></param>
        public override void SetRtsEnabled(bool value)
        {
            RtsEnable = value;
            SetControlLines();
        }
    }
}