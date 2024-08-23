using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Extensions;

namespace UsbSerialForAndroid.Net.Drivers
{
    public class ArduinoSerialDriver : UsbDriverBase
    {
        public const int UsbSubclassAcm = 2;
        public const int UsbRecipInterface = 0x01;
        public const int UsbRtAcm = UsbConstants.UsbTypeClass | UsbRecipInterface;
        public const int SetLineCoding = 0x20;
        public const int SetControlLineState = 0x22;
        private int controlIndex;
        private UsbInterface? controlInterface;
        private UsbEndpoint? controlEndpoint;
        public ArduinoSerialDriver(UsbDevice usbDevice) : base(usbDevice) { }
        public override void Open(int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            UsbDeviceConnection = UsbManager.OpenDevice(UsbDevice);

            if (UsbDevice.InterfaceCount == 1)
            {
                //device might be castrated ACM device, trying single interface logic
                OpenSingleInterface();
            }
            else
            {
                OpenInterface();
            }

            SetParameters(baudRate, dataBits, stopBits, parity);
        }
        private void OpenSingleInterface()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            controlIndex = 0;
            controlInterface = UsbInterface = UsbDevice.GetInterface(0);
            if (!UsbDeviceConnection.ClaimInterface(UsbInterface, true))
                throw new Exception($"Could not claim interface {UsbInterfaceIndex}");

            for (int i = 0; i < UsbInterface.EndpointCount; ++i)
            {
                var ep = UsbInterface.GetEndpoint(i);
                if ((ep?.Direction == UsbAddressing.In) && (ep.Type == UsbAddressing.XferInterrupt))
                {
                    controlEndpoint = ep;
                }
                else if ((ep?.Direction == UsbAddressing.In) && (ep.Type == UsbAddressing.XferBulk))
                {
                    UsbEndpointRead = ep;
                }
                else if ((ep?.Direction == UsbAddressing.Out) && (ep.Type == UsbAddressing.XferBulk))
                {
                    UsbEndpointWrite = ep;
                }
            }
        }
        private int GetFirstInterfaceIdFromDescriptors()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            var descriptors = UsbDeviceConnection.GetDescriptors();

            if (descriptors.Count > 0 &&
                descriptors[0].Length == 18 &&
                descriptors[0][1] == 1 && // bDescriptorType
                descriptors[0][4] == (byte)(UsbClass.Misc) && //bDeviceClass
                descriptors[0][5] == 2 && // bDeviceSubClass
                descriptors[0][6] == 1)
            {
                int port = -1;
                for (int i = 1; i < descriptors.Count; i++)
                {
                    if (descriptors[i].Length == 8 &&
                        descriptors[i][1] == 0x0b && // bDescriptorType == IAD
                        descriptors[i][4] == (byte)UsbClass.Comm && // bFunctionClass == CDC
                        descriptors[i][5] == UsbSubclassAcm)
                    {
                        port++;
                        if (port == UsbInterfaceIndex && descriptors[i][3] == 2)
                        {
                            return descriptors[i][2];
                        }
                    }
                }
            }
            return -1;
        }
        private void OpenInterface()
        {
            int id = GetFirstInterfaceIdFromDescriptors();
            if (id >= 0)
            {
                for (int i = 0; i < UsbDevice.InterfaceCount; i++)
                {
                    var usbInterface = UsbDevice.GetInterface(i);
                    if (usbInterface.Id == id || usbInterface.Id == id + 1)
                    {
                        if (usbInterface.InterfaceClass == UsbClass.Comm && (int)usbInterface.InterfaceSubclass == UsbSubclassAcm)
                        {
                            controlIndex = usbInterface.Id;
                            controlInterface = usbInterface;
                        }
                        if (usbInterface.InterfaceClass == UsbClass.CdcData)
                        {
                            UsbInterface = usbInterface;
                        }
                    }
                }
            }
            if (controlInterface == null || UsbInterface == null)
            {
                int controlInterfaceCount = 0;
                int dataInterfaceCount = 0;
                for (int i = 0; i < UsbDevice.InterfaceCount; i++)
                {
                    var usbInterface = UsbDevice.GetInterface(i);
                    if (usbInterface.InterfaceClass == UsbClass.Comm && (int)usbInterface.InterfaceSubclass == UsbSubclassAcm)
                    {
                        if (controlInterfaceCount == UsbInterfaceIndex)
                        {
                            controlIndex = usbInterface.Id;
                            controlInterface = usbInterface;
                        }
                        controlInterfaceCount++;
                    }
                    if (usbInterface.InterfaceClass == UsbClass.CdcData)
                    {
                        if (dataInterfaceCount == UsbInterfaceIndex)
                        {
                            UsbInterface = usbInterface;
                        }
                        dataInterfaceCount++;
                    }
                }
            }

            ArgumentNullException.ThrowIfNull(controlInterface);
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            if (!UsbDeviceConnection.ClaimInterface(controlInterface, true))
                throw new Exception("Could not claim control interface");

            controlEndpoint = controlInterface.GetEndpoint(0);
            if (controlEndpoint?.Direction != UsbAddressing.In || controlEndpoint?.Type != UsbAddressing.XferInterrupt)
                throw new Exception("Invalid control endpoint");

            ArgumentNullException.ThrowIfNull(UsbInterface);

            if (!UsbDeviceConnection.ClaimInterface(UsbInterface, true))
                throw new Exception("Could not claim data interface");

            for (int i = 0; i < UsbInterface.EndpointCount; i++)
            {
                var ep = UsbInterface.GetEndpoint(i);
                if (ep?.Direction == UsbAddressing.In && ep.Type == UsbAddressing.XferBulk)
                {
                    UsbEndpointRead = ep;
                }
                else if (ep?.Direction == UsbAddressing.Out && ep.Type == UsbAddressing.XferBulk)
                {
                    UsbEndpointWrite = ep;
                }
            }
        }
        private void SetParameters(int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            if (baudRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "Invalid baud rate");

            if (dataBits < 5 || dataBits > 8)
                throw new ArgumentOutOfRangeException(nameof(dataBits), dataBits, "Invalid data bits");

            byte stopBitsByte = stopBits switch
            {
                StopBits.One => 0,
                StopBits.OnePointFive => 1,
                StopBits.Two => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, "Invalid stop bits"),
            };

            byte parityBitesByte = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, "Invalid parity"),
            };

            var buffer = new byte[]{
                (byte) ( baudRate & 0xff),
                (byte) ((baudRate >> 8 ) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff),
                stopBitsByte,
                parityBitesByte,
                dataBits};

            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)UsbRtAcm, SetLineCoding, 0, controlIndex, buffer, buffer.Length, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set parameters failed", result, UsbRtAcm, SetLineCoding, 0, controlIndex, buffer, buffer.Length, ControlTimeout);
        }
        public override void Close()
        {
            UsbDeviceConnection?.ReleaseInterface(controlInterface);
            UsbDeviceConnection?.ReleaseInterface(UsbInterface);
            UsbDeviceConnection?.Close();
        }
        public override void SetDtrEnable(bool value)
        {
            DtrEnable = value;
            SetDtrRts();
        }
        public override void SetRtsEnable(bool value)
        {
            RtsEnable = value;
            SetDtrRts();
        }
        private void SetDtrRts()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);

            int value = (RtsEnable ? 0x2 : 0) | (DtrEnable ? 0x1 : 0);
            int result = UsbDeviceConnection.ControlTransfer((UsbAddressing)UsbRtAcm, SetControlLineState, value, controlIndex, null, 0, ControlTimeout);
            if (result != 0)
                throw new ControlTransferException("Set dtr rts failed", result, UsbRtAcm, SetControlLineState, value, controlIndex, null, 0, ControlTimeout);
        }
    }
}
