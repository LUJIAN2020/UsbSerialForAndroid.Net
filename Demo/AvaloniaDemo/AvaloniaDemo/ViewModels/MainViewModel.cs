using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using AvaloniaDemo.Enums;
using AvaloniaDemo.Models;
using AvaloniaDemo.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace AvaloniaDemo.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NotificationService notificationService;
        private readonly IUsbService usbService;
        public MainViewModel(NotificationService notificationService, IUsbService usbService)
        {
            this.notificationService = notificationService;
            this.usbService = usbService;
        }
        [ObservableProperty] private ObservableCollection<UsbDeviceInfo> usbDeviceInfos = new();
        [ObservableProperty] private string? receivedText;
        [ObservableProperty] private bool sendHexIsChecked = true;
        [ObservableProperty] private bool receivedHexIsChecked = true;
        [ObservableProperty] private UsbDeviceInfo? selectedDeviceInfo;
        public void GetAllCommand()
        {
            try
            {
                UsbDeviceInfos = new(usbService.GetUsbDeviceInfos());
                notificationService.ShowMessage($"USB设备总数：{UsbDeviceInfos.Count}");
            }
            catch (Exception ex)
            {
                notificationService.ShowMessage(ex.Message, NotificationType.Error);
            }
        }
        public void ConnectDeviceCommand(object[] items)
        {
            try
            {
                if (items[0] is UsbDeviceInfo usbDeviceInfo)
                {
                    if (items[1] is ComboBoxItem item1 &&
                        items[2] is ComboBoxItem item2 &&
                        items[3] is ComboBoxItem item3 &&
                        items[4] is ComboBoxItem item4)
                    {
                        int baudRate = Convert.ToInt32(item1.Content?.ToString());
                        byte dataBits = Convert.ToByte(item2.Content?.ToString());
                        byte stopBits = Convert.ToByte(item3.Content?.ToString());
                        var parity = item4.Content?.ToString() ?? Parity.None.ToString();
                        var par = (Parity)Enum.Parse(typeof(Parity), parity);
                        usbService?.Open(usbDeviceInfo.DeviceId, baudRate, dataBits, stopBits, (byte)par);
                        notificationService.ShowMessage("连接成功");
                    }
                }
                else
                {
                    throw new Exception("没有选择USB设备");
                }
            }
            catch (Exception ex)
            {
                notificationService.ShowMessage(ex.Message, NotificationType.Error);
            }
        }
        public void SendCommand(string? text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new Exception("发送内容不能为空");

                var buffer = SendHexIsChecked
                    ? TextToBytes(text)
                    : Encoding.Default.GetBytes(text);
                usbService.Send(buffer);
                notificationService.ShowMessage("发送成功");
            }
            catch (Exception ex)
            {
                notificationService.ShowMessage(ex.Message, NotificationType.Error);
            }
        }
        public void ReceiveCommand()
        {
            try
            {
                var buffer = usbService.Receive();
                if (buffer is null)
                {
                    notificationService.ShowMessage("没有数据可读");
                    return;
                }

                ReceivedText = ReceivedHexIsChecked
                ? string.Join(' ', buffer.Select(c => c.ToString("X2")))
                : Encoding.Default.GetString(buffer);
                notificationService.ShowMessage($"接收成功,接收长度：{buffer.Length}");
            }
            catch (Exception ex)
            {
                notificationService.ShowMessage(ex.Message, NotificationType.Error);
            }
        }
        private static byte[] TextToBytes(string hexString)
        {
            var text = hexString.ToUpper();
            if (text.Any(c => c < '0' && c > 'F'))
                throw new Exception("发送内容为16进制");

            text = text.Replace(" ", "");
            if (text.Length % 2 > 0)
                text = text.PadLeft(text.Length + 1, '0');
            var buffer = new byte[text.Length / 2];
            for (int i = 0; i < text.Length; i += 2)
            {
                string value = text.Substring(i, 2);
                buffer[i / 2] = Convert.ToByte(value, 16);
            }

            return buffer;
        }
        public void TestConnectCommand()
        {
            try
            {
                bool b = usbService.IsConnection();
                notificationService.ShowMessage(b ? "已连接" : "未连接");
            }
            catch (Exception ex)
            {
                notificationService.ShowMessage(ex.Message, NotificationType.Error);
            }
        }
    }
}