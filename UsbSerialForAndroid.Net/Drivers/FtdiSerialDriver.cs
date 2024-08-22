using Android.Hardware.Usb;
using System;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// Future Technology Devices International, Ltd
    /// </summary>
    public class FtdiSerialDriver : UsbDriverBase
    {
        private bool baudRateWithPort = false;
        public const int RequestTypeHostToDevice = UsbConstants.UsbTypeVendor | (int)UsbAddressing.Out;
        public const int ReadHeaderLength = 2; // contains MODEM_STATUS

        public const int ModemControlRequest = 1;
        public const int ModemControlDtrEnable = 0x0101;
        public const int ModemControlDtrDisable = 0x0100;
        public const int ModemControlRtsEnable = 0x0202;
        public const int ModemControlRtsDisable = 0x0200;

        public const int RestRequest = 0;
        public const int ResetAll = 0;

        public const int SetFlowControlRequest = 2;
        public const int SetBaudRateRequest = 3;
        public const int SetDataRequest = 4;
        public FtdiSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        public override void Open(int baudRate = DefaultBaudRate, byte dataBits = DefaultDataBits, StopBits stopBits = DefaultStopBits, Parity parity = DefaultParity)
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

            int value = (DtrEnable ? ModemControlDtrEnable : ModemControlDtrDisable) | (RtsEnable ? ModemControlRtsEnable : ModemControlRtsDisable);
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, ModemControlRequest, value, UsbInterfaceIndex + 1, null, 0, ControlTimeout);
            if (result != 0)
                throw new Exception($"Init RTS,DTR failed: result={result}");

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
        }
        private void Reset()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, RestRequest, ResetAll, UsbInterfaceIndex + 1, null, 0, ControlTimeout);
            if (result != 0)
                throw new Exception($"Reset failed: result={result}");
        }
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

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetFlowControlRequest, value, index, null, 0, WriteTimeout);
            if (result != 0)
                throw new Exception($"Set flow control failed: result={result}");
            FlowControl = flowControl;
        }
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

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetBaudRateRequest, value, index, null, 0, WriteTimeout);
            if (result != 0)
                throw new Exception($"Setting baudrate failed: result={result}");
        }
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

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)RequestTypeHostToDevice, SetDataRequest, config, UsbInterfaceIndex + 1, null, 0, WriteTimeout);
            if (result != 0)
                throw new Exception($"Setting parameters failed: result={result}");
        }
        public override byte[]? Read()
        {
            var buffer = base.Read();
            if (buffer is not null && buffer.Length >= ReadHeaderLength)
            {
                return buffer.AsSpan()
                    .Slice(ReadHeaderLength, buffer.Length - ReadHeaderLength)
                    .ToArray();
            }
            return buffer;
        }
        public override async Task<byte[]?> ReadAsync()
        {
            var buffer = await base.ReadAsync();
            if (buffer is not null && buffer.Length >= ReadHeaderLength)
            {
                return buffer.AsSpan()
                    .Slice(ReadHeaderLength, buffer.Length - ReadHeaderLength)
                    .ToArray();
            }
            return buffer;
        }
        public override void SetDtrEnable(bool dtrEnable)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer(
                (UsbAddressing)RequestTypeHostToDevice,
                ModemControlRequest,
                dtrEnable ? ModemControlDtrEnable : ModemControlDtrDisable,
                UsbInterfaceIndex + 1,
                null,
                0,
                ControlTimeout);
            if (result != 0) throw new Exception($"Set DTR failed: result={result}");

            DtrEnable = dtrEnable;
        }
        public override void SetRtsEnable(bool rtsEnable)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer(
                (UsbAddressing)RequestTypeHostToDevice,
                ModemControlRequest,
                rtsEnable ? ModemControlRtsEnable : ModemControlRtsDisable,
                UsbInterfaceIndex + 1,
                null,
                0,
                ControlTimeout);
            if (result != 0) throw new Exception($"Set DTR failed: result={result}");

            RtsEnable = rtsEnable;
        }
    }
}