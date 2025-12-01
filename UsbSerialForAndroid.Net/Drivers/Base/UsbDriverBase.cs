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
        public const int UsbBufLength = 256;
        public const int UsbBufCount = 256;
        public const int UsbRequestCount = 64;

        public int ReadHeaderLength = 0;
        public FilterDataFn? FilterData;
        public delegate int FilterDataFn(Span<byte> src, Span<byte> dst);

        protected UsbRequest? _usbWriteRequest;
        protected List<UsbRequest> _readRequests = [];

        NetDirectByteBuffer? _current = null;
        protected CancellationTokenSource? _readerExit;
        protected List<NetDirectByteBuffer> _allBuffers = [];

        protected Task? _dispatchTask;
        protected Task? _readTask;
        protected Task? _filterTask;

        Channel<UsbRequest>? _writeChannel;
        Channel<UsbRequest>? _readChannel;

        Channel<NetDirectByteBuffer>? _emptyChannel;
        Channel<NetDirectByteBuffer>? _dataChannel;

        ChannelReader<NetDirectByteBuffer>? _emptyReader;
        ChannelWriter<NetDirectByteBuffer>? _emptyWriter;
        ChannelReader<NetDirectByteBuffer>? _dataReader;
        ChannelWriter<NetDirectByteBuffer>? _dataWriter;

        protected async Task InitBuffersAsync()
        {
            Logger.Trace($"[USBDRIVER]: InitAsync");
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            ArgumentNullException.ThrowIfNull(UsbEndpointWrite);
            ArgumentNullException.ThrowIfNull(UsbEndpointRead);

            // initializing queue of empty buffers
            _emptyChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
            // initializing queue of filled data buffers
            _dataChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            _emptyReader = _emptyChannel.Reader;
            _emptyWriter = _emptyChannel.Writer;
            _dataReader = _dataChannel.Reader;
            _dataWriter = _dataChannel.Writer;

            // initializing buffers
            for (int i = 0; i < UsbBufCount; i++)
            {
                var newBuf = new NetDirectByteBuffer(UsbBufLength);
                _allBuffers.Add(newBuf);
                await _emptyWriter.WriteAsync(newBuf);
            }
            // initializing a queue of free write requests
            _usbWriteRequest = new();
            _usbWriteRequest.Initialize(UsbDeviceConnection, UsbEndpointWrite);
            _writeChannel = Channel.CreateBounded<UsbRequest>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            await _writeChannel.Writer.WriteAsync(_usbWriteRequest);// just one
            // initializing a queue of free read requests
            _readChannel = Channel.CreateUnbounded<UsbRequest>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            // create and send to OS all read request
            for (int i = 0; i < UsbRequestCount; i++)
            {
                var usbReadRequest = new UsbRequest();
                usbReadRequest.Initialize(UsbDeviceConnection, UsbEndpointRead);
                _readRequests.Add(usbReadRequest);
                //await _readChannel.Writer.WriteAsync(usbReadRequest);
                var buf = await _emptyReader.ReadAsync();
                usbReadRequest.ClientData = buf;
                usbReadRequest.QueueReq(buf.JavaBuffer);
            }
            _readerExit = new();
            _readTask = Task.Run(() => PostReadRequestsAsync(_readerExit.Token));
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
                _dataReader = null;
                _dataWriter = null;

                _writeChannel = null;
                _readChannel = null;
                _emptyChannel = null;
                _dataChannel = null;
                foreach (var item in _allBuffers)
                    item.Dispose();
                _allBuffers.Clear();
                // clear requests
                var oldReadRequests = Interlocked.Exchange(ref _readRequests, []);
                foreach (var item in oldReadRequests)
                {
                    item.Cancel();
                    item.Close();
                    item.Dispose();
                }
                Interlocked.Exchange(ref _usbWriteRequest, null)?.Dispose();
                Logger.Trace($"[USBDRIVER]: DeinitAsync - Ok");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
        }

        protected virtual async Task UsbDispatchAsync(CancellationToken ct = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                ArgumentNullException.ThrowIfNull(_writeChannel);
                ArgumentNullException.ThrowIfNull(_readChannel);
                var wr = _writeChannel.Writer;
                var rd = _readChannel.Writer;
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
                    catch (TimeoutException boEx)
                    {
                        Logger.Warning($"[USBDRIVER]: TimeoutException {boEx}");
                        continue;
                    }
                    if (null == response)
                    {
                        Logger.Warning($"[USBDRIVER]: response is null");
                        if (!TestConnection())
                            await Task.Run(Close, ct);
                        continue;
                    }
                    if (ReferenceEquals(response.Endpoint, UsbEndpointRead))
                    {
                        await rd.WriteAsync(response, ct);
                        continue;
                    }
                    if (ReferenceEquals(response.Endpoint, UsbEndpointWrite))
                    {
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
        protected NetDirectByteBuffer? TryGetBuffer()
        {
            if (_emptyReader!.TryRead(out var buf))
                return buf;
            Logger.Debug($"[USBDRIVER]: failed attempt to get from empty queue");
            buf = Interlocked.Exchange(ref _current, null);
            if (null != buf)
                return buf;
            Logger.Debug($"[USBDRIVER]: failed attempt to get from data current");
            if (_dataReader!.TryRead(out buf))
                return buf;
            Logger.Debug($"[USBDRIVER]: failed attempt to get from filter queue");
            return buf;
        }
        /// <summary>
        /// receives requests, send data to data-queue, get free buffers
        /// , and send back to OS request queue. 
        /// Does not allow the OS request queue to starve
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected virtual async Task PostReadRequestsAsync(CancellationToken ct = default)
        {
            NetDirectByteBuffer? buf = null;
            UsbRequest? rq;
            try
            {
                ArgumentNullException.ThrowIfNull(_readChannel);
                ArgumentNullException.ThrowIfNull(_emptyReader);
                ArgumentNullException.ThrowIfNull(_dataWriter);
                ArgumentNullException.ThrowIfNull(_dataReader);
                while (!ct.IsCancellationRequested)
                {
                    rq = await _readChannel.Reader.ReadAsync(ct);

                    if (null != rq.ClientData)
                    {
                        buf = (NetDirectByteBuffer)rq.ClientData;
                        if (buf == null)
                            Logger.Warning($"[USBDRIVER]: response buffer is null");
                        else
                        {
                            if (ReadHeaderLength < buf.Position)
                            {
                                //Logger.Debug($"[USBDRIVER]: received {buf.Position}");
                                await _dataWriter.WriteAsync(buf, ct);
                                buf = null;
                            }
                        }
                        rq.ClientData = null;
                    }
                    while (null == buf)
                    {
                        ct.ThrowIfCancellationRequested();
                        buf = TryGetBuffer();
                    }
                    buf.Rewind();
                    buf.ClientData = null;
                    // associate the request with a buffer and send it to the OS queue
                    rq.ClientData = buf;
                    rq.QueueReq(buf.JavaBuffer);
                    buf = null;
                }
            }
            catch (OperationCanceledException)
            {
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
            Logger.Debug($"[USBDRIVER]: Start read count={count}");
            ArgumentNullException.ThrowIfNull(_emptyWriter);
            ArgumentNullException.ThrowIfNull(_dataReader);
            int readed = 0;
            NetDirectByteBuffer? buf = Interlocked.Exchange(ref _current, null); // try get previos peeked data,
            buf ??= await _dataReader.ReadAsync(ct); // if there is none, do async wait
            // get all buffered data, not more than the requested size
            while (!ct.IsCancellationRequested && null != buf)
            {
                var data = buf.MemBuffer.Span.Slice(0, buf.Position);
                Logger.Debug($"[USBDRIVER]: data length {buf.Position}");
                if (null != FilterData && buf.Position > (count - readed))
                {
                    buf.Position = FilterData(data, data);
                    buf.ClientData = true;// filtered
                    data = buf.MemBuffer.Span.Slice(0, buf.Position);
                    Logger.Debug($"[USBDRIVER]: filter in buf, filtered Length={buf.Position}");
                }
                int currLen;
                if (null == FilterData || buf.ClientData is true)
                {
                    currLen = int.Min(count - readed, buf.Position);
                    Logger.Debug($"[USBDRIVER]: copy filtered {currLen}");
                    data.Slice(0, currLen).CopyTo(dstBuf.AsSpan(offset));
                }
                else
                {
                    currLen = FilterData(data, dstBuf.AsSpan(offset));
                    Logger.Debug($"[USBDRIVER]: filter copy {currLen}");
                }
                readed += currLen;
                offset += currLen;
                Logger.Debug($"[USBDRIVER]: readed={readed}");
                if (readed == count)
                {
                    var rest = buf.Position - currLen;
                    Logger.Debug($"[USBDRIVER]: rest={rest}");
                    if (0 < rest)
                    {
                        data.Slice(currLen, rest).CopyTo(data.Slice(0, rest));
                        buf.Position = rest;
                        buf = Interlocked.Exchange(ref _current, buf);
                    }
                    else
                    {
                        await _emptyWriter.WriteAsync(buf, ct);
                        buf = null;
                    }
                }
                else
                {
                    await _emptyWriter.WriteAsync(buf, ct);
                    _dataReader.TryRead(out buf);
                }
                // _emptyReader!.Count does not work on single reader
                //Logger.Trace($"[USBDRIVER]: net buf={buf} data={_dataReader.Count} , free={_emptyReader!.Count}");
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
                    wr.QueueReq(buf.JavaBuffer);//send request
                    wr = null; // here we no longer own the request 
                    wr = await writeRqQueueReader.ReadAsync(ct);//wait response
                    offset += buf.Position;
                    rest -= buf.Position;
                    //Logger.Trace($"[USBDRIVER]: sent {buf.Position}");
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
        public async void FlushAsync(CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(_emptyWriter);
            ArgumentNullException.ThrowIfNull(_dataReader);
            Task t2 = Task.Run(async () =>
            {
                await foreach (var item in _dataReader.ReadAllAsync(ct))
                    await _emptyWriter.WriteAsync(item, ct);
            }, ct);
            await t2;
        }
    }
}