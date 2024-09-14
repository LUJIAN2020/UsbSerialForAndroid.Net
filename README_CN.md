# UsbSerialForAndroid.Net

### 👓 介绍
这是一个Android的USB串口通讯的驱动程序库，支持MAUI、Avalonia的Android平台USB串行硬件进行通信。该库最低支持Android 6.0（API23.0）。由于net6.0-android已经失去支持，所以最低支持net8.0-android。它使用 Android 3.1+ 上可用的 [Android USB Host API](http://developer.android.com/guide/topics/connectivity/usb/host.html)。

无需root访问权限、ADK或特殊内核驱动程序;所有驱动程序通过C#实现。根据设备的VendorID 和ProductID 获取DeviceId自动选择适当的驱动程序进行读写。

本库基于Java实现开源库[usb-serial-for-android](https://github.com/mik3y/usb-serial-for-android)和C#实现开源库[UsbSerialForAndroid](https://github.com/anotherlab/UsbSerialForAndroid)修改而来。

### 💡 使用方法

如果需要广播接收USB的插入和拔出，这个需要在构造函数内进行注册
```
//注册广播接收器
//isShowToast=true USB授权后会有Toast显示
//attached USB添加后的委托
//detached USB删除后的委托
//errorCallback 广播接收器内部OnReceive()方法错误回调
UsbDriverFactory.RegisterUsbBroadcastReceiver();

//取消注册广播接收器
UsbDriverFactory.UnRegisterUsbBroadcastReceiver();
```

通过帮助类获取当前所有插入的设备
```
var usbDevices = UsbManagerHelper.GetAllUsbDevices();
```

创建驱动程序，发送和接收数据，创建前会检查是否支持的驱动，如果不支持将抛出异常
```
//通过设备Id（DeviceId）创建
int deviceId = 0x03EA;
UsbDriverBase usbDriver = UsbDriverFactory.CreateUsbDriver(deviceId);

//或者通过厂商标识（VendorId）和产品编号（ProductId）创建
int vendorId = 0x0403;
int productId = 0x6001;
UsbDriverBase usbDriver = UsbDriverFactory.CreateUsbDriver(vendorId,productId);

//打开USB设备，设置通讯参数
usbDriver.Open(115200, 8, StopBits.One, Parity.None);

//发送数据
var data = new byte[] { 0x01, 0x01, 0x00, 0x00, 0x00, 0x08, 0x3D, 0xCC };
usbDriver.Write(data);

//接收数据
var buffer = usbDriver.Read();

//关闭USB设备
usbDriver.Close();
```

### 🚀支持的驱动

**Technology Devices International, Ltd**

FTDI = 0x0403 Future 

- FT232R = 0x6001
- FT2232H = 0x6010
- FT4232H = 0x6011
- FT232H = 0x6014
- FT231X = 0x6015

**Prolific Technology, Inc.** 

Prolific = 0x067B 

- PL2303 = 0x2303
- PL2303GC = 0x23A3
- PL2303GB = 0x23B3
- PL2303GT = 0x23C3
- PL2303GL = 0x23D3
- PL2303GE = 0x23E3
- PL2303GS = 0x23F3

**QinHeng Electronics** 

QinHeng = 0x1A86 

- HL340 = 0x7523
- CH341A = 0x5523

**Silicon Labs** 

SiliconLabs = 0x10C4

- CP2102 = 0xEA60
- CP2105 = 0xEA70
- CP2108 = 0xEA71
- CP2110 = 0xEA80

### 🎨DEMO截图

| MAUI | Avalonia |
| ----------- | ----------- |
| ![alt text](./Images/MauiDemo.jpg) | ![alt text](./Images/AvaloniaDemo.jpg) |

