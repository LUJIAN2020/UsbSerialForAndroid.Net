using Android.Hardware.Usb;
using System;
using System.Buffers;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// Future Technology Devices International, Ltd
    /// </summary>
    public class FtdiSerialDriver : UsbDriverBase
    {
        private bool baudRateWithPort = false;
        public const int RequestTypeHostToDevice = UsbConstants.UsbTypeVendor | (int)UsbAddressing.Out;

        public const int ModemControlDtrEnable = 0x0101;
        public const int ModemControlDtrDisable = 0x0100;
        public const int ModemControlRtsEnable = 0x0202;
        public const int ModemControlRtsDisable = 0x0200;

        public const int ResetAll = 0;

        public const int ResetRequest = 0;
        public const int ModemControlRequest = 1;
        public const int SetFlowControlRequest = 2;
        public const int SetBaudRateRequest = 3;
        public const int SetDataRequest = 4;
        public const int GetModemStatusRequest = 5; // GET_MODEM_STATUS_REQUEST
        public const int SetLatencyTimerRequest = 9; // SET_LATENCY_TIMER_REQUEST
        public const int GetLatencyTimerRequest = 10; // GET_LATENCY_TIMER_REQUEST

        public FtdiSerialDriver(UsbDevice usbDevice)
            : base(usbDevice)
        {
            ReadHeaderLength = 2;
        }
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

            UsbInterface = UsbDevice.GetInterface(UsbInterfaceIndex);
            bool isClaim = UsbDeviceConnection.ClaimInterface(UsbInterface, true);
            if (!isClaim)
                throw new Exception($"Could not claim interface {UsbInterfaceIndex}");

            if (UsbInterface.EndpointCount < 2)
                throw new Exception("Not enough endpoints");

            UsbEndpointRead = UsbInterface.GetEndpoint(0);
            UsbEndpointWrite = UsbInterface.GetEndpoint(1);

            Reset();
            InitRtsDtr();
            SetFlowControl(FlowControl);

            var rawDescriptors = UsbDeviceConnection.GetRawDescriptors();
            if (rawDescriptors == null || rawDescriptors.Length < 14)
                throw new Exception("Could not get device descriptors");

            int deviceType = rawDescriptors[13];
            baudRateWithPort = deviceType == 7
                || deviceType == 8
                || deviceType == 9// ...H devices                                                        
                || UsbDevice.InterfaceCount > 1;// FT2232C

            SetParameter(baudRate, dataBits, stopBits, parity);
            SetLatency(1);
            FilterData = FilterBuf;
            await InitBuffersAsync();
        }
        /// <summary>
        /// Reset the USB device
        /// </summary>
        /// <exception cref="ControlTransferException"></exception>
        private void Reset()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int index = UsbInterfaceIndex + 1;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, ResetRequest, ResetAll, index, null, 0, ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Reset failed", result, RequestTypeHostToDevice, ResetRequest, ResetAll, index, null, 0, ControlTimeout);
        }
        /// <summary>
        /// Initialize RTS and DTR
        /// </summary>
        /// <exception cref="ControlTransferException"></exception>
        private void InitRtsDtr()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int value = (DtrEnable ? ModemControlDtrEnable : ModemControlDtrDisable) | (RtsEnable ? ModemControlRtsEnable : ModemControlRtsDisable);
            int index = UsbInterfaceIndex + 1;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Init RTS,DTR failed", result, RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);
        }
        /// <summary>
        /// Set the flow control
        /// </summary>
        /// <param name="flowControl"></param>
        /// <exception cref="ControlTransferException"></exception>
        public void SetFlowControl(FlowControl flowControl)
        {
            int value = 0;
            int index = UsbInterfaceIndex + 1;
            switch (flowControl)
            {
                case FlowControl.NONE:
                    break;
                case FlowControl.RTS_CTS:
                    index |= 0x100;
                    break;
                case FlowControl.DTR_DSR:
                    index |= 0x200;
                    break;
                case FlowControl.XON_XOFF:
                    break;
                case FlowControl.XON_XOFF_INLINE:
                    value = XON + (XOFF << 8);
                    index |= 0x400;
                    break;
            }

            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetFlowControlRequest, value, index, null, 0, ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Set flow control failed", result, RequestTypeHostToDevice, SetFlowControlRequest, value, index, null, 0, ControlTimeout);

            FlowControl = flowControl;
        }
        /// <summary>
        /// Set the baud rate
        /// </summary>
        /// <param name="baudRate"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ControlTransferException"></exception>
        private void SetBaudrate(int baudRate)
        {
            int divisor, subdivisor, effectiveBaudRate;
            if (baudRate > 3500000)
                throw new Exception("Baud rate to high");

            if (baudRate >= 2500000)
            {
                divisor = 0;
                subdivisor = 0;
                effectiveBaudRate = 3000000;
            }
            else if (baudRate >= 1750000)
            {
                divisor = 1;
                subdivisor = 0;
                effectiveBaudRate = 2000000;
            }
            else
            {
                divisor = (24000000 << 1) / baudRate;
                divisor = (divisor + 1) >> 1; // round
                subdivisor = divisor & 0x07;
                divisor >>= 3;
                if (divisor > 0x3fff) // exceeds bit 13 at 183 baud
                    throw new Exception("Baud rate to low");
                effectiveBaudRate = (24000000 << 1) / ((divisor << 3) + subdivisor);
                effectiveBaudRate = (effectiveBaudRate + 1) >> 1;
            }
            double baudRateError = System.Math.Abs(1.0 - (effectiveBaudRate / (double)baudRate));
            if (baudRateError >= 0.031) // can happen only > 1.5Mbaud
                throw new Exception($"Baud rate deviation {baudRateError} is higher than allowed 0.03");

            int value = divisor;
            int index = 0;
            switch (subdivisor)
            {
                case 0: break; // 16,15,14 = 000 - sub-integer divisor = 0
                case 4: value |= 0x4000; break; // 16,15,14 = 001 - sub-integer divisor = 0.5
                case 2: value |= 0x8000; break; // 16,15,14 = 010 - sub-integer divisor = 0.25
                case 1: value |= 0xc000; break; // 16,15,14 = 011 - sub-integer divisor = 0.125
                case 3: value |= 0x0000; index |= 1; break; // 16,15,14 = 100 - sub-integer divisor = 0.375
                case 5: value |= 0x4000; index |= 1; break; // 16,15,14 = 101 - sub-integer divisor = 0.625
                case 6: value |= 0x8000; index |= 1; break; // 16,15,14 = 110 - sub-integer divisor = 0.75
                case 7: value |= 0xc000; index |= 1; break; // 16,15,14 = 111 - sub-integer divisor = 0.875
            }
            if (baudRateWithPort)
            {
                index <<= 8;
                index |= UsbInterfaceIndex + 1;
            }

            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetBaudRateRequest, value, index, null, 0, ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Setting baudrate failed", result, RequestTypeHostToDevice, SetBaudRateRequest, value, index, null, 0, ControlTimeout);
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
            SetBaudrate(baudRate);

            int config = 0;
            switch (dataBits)
            {
                case 5:
                case 6:
                    throw new Exception($"Unsupported data bits: {dataBits}");
                case 7:
                case 8:
                    config |= dataBits;
                    break;
                default:
                    throw new Exception($"Invalid data bits: {dataBits}");
            }

            switch (parity)
            {
                case Parity.None:
                    break;
                case Parity.Odd:
                    config |= 0x100;
                    break;
                case Parity.Even:
                    config |= 0x200;
                    break;
                case Parity.Mark:
                    config |= 0x300;
                    break;
                case Parity.Space:
                    config |= 0x400;
                    break;
            }

            switch (stopBits)
            {
                case StopBits.One:
                    break;
                case StopBits.OnePointFive:
                    throw new Exception("Unsupported stop bits: 1.5");
                case StopBits.Two:
                    config |= 0x1000;
                    break;
                default:
                    throw new Exception($"Invalid stop bits: {stopBits}");
            }

            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int index = UsbInterfaceIndex + 1;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetDataRequest, config, index, null, 0, ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Setting parameters failed", result, RequestTypeHostToDevice, SetDataRequest, config, index, null, 0, ControlTimeout);
        }
        /// <summary>
        /// Set the Latency - miliseconds
        /// https://ftdichip.com/Support/Knowledgebase/index.html?an232beffectbuffsizeandlatency.htm
        /// </summary>
        /// <param name="latency"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ControlTransferException"></exception>
        public void SetLatency(byte latency)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int config = latency;
            int index = UsbInterfaceIndex + 1;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetLatencyTimerRequest, config, index,
                buffer: null, length: 0, timeout: ControlTimeout);
            if (result < 0)
                throw new ControlTransferException("Set Latency Timer failed", result, RequestTypeHostToDevice, SetLatencyTimerRequest, config, index, null, 0, ControlTimeout);
        }
        /// https://ftdichip.com/Support/Knowledgebase/index.html?ft_w32_getcommmodemstatus.htm
        /// https://microsin.net/programming/pc/ftdi-d2xx-functions-api.html?ysclid=mhfr8ts7fi434528274
        /// <summary>
        /// Read the data
        /// </summary>
        /// <returns></returns>
        public override byte[]? Read()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int len = UsbDeviceConnection.BulkTransfer(UsbEndpointRead, buffer, 0, DefaultBufferLength, ReadTimeout);
                var data = buffer.AsSpan(0, len);
                len = FilterBuf(data, data);
                return buffer.AsSpan(0, len).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        static private int FilterBuf(Span<byte> src, Span<byte> dst)
        {
            int curr;
            int retLen = 0;
            while (2 < src.Length)
            {
                curr = int.Min(64, src.Length);
                src.Slice(2, curr - 2).CopyTo(dst);
                dst = dst.Slice(curr - 2);
                src = src.Slice(curr);
                retLen += curr - 2;
            }
            return retLen;
        }
        /// <summary>
        /// Set the DTR enabled
        /// </summary>
        /// <param name="dtrEnable"></param>
        /// <exception cref="ControlTransferException"></exception>
        public override void SetDtrEnabled(bool dtrEnable)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int value = dtrEnable ? ModemControlDtrEnable : ModemControlDtrDisable;
            int index = UsbInterfaceIndex + 1;
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set Dtr failed", result, RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);

            DtrEnable = dtrEnable;
        }
        /// <summary>
        /// Set the RTS enabled
        /// </summary>
        /// <param name="rtsEnable"></param>
        /// <exception cref="ControlTransferException"></exception>
        public override void SetRtsEnabled(bool rtsEnable)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int value = rtsEnable ? ModemControlRtsEnable : ModemControlRtsDisable;
            int index = UsbInterfaceIndex + 1;

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set Rts failed", result, RequestTypeHostToDevice, ModemControlRequest, value, index, null, 0, ControlTimeout);

            RtsEnable = rtsEnable;
        }
    }
}