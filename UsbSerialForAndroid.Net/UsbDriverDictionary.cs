using Android.Hardware.Usb;
using System.Collections.Generic;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;

namespace UsbSerialForAndroid.Net;

public delegate UsbDriverBase CreateUsbDriverFn(UsbDevice usbDevice);

public class ProductDriverDictionary
{
    public Dictionary<int, CreateUsbDriverFn>? ById;
    public CreateUsbDriverFn? Default = null;

    public UsbDriverBase Create(UsbDevice usbDevice)
    {
        if (null != ById && ById.TryGetValue(usbDevice.DeviceId, out var driverFn))
            return driverFn(usbDevice);
        if (null != Default)
            return Default.Invoke(usbDevice);
        throw new NotSupportedDriverException(usbDevice);
    }
    public bool IsSupported(int productId)
    {
        if (null != ById && ById.TryGetValue(productId, out var driverFn))
            return true;
        if (null != Default)
            return true;
        return false;
    }
}

public class UsbDriverDictionary
{
    public readonly Dictionary<int, ProductDriverDictionary> VendorDict = [];
    public UsbDriverDictionary()
    {
        Register((int)VendorIds.FTDI, (usbDevice) => new FtdiSerialDriver(usbDevice));
        Register((int)VendorIds.Prolific, (usbDevice) => new ProlificSerialDriver(usbDevice));
        Register((int)VendorIds.QinHeng, (usbDevice) => new QinHengSerialDriver(usbDevice));
        Register((int)VendorIds.SiliconLabs, (usbDevice) => new SiliconLabsSerialDriver(usbDevice));
        Register((int)VendorIds.Atmel, (usbDevice) => new CdcAcmSerialDriver(usbDevice));
        Register((int)VendorIds.GigaDevice, (usbDevice) => new CdcAcmSerialDriver(usbDevice));
        Register((int)VendorIds.Arduino, (usbDevice) => new CdcAcmSerialDriver(usbDevice));
        Register((int)VendorIds.Nrf, (usbDevice) => new CdcAcmSerialDriver(usbDevice));
    }
    void Register(int VendorId, CreateUsbDriverFn? fn)
    {
        if (!VendorDict.TryGetValue(VendorId, out var vendorCreator))
        {
            vendorCreator = new();
            VendorDict.Add(VendorId, vendorCreator);
        }
        vendorCreator.Default = fn;
    }
    void Register(int VendorId, int ProductId, CreateUsbDriverFn? fn)
    {
        if (!VendorDict.TryGetValue(VendorId, out var vendorCreator))
        {
            vendorCreator = new();
            VendorDict.Add(VendorId, vendorCreator);
        }
        if (null == vendorCreator.ById)
            vendorCreator.ById = [];
        if (null == fn)
            vendorCreator.ById.Remove(ProductId);
        else
            vendorCreator.ById[ProductId] = fn;
    }
    public UsbDriverBase CreateUsbDriver(UsbDevice usbDevice)
    {
        if (VendorDict.TryGetValue(usbDevice.VendorId, out var vendorCreator))
            return vendorCreator.Create(usbDevice);
        throw new NotSupportedDriverException(usbDevice);
    }
    public bool HasSupportedDriver(int vendorId, int productId)
    {
        if (VendorDict.TryGetValue(vendorId, out var vendorCreator))
            return vendorCreator.IsSupported(productId);
        return false;
    }
}