using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using System;
using System.Buffers;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net.Drivers
{
    public abstract class UsbDriverBase
    {
        private static readonly UsbManager usbManager = GetUsbManager();
        public const byte XON = 17;
        public const byte XOFF = 19;
        public const int DefaultTimeout = 1000;
        public const int DefaultBufferLength = 1024 * 4;
        public const int DefaultBaudRate = 9600;
        public const byte DefaultDataBits = 8;
        public const StopBits DefaultStopBits = StopBits.One;
        public const Parity DefaultParity = Parity.None;
        public const int DefaultUsbInterfaceIndex = 0;
        /// <summary>
        /// 流控制
        /// </summary>
        public FlowControl FlowControl { get; protected set; }
        /// <summary>
        /// Data Terminal Ready Enable
        /// </summary>
        public bool DtrEnable { get; protected set; }
        /// <summary>
        /// Request To Send Enable
        /// </summary>
        public bool RtsEnable { get; protected set; }
        /// <summary>
        /// 要使用的USB接口索引
        /// </summary>
        public int UsbInterfaceIndex { get; set; } = DefaultUsbInterfaceIndex;
        public static UsbManager UsbManager => usbManager;
        /// <summary>
        /// 打开的USB设备
        /// </summary>
        public UsbDevice UsbDevice { get; private set; }
        /// <summary>
        /// USB连接
        /// </summary>
        public UsbDeviceConnection? UsbDeviceConnection { get; protected set; }
        /// <summary>
        /// USB接口
        /// </summary>
        public UsbInterface? UsbInterface { get; protected set; }
        /// <summary>
        /// USB读数据的端点
        /// </summary>
        public UsbEndpoint? UsbEndpointRead { get; protected set; }
        /// <summary>
        /// USB写数据的端点
        /// </summary>
        public UsbEndpoint? UsbEndpointWrite { get; protected set; }
        /// <summary>
        /// 读数据超时
        /// </summary>
        public int ReadTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// 写数据超时
        /// </summary>
        public int WriteTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// 控制数据超时
        /// </summary>
        public int ControlTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// 是否连接
        /// </summary>
        public bool Connected => TestConnection();
        protected UsbDriverBase(UsbDevice _usbDevice)
        {
            UsbDevice = _usbDevice;
        }
        /// <summary>
        /// 获取UsbManager
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        private static UsbManager GetUsbManager()
        {
            var usebService = Application.Context.GetSystemService(Context.UsbService);
            return usebService is UsbManager manager
                ? manager
                : throw new NullReferenceException("UsbManager is null");
        }
        /// <summary>
        /// 打开USB设备
        /// </summary>
        /// <param name="baudRate"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        public abstract void Open(int baudRate, byte dataBits, StopBits stopBits, Parity parity);
        /// <summary>
        /// 设置DTR使能
        /// </summary>
        /// <param name="value"></param>
        public abstract void SetDtrEnable(bool value);
        /// <summary>
        /// 设置RTS使能
        /// </summary>
        /// <param name="value"></param>
        public abstract void SetRtsEnable(bool value);
        /// <summary>
        /// 关闭USB
        /// </summary>
        public virtual void Close()
        {
            UsbDeviceConnection?.Close();
        }
        /// <summary>
        /// 写
        /// </summary>
        /// <param name="buffer">写入的数据</param>
        /// <returns>写入成功返回写入数据长度，写入失败返回-1</returns>
        public virtual void Write(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int result = UsbDeviceConnection.BulkTransfer(UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
            if (result < 0)
                throw new BulkTransferException("Write failed", result, UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
        }
        /// <summary>
        /// 读
        /// </summary>
        /// <returns>读成功返回读到的数据，读失败返回空</returns>
        public virtual byte[]? Read()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int result = UsbDeviceConnection.BulkTransfer(UsbEndpointRead, buffer, 0, DefaultBufferLength, ReadTimeout);
                return result >= 0
                    ? buffer.AsSpan().Slice(0, result).ToArray()
                    : default;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        /// <summary>
        /// 写（异步）
        /// </summary>
        /// <param name="buffer">写入的数据</param>
        /// <returns>写入成功返回写入数据长度，写入失败返回-1</returns>
        public virtual async Task WriteAsync(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int result = await UsbDeviceConnection.BulkTransferAsync(UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
            if (result < 0)
                throw new BulkTransferException("Write failed", result, UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
        }
        /// <summary>
        /// 读（异步）
        /// </summary>
        /// <returns>读成功返回读到的数据，读失败返回空</returns>
        public virtual async Task<byte[]?> ReadAsync()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int result = await UsbDeviceConnection.BulkTransferAsync(UsbEndpointRead, buffer, 0, DefaultBufferLength, ReadTimeout);
                return result >= 0
                    ? buffer.AsSpan().Slice(0, result).ToArray()
                    : default;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        /// <summary>
        /// 获取当前USB设备的接口
        /// </summary>
        /// <param name="usbDevice">USB设备</param>
        /// <returns>接口数组</returns>
        public static UsbInterface[] GetUsbInterfaces(UsbDevice usbDevice)
        {
            var array = new UsbInterface[usbDevice.InterfaceCount];
            for (int i = 0; i < usbDevice.InterfaceCount; i++)
            {
                array[i] = usbDevice.GetInterface(i);
            }
            return array;
        }
        /// <summary>
        /// 测试连接
        /// </summary>
        /// <returns></returns>
        public bool TestConnection()
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                byte[] buf = new byte[2];
                const int request = 0;//GET_STATUS
                int len = UsbDeviceConnection.ControlTransfer(UsbAddressing.DirMask, request, 0, 0, buf, buf.Length, 100);
                return len == 2;
            }
            catch
            {
                return false;
            }
        }
    }
}