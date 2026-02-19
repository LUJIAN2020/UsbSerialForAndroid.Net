#if DEBUG
//#define TRACE_INFO
#endif
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Helper;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// USB driver base class
    /// </summary>
    public abstract class UsbDriverBase : BaseDisposable
    {
        [Conditional("TRACE_INFO")]
        public static void TraceInfo(string msg) => Console.WriteLine($"[USBDRIVER] {msg}");

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
        public abstract ValueTask OpenAsync(int baudRate, byte dataBits, StopBits stopBits, Parity parity);
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
        public void Close() => CloseAsync().SynchronousWait();
        public async virtual Task CloseAsync(List<Exception>? errors = null)
        {
            TraceInfo("CloseAsync");
            try
            {
                await DeinitBuffersAsync();
            }
            catch (Exception ex)
            {
                errors?.Add(ex);
            }
            UsbEndpointRead?.Dispose(); UsbEndpointRead = null;
            UsbEndpointWrite?.Dispose(); UsbEndpointWrite = null;
            UsbDeviceConnection?.ReleaseInterface(UsbInterface);
            UsbInterface?.Dispose(); UsbInterface = null;
            UsbDeviceConnection?.Close(); UsbDeviceConnection = null;
            TraceInfo("CloseAsync - Ok");
        }
        protected async override ValueTask DisposeAsyncCore()
        {
            await CloseAsync();
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
        protected int UsbWriteBufLength = 256;
        protected int UsbReadBufLength = 256;
        protected int UsbRequestCount = 64;
        public const int UsbMinRequestCount = 4;

        public int ReadHeaderLength = 0;
        public FilterDataFn? FilterData;
        public delegate int FilterDataFn(Span<byte> src, Span<byte> dst);

        protected UsbRequest? _usbWriteRequest;
        protected List<UsbRequest> _readRequests = [];

        protected UsbRequest? _current = null;
        protected CancellationTokenSource? _processUsbTasksToken;

        protected Task? _receiveTask;
        protected Task? _sendTask;

        Channel<UsbRequest>? _writeChannel;
        Channel<UsbRequest>? _dataRqChannel;
        Channel<UsbRequest>? _sendRqChannel;

        protected async Task InitBuffersAsync()
        {
            TraceInfo("InitAsync");
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            ArgumentNullException.ThrowIfNull(UsbEndpointWrite);
            ArgumentNullException.ThrowIfNull(UsbEndpointRead);

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
            _sendRqChannel = Channel.CreateUnbounded<UsbRequest>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _dataRqChannel = Channel.CreateUnbounded<UsbRequest>(new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            // create and send to OS all read request
            for (int i = 0; i < UsbRequestCount; i++)
            {
                var rq = new UsbRequest();
                rq.Initialize(UsbDeviceConnection, UsbEndpointRead);
                _readRequests.Add(rq);
                rq.ClientData = new NetDirectByteBuffer(UsbReadBufLength);
                await _sendRqChannel.Writer.WriteAsync(rq);
            }
            StartProcessingTasks();
            TraceInfo("InitAsync - Ok");
        }
        protected async Task DeinitBuffersAsync(List<Exception>? errors = null)
        {
            TraceInfo("DeinitAsync");
            try
            {
                await StopProcessingTasks();
            }
            catch (Exception ex)
            {
                errors?.Add(ex);
            }
            try
            {
                Interlocked.Exchange(ref _writeChannel, null)?.Writer.Complete();
                Interlocked.Exchange(ref _dataRqChannel, null)?.Writer.Complete();
                Interlocked.Exchange(ref _sendRqChannel, null)?.Writer.Complete();

                // clear requests
                var oldReadRequests = Interlocked.Exchange(ref _readRequests, []);
                foreach (var item in oldReadRequests)
                {
                    if (item.ClientData is NetDirectByteBuffer buf)
                        buf.Dispose();
                    item.Cancel();
                    item.Close();
                    item.Dispose();
                }
                Interlocked.Exchange(ref _usbWriteRequest, null)?.Dispose();
                TraceInfo("DeinitAsync - Ok");
            }
            catch (Exception ex)
            {
                errors?.Add(ex); // unexpected errors
            }
        }
        private void StartProcessingTasks()
        {
            _processUsbTasksToken = new();
            _sendTask = Task.Run(() => UsbSendAsync(_processUsbTasksToken.Token));
            _receiveTask = Task.Run(() => UsbReceiveAsync(_processUsbTasksToken.Token));
        }
        private void ThrowIfNotStartedOrFaulted()
        {
            ArgumentNullException.ThrowIfNull(_receiveTask);
            ArgumentNullException.ThrowIfNull(_sendTask);
            if (_receiveTask.IsFaulted)
                throw _receiveTask.Exception;
            if (_sendTask.IsFaulted)
                throw _sendTask.Exception;
        }
        private async Task StopProcessingTasks()
        {
            _processUsbTasksToken?.Cancel();
            // await exit all tasks // clear all tasks
            List<Exception> exceptions = [];
            if (null != _receiveTask)
            {
                try
                {
                    await _receiveTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    exceptions.Add(ex); // mostly when device disconnected, or unexpected errors
                }
                _receiveTask = null;
            }
            if (null != _sendTask)
            {
                try
                {
                    await _sendTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    exceptions.Add(ex); // mostly when device disconnected, or unexpected errors
                }
                _sendTask = null;
            }
            Interlocked.Exchange(ref _processUsbTasksToken, null)?.Dispose();
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        }
        protected virtual async Task UsbReceiveAsync(CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            ArgumentNullException.ThrowIfNull(_writeChannel);
            ArgumentNullException.ThrowIfNull(_dataRqChannel);
            ArgumentNullException.ThrowIfNull(_sendRqChannel);
            var wr = _writeChannel.Writer;
            var dataRqWriter = _dataRqChannel.Writer;
            var sendRqWriter = _sendRqChannel.Writer;
            UsbRequest? dataRq = null;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    dataRq = await UsbDeviceConnection.RequestWaitAsync().WaitAsync(ct);
                }
                catch (Java.Lang.IllegalArgumentException iEx)
                {
                    TraceInfo($"IllegalArgumentException {iEx}");
                    continue;
                }
                catch (BufferOverflowException boEx)
                {
                    TraceInfo($"BufferOverflowException {boEx}");
                    continue;
                }
                if (null == dataRq)
                {
                    TraceInfo($"response is null");
                    if (!TestConnection())
                    {
                        TraceInfo($"device disconnected - close port");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                List<Exception> errors = [];
                                await CloseAsync(errors);
                                if (0 < errors.Count)
                                    TraceInfo($"close errors: {errors}");
                            }
                            catch (Exception ex)
                            {
                                TraceInfo($"unexpected error {ex}");
                            }
                        }, ct);
                        break;
                    }
                    continue;
                }
                if (ReferenceEquals(dataRq.Endpoint, UsbEndpointRead))
                {
                    if (ReadHeaderLength < ((NetDirectByteBuffer?)dataRq.ClientData!).Position)
                    {
                        await dataRqWriter.WriteAsync(dataRq, ct);
                        // if _dataRqChannel is full, deqeue from the beginning of the queue
                        // we'll leave a reserve of UsbMinRequestCount(4) active requests in the OS queue.
                        if (UsbRequestCount - UsbMinRequestCount < _dataRqChannel.Reader.Count)
                        {
                            dataRq = Interlocked.Exchange(ref _current, null);// try return current first dequeued item
                            dataRq ??= await _dataRqChannel.Reader.ReadAsync(ct);
                            await sendRqWriter.WriteAsync(dataRq, ct);
                        }
                    }
                    else
                        await sendRqWriter.WriteAsync(dataRq, ct);
                    continue;
                }
                if (ReferenceEquals(dataRq.Endpoint, UsbEndpointWrite))
                {
                    await wr.WriteAsync(dataRq, ct);
                }
            }
        }
        /// <summary>
        /// receives requests
        /// , and send back to OS request queue. 
        /// Does not allow the OS request queue to starve
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected async Task UsbSendAsync(CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(_sendRqChannel);
            var sendRqReader = _sendRqChannel.Reader;
            UsbRequest? sendRq;
            while (!ct.IsCancellationRequested)
            {
                sendRq = await sendRqReader.ReadAsync(ct);
                if (sendRq?.ClientData is NetDirectByteBuffer buf)
                {
                    if (ReferenceEquals(sendRq.Endpoint, UsbEndpointRead))
                    {
                        buf.Rewind();
                        buf.ClientData = null;
                    }
                    if (!(OperatingSystem.IsAndroidVersionAtLeast(26) ?
                        sendRq.Queue(buf.JavaBuffer) : sendRq.Queue(buf.JavaBuffer, buf.JavaBuffer.Capacity())))
                        throw new Java.IO.IOException("Error queueing request.");
                }
                else
                    throw new Exception("broken emptyRq - ClientData is null or not NetDirectByteBuffer");
            }
        }
        public virtual async Task<int> ReadAsync(byte[] dstBuf, int offset, int count, CancellationToken ct = default)
        {
            TraceInfo($"Start read count={count}");
            ThrowIfNotStartedOrFaulted();
            ArgumentNullException.ThrowIfNull(_dataRqChannel);
            ArgumentNullException.ThrowIfNull(_sendRqChannel);
            var sendRqWriter = _sendRqChannel.Writer;
            var dataRqReader = _dataRqChannel.Reader;
            int readed = 0;
            UsbRequest? rq = null;
            try
            {
                rq = Interlocked.Exchange(ref _current, null); // try get previos peeked data,
                rq ??= await dataRqReader.ReadAsync(ct); // if there is none, do async wait
                // get all buffered data, not more than the requested size
                while (!ct.IsCancellationRequested && rq?.ClientData is NetDirectByteBuffer buf)
                {
                    var data = buf.MemBuffer.Span.Slice(0, buf.Position);
                    TraceInfo($"data length {buf.Position}");
                    if (null != FilterData && buf.Position > (count - readed))
                    {
                        buf.Position = FilterData(data, data);
                        buf.ClientData = true;// filtered
                        data = buf.MemBuffer.Span.Slice(0, buf.Position);
                        TraceInfo($"filter in buf, filtered Length={buf.Position}");
                    }
                    int currLen;
                    if (null == FilterData || buf.ClientData is true)
                    {
                        currLen = int.Min(count - readed, buf.Position);
                        TraceInfo($"copy filtered {currLen}");
                        data.Slice(0, currLen).CopyTo(dstBuf.AsSpan(offset));
                    }
                    else
                    {
                        currLen = FilterData(data, dstBuf.AsSpan(offset));
                        TraceInfo($"filter copy {currLen}");
                    }
                    readed += currLen;
                    offset += currLen;
                    TraceInfo($"readed={readed}");
                    if (readed == count)
                    {
                        var rest = buf.Position - currLen;
                        TraceInfo($"rest={rest}");
                        if (0 < rest)
                        {
                            data.Slice(currLen, rest).CopyTo(data.Slice(0, rest));
                            buf.Position = rest;
                            rq = Interlocked.Exchange(ref _current, rq);
                        }
                        else
                        {
                            await sendRqWriter.WriteAsync(rq, ct);
                            rq = null;
                        }
                    }
                    else
                    {
                        await sendRqWriter.WriteAsync(rq, ct);
                        dataRqReader.TryRead(out rq);
                    }
                    // _emptyReader!.Count does not work on single reader
                    //TraceInfo($"[USBDRIVER]: net buf={buf} data={_dataReader.Count} , free={_emptyReader!.Count}");
                }
                return readed;
            }
            finally
            {
                if (null != rq)
                {
                    await sendRqWriter.WriteAsync(rq, CancellationToken.None);
                }
            }
        }
        public virtual async Task<int> WriteAsync(byte[] wbuf, int offset, int count, CancellationToken ct = default)
        {
            ThrowIfNotStartedOrFaulted();
            ArgumentNullException.ThrowIfNull(_writeChannel);
            ArgumentNullException.ThrowIfNull(_sendRqChannel);
            UsbRequest? wr = null;
            try
            {
                var receiveQueue = _writeChannel.Reader;
                var sendQueue = _sendRqChannel.Writer;
                int rest = count;
                while (0 < rest)
                {
                    ct.ThrowIfCancellationRequested();
                    if (null == wr && !receiveQueue.TryRead(out wr))
                        wr = await receiveQueue.ReadAsync(ct);// get a free write-request
                    using var buf = new NetDirectByteBuffer(wbuf, offset, int.Min(rest, UsbWriteBufLength));
                    wr.ClientData = buf;
                    await sendQueue.WriteAsync(wr, ct);//send request
                    wr = null; // here we no longer own the request 
                    wr = await receiveQueue.ReadAsync(ct);//wait response
                    offset += buf.Position;
                    rest -= buf.Position;
                    //TraceInfo($"sent {buf.Position}");
                }
                TraceInfo($"sent total {count - rest}");
                return count - rest;
            }
            catch (OperationCanceledException)
            {
                // we need to wait for a request to remove from the queue, even if we cancel the request
                // the UsbDispatchAsync thread will do wait and return the free request to _writeChannel queue
                var isCanceled = wr?.Cancel();
                // isCanceled == true - operation canceled
                // isCanceled == false - the operation does not require cancellation, because has already been completed
                TraceInfo($"cancel write is {isCanceled}");
                throw;
            }
            finally
            {
                if (null != wr)
                {
                    // we will get here from "wr.QueueReq"
                    // or upon completion of sending
                    await _writeChannel.Writer.WriteAsync(wr, CancellationToken.None);
                }
            }
        }
        public async Task FlushAsync(CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(_dataRqChannel);
            ArgumentNullException.ThrowIfNull(_sendRqChannel);
            var sendQueue = _sendRqChannel.Writer;
            await StopProcessingTasks();
            var curr = Interlocked.Exchange(ref _current, null);
            if (curr != null)
                await sendQueue.WriteAsync(curr, ct);
            await foreach (var rq in _dataRqChannel.Reader.ReadAllAsync(ct))
                await sendQueue.WriteAsync(rq, ct);
            StartProcessingTasks();
        }
    }
}