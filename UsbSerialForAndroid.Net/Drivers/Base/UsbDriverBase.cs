using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Extensions;
using UsbSerialForAndroid.Net.Helper;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// USB driver base class
    /// </summary>
    public abstract class UsbDriverBase : BaseDisposable
    {
        private static readonly UsbManager usbManager = GetUsbManager();
        public const byte XON = 17;
        public const byte XOFF = 19;
        public const int DefaultTimeout = 1000;
        public const int DefaultBufferLength = 1024 * 4;
        public const int UsbBufLength = 512; // USB 2.0 High-Speed Endpoints Bulk: 512 bytes maximum
        public const int UsbBufCount = 16;
        public const int DefaultBaudRate = 9600;
        public const byte DefaultDataBits = 8;
        public const StopBits DefaultStopBits = StopBits.One;
        public const Parity DefaultParity = Parity.None;
        public const int DefaultUsbInterfaceIndex = 0;
        /// <summary>
        /// flow control
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
        /// USB interface index to use
        /// </summary>
        public int UsbInterfaceIndex { get; set; } = DefaultUsbInterfaceIndex;
        /// <summary>
        /// USB manager
        /// </summary>
        public static UsbManager UsbManager => usbManager;
        /// <summary>
        /// USB device
        /// </summary>
        public UsbDevice UsbDevice { get; private set; }
        /// <summary>
        /// USB device connection
        /// </summary>
        public UsbDeviceConnection? UsbDeviceConnection { get; protected set; }
        /// <summary>
        /// USB interface
        /// </summary>
        public UsbInterface? UsbInterface { get; protected set; }
        /// <summary>
        /// read endpoint
        /// </summary>
        public UsbEndpoint? UsbEndpointRead { get; protected set; }
        /// <summary>
        /// write endpoint
        /// </summary>
        public UsbEndpoint? UsbEndpointWrite { get; protected set; }
        /// <summary>
        /// read timeout
        /// </summary>
        public int ReadTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// write timeout
        /// </summary>
        public int WriteTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// Control timeout
        /// </summary>
        public int ControlTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// is connected
        /// </summary>
        public bool Connected => TestConnection();
        /// <summary>
        /// USB driver base class
        /// </summary>
        /// <param name="_usbDevice"></param>
        protected UsbDriverBase(UsbDevice _usbDevice)
        {
            UsbDevice = _usbDevice;
        }
        /// <summary>
        /// Get usbManager
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">UsbManager is null exception</exception>
        private static UsbManager GetUsbManager()
        {
            var usebService = Application.Context.GetSystemService(Context.UsbService);
            return usebService is UsbManager manager
                ? manager
                : throw new NullReferenceException("UsbManager is null");
        }
        /// <summary>
        /// open the usb device
        /// </summary>
        /// <param name="baudRate">baudRate</param>
        /// <param name="dataBits">dataBits</param>
        /// <param name="stopBits">stopBits</param>
        /// <param name="parity">parity</param>
        public void Open(int baudRate, byte dataBits, StopBits stopBits, Parity parity) =>
            OpenAsync(baudRate, dataBits, stopBits, parity).AsTask().SynchronousWait();
        /// <summary>
        /// Set DTR enabled
        /// </summary>
        /// <param name="value">true=enabled</param>
        public abstract void SetDtrEnabled(bool value);
        /// <summary>
        /// Set RTS enabled
        /// </summary>
        /// <param name="value">true=enabled</param>
        public abstract void SetRtsEnabled(bool value);
        /// <summary>
        /// close the usb device
        /// </summary>
        public void Close() => DisposeAsync().AsTask().SynchronousWait();

        public abstract ValueTask OpenAsync(int baudRate, byte dataBits, StopBits stopBits, Parity parity);
        protected override async ValueTask DisposeAsyncCore()
        {
            Logger.Trace($"[USBDRIVER]: DisposeAsync");
            await DeinitBuffersAsync();
            UsbEndpointRead?.Dispose(); UsbEndpointRead = null;
            UsbEndpointWrite?.Dispose(); UsbEndpointWrite = null;
            UsbDeviceConnection?.ReleaseInterface(UsbInterface);
            UsbInterface?.Dispose(); UsbInterface = null;
            UsbDeviceConnection?.Close(); UsbDeviceConnection = null;
            Logger.Trace($"[USBDRIVER]: DisposeAsync - Ok");
        }
        /// <summary>
        /// sync write
        /// </summary>
        /// <param name="buffer">write data</param>
        /// <exception cref="BulkTransferException">write failed exception</exception>
        public virtual void Write(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int result = UsbDeviceConnection.BulkTransfer(UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
            if (result < 0)
                throw new BulkTransferException("Write failed", result, UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
        }
        /// <summary>
        /// sync read
        /// </summary>
        /// <returns>The read data is returned after the read succeeds. Null data is returned after the read fails</returns>
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
        /// async write
        /// </summary>
        /// <param name="buffer">write data</param>
        /// <returns></returns>
        /// <exception cref="BulkTransferException">Write failed exception</exception>
        public virtual Task WriteAsync(byte[] buffer)
        {
            return WriteAsync(buffer, 0, buffer.Length);
        }
        /// <summary>
        /// async read
        /// </summary>
        /// <returns>The read data is returned after the read succeeds. Null data is returned after the read fails</returns>
        public virtual async Task<byte[]?> ReadAsync()
        {
            var dest = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int len = await ReadAsync(dest, 0, dest.Length);
                return dest.AsSpan(0, len).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dest);
            }
        }
        /// <summary>
        /// get the interface of the current USB device
        /// </summary>
        /// <param name="usbDevice">USB device</param>
        /// <returns>UsbInterface array</returns>
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
        /// test connection
        /// </summary>
        /// <returns>true=connected</returns>
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

        public int ReadHeaderLength = 0;
        public FilterDataFn? FilterData;
        public delegate int FilterDataFn(Span<byte> src, Span<byte> dst);

        protected UsbRequest? _usbWriteRequest;
        protected UsbRequest? _usbReadRequest;

        NetDirectByteBuffer? _current = null;
        protected CancellationTokenSource? _readerExit;
        protected List<NetDirectByteBuffer> _allBuffers = [];

        protected Task? _dispatchTask;
        protected Task? _readTask;
        protected Task? _filterTask;

        Channel<UsbRequest>? _writeChannel;
        Channel<UsbRequest>? _readChannel;

        Channel<NetDirectByteBuffer>? _emptyChannel;
        Channel<NetDirectByteBuffer>? _filterChannel;
        Channel<NetDirectByteBuffer>? _dataChannel;

        ChannelReader<NetDirectByteBuffer>? _emptyReader;
        ChannelWriter<NetDirectByteBuffer>? _emptyWriter;
        ChannelReader<NetDirectByteBuffer>? _filterReader;
        ChannelWriter<NetDirectByteBuffer>? _filterWriter;
        ChannelReader<NetDirectByteBuffer>? _dataReader;
        ChannelWriter<NetDirectByteBuffer>? _dataWriter;

        protected async Task InitBuffersAsync()
        {
            Logger.Trace($"[USBDRIVER]: InitAsync");
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            ArgumentNullException.ThrowIfNull(UsbEndpointWrite);
            ArgumentNullException.ThrowIfNull(UsbEndpointRead);
            _usbReadRequest = new();
            _usbReadRequest.Initialize(UsbDeviceConnection, UsbEndpointRead);
            _usbWriteRequest = new();
            _usbWriteRequest.Initialize(UsbDeviceConnection, UsbEndpointWrite);
            _writeChannel = Channel.CreateBounded<UsbRequest>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            // write request queue - free items
            await _writeChannel.Writer.WriteAsync(_usbWriteRequest);

            _readChannel = Channel.CreateBounded<UsbRequest>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _emptyChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true,
            });
            _filterChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
            _dataChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            _emptyReader = _emptyChannel.Reader;
            _emptyWriter = _emptyChannel.Writer;
            _filterReader = _filterChannel.Reader;
            _filterWriter = _filterChannel.Writer;
            _dataReader = _dataChannel.Reader;
            _dataWriter = _dataChannel.Writer;

            for (int i = 0; i < UsbBufCount; i++)
            {
                var newBuf = new NetDirectByteBuffer(UsbBufLength);
                _allBuffers.Add(newBuf);
                await _emptyWriter.WriteAsync(newBuf);
            }
            _readerExit = new();
            if (null != FilterData)
                _filterTask = Task.Run(() => ProcessFilterAsync(_readerExit.Token));
            _readTask = Task.Run(() => UsbReadAsync(_readerExit.Token));
            _dispatchTask = Task.Run(() => UsbDispatchAsync(_readerExit.Token));
            Logger.Trace($"[USBDRIVER]: InitAsync - Ok");
        }
        protected async Task DeinitBuffersAsync()
        {
            Logger.Trace($"[USBDRIVER]: DeinitAsync");
            try
            {
                // cancel all tasks
                _readerExit?.Cancel();
                _writeChannel?.Writer.Complete();
                _readChannel?.Writer.Complete();
                _emptyChannel?.Writer.Complete();
                _filterChannel?.Writer.Complete();
                _dataChannel?.Writer.Complete();
                Interlocked.Exchange(ref _readerExit, null)?.Dispose();
                // await exit all tasks // clear all tasks
                if (null != _dispatchTask)
                {
                    await _dispatchTask;
                    _dispatchTask = null;
                }
                if (null != _readTask)
                {
                    await _readTask;
                    _readTask = null;
                }
                if (null != _filterTask)
                {
                    await _filterTask;
                    _filterTask = null;
                }
                // clear all buffers
                _emptyReader = null;
                _emptyWriter = null;
                _filterReader = null;
                _filterWriter = null;
                _dataReader = null;
                _dataWriter = null;

                _writeChannel = null;
                _readChannel = null;
                _emptyChannel = null;
                _filterChannel = null;
                _dataChannel = null;
                foreach (var item in _allBuffers)
                    item.Dispose();
                _allBuffers.Clear();
                // clear requests
                Interlocked.Exchange(ref _usbReadRequest, null)?.Dispose();
                Interlocked.Exchange(ref _usbWriteRequest, null)?.Dispose();
                Logger.Trace($"[USBDRIVER]: DeinitAsync - Ok");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
        }
        protected virtual async Task ProcessFilterAsync(CancellationToken ct = default)
        {
            NetDirectByteBuffer? buf;
            try
            {
                ArgumentNullException.ThrowIfNull(FilterData);
                ArgumentNullException.ThrowIfNull(_filterReader);
                ArgumentNullException.ThrowIfNull(_dataWriter);
                ArgumentNullException.ThrowIfNull(_emptyWriter);
                while (!ct.IsCancellationRequested)
                {
                    if (!_filterReader.TryRead(out buf))
                        buf = await _filterReader.ReadAsync(ct);
                    var data = buf.MemBuffer.Span.Slice(0, buf.Position);
                    buf.Position = FilterData(data, data);
                    if (0 < buf.Position)
                        await _dataWriter.WriteAsync(buf, ct);
                    else
                        await _emptyWriter.WriteAsync(buf, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit ProcessFilter");
        }
        protected virtual async Task UsbDispatchAsync(CancellationToken ct = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                ArgumentNullException.ThrowIfNull(_writeChannel);
                ArgumentNullException.ThrowIfNull(_readChannel);
                var wr = _writeChannel.Writer;
                var read = _readChannel.Writer;
                UsbRequest? response = null;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        response = await UsbDeviceConnection.RequestWaitAsync();
                    }
                    catch (Java.Lang.IllegalArgumentException iEx)
                    {
                        Logger.Warning($"[USBDRIVER]: IllegalArgumentException {iEx}");
                        continue;
                    }
                    catch (BufferOverflowException boEx)
                    {
                        Logger.Warning($"[USBDRIVER]: BufferOverflowException {boEx}");
                        continue;
                    }
                    if (null == response)
                    {
                        Logger.Warning($"[USBDRIVER]: response is null");
                        if (!TestConnection())
                            await Task.Run(Close, ct);
                        continue;
                    }
                    if (ReferenceEquals(response, _usbReadRequest))
                    {
                        //Logger.Trace($"[USBDRIVER]: _tcsRead");
                        await read.WriteAsync(response, ct);
                        continue;
                    }
                    if (ReferenceEquals(response, _usbWriteRequest))
                    {
                        //Logger.Trace($"[USBDRIVER]: _tcsWrite");
                        await wr.WriteAsync(response, ct);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit UsbDispatchAsync");
        }
        protected virtual async Task UsbReadAsync(CancellationToken ct = default)
        {
            NetDirectByteBuffer? buf = null;
            try
            {
                ArgumentNullException.ThrowIfNull(_usbReadRequest);
                ArgumentNullException.ThrowIfNull(_readChannel);
                ArgumentNullException.ThrowIfNull(_emptyReader);
                ArgumentNullException.ThrowIfNull(_filterReader);
                ArgumentNullException.ThrowIfNull(_dataReader);
                var outQueue = (null == FilterData) ? _dataWriter : _filterWriter;
                ArgumentNullException.ThrowIfNull(outQueue);
                var reader = _readChannel.Reader;
                while (!ct.IsCancellationRequested)
                {
                    while (null == buf)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (_emptyReader.TryRead(out buf))
                            continue;
                        else
                            Logger.Debug($"[USBDRIVER]: FIND _emptyReader=0");
                        buf = Interlocked.Exchange(ref _current, null);
                        if (null != buf)
                            continue;
                        else
                            Logger.Debug($"[USBDRIVER]: FIND _current=0");
                        if (_dataReader.TryRead(out buf))
                            continue;
                        else
                            Logger.Debug($"[USBDRIVER]: FIND _dataReader=0");
                        if (_filterReader.TryRead(out buf))
                            continue;
                        else
                            Logger.Debug($"[USBDRIVER]: FIND _filterReader=0");
                    }
                    buf.Rewind();
                    //Logger.Trace($"[USBDRIVER]: read requested");
                    _usbReadRequest.QueueReq((ByteBuffer)buf);
                    _ = await reader.ReadAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    if (ReadHeaderLength < buf.Position)
                    {
                        Logger.Debug($"[USBDRIVER]: received {buf.Position}");
                        await outQueue.WriteAsync(buf, ct);
                        buf = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _usbReadRequest?.Cancel();
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit UsbReadAsync");
        }
        public virtual async Task<int> ReadAsync(byte[] dstBuf, int offset, int count, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(_emptyWriter);
            ArgumentNullException.ThrowIfNull(_dataReader);
            int readed = 0;
            NetDirectByteBuffer? buf = Interlocked.Exchange(ref _current, null); // try get previos peeked data,
            if (null == buf && !_dataReader.TryRead(out buf)) // try peek already buffered data,
                buf = await _dataReader.ReadAsync(ct); // if there is none, do async wait
            // get all buffered data, not more than the requested size
            while (!ct.IsCancellationRequested && null != buf)
            {
                var currLen = int.Min(count, buf.Position);
                var data = buf.MemBuffer.Span;
                buf.MemBuffer.Span.Slice(0, currLen).CopyTo(dstBuf.AsSpan(offset));
                readed += currLen;
                offset += currLen;
                count -= currLen;
                var rest = buf.Position - currLen;
                if (0 < rest)
                {
                    data.Slice(currLen, rest).CopyTo(data.Slice(0));
                    buf.Position = rest;
                    buf = Interlocked.Exchange(ref _current, buf);
                    //return readed;
                }
                else
                {
                    await _emptyWriter.WriteAsync(buf, ct);
                    _dataReader.TryRead(out buf);
                }
            }
            return readed;
        }
        public virtual async Task<int> WriteAsync(byte[] wbuf, int offset, int count, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(_writeChannel);
            UsbRequest? wr = null;
            try
            {
                var writeRqQueueReader = _writeChannel.Reader;
                int rest = count;
                while (0 < rest)
                {
                    ct.ThrowIfCancellationRequested();
                    if (null == wr)
                        wr = await writeRqQueueReader.ReadAsync(ct);// get a free write-request
                    using var buf = new NetDirectByteBuffer(wbuf, offset, int.Min(rest, UsbBufLength));
                    wr.QueueReq((ByteBuffer)buf);//send request
                    wr = null; // here we no longer own the request 
                    wr = await writeRqQueueReader.ReadAsync(ct);//wait response
                    offset += buf.Position;
                    rest -= buf.Position;
                }
                return count - rest;
            }
            catch (OperationCanceledException)
            {
                // we need to wait for a request to remove from the queue, even if we cancel the request
                // the UsbDispatchAsync thread will do wait and return the free request to _writeChannel queue
                var isCanceled = wr?.Cancel();
                // isCanceled == true - operation canceled
                // isCanceled == false - the operation does not require cancellation, because has already been completed
                Logger.Trace($"[USBDRIVER]: cancel {isCanceled}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: write Exception {ex}");
                throw;
            }
            finally
            {
                if (null != wr)
                {
                    // we will get here from "wr.QueueReq"
                    // or upon completion of sending
                    await _writeChannel.Writer.WriteAsync(wr);
                }
            }
        }
    }
}