using Android.Widget;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiDemo.Enums;
using MauiDemo.Models;
using MauiDemo.Services;
using System.Collections.ObjectModel;
using System.Text;
using UsbSerialForAndroid.Resources;

namespace MauiDemo.ViewModels
{
    public partial class MainViewModel(IUsbService usbService) : ObservableObject
    {
        [ObservableProperty] public partial ObservableCollection<UsbDeviceInfo> UsbDeviceInfos { get; set; } = new();
        [ObservableProperty] public partial string? ReceivedText { get; set; }
        [ObservableProperty] public partial bool SendHexIsChecked { get; set; } = true;
        [ObservableProperty] public partial bool ReceivedHexIsChecked { get; set; } = true;
        [ObservableProperty] public partial UsbDeviceInfo? SelectedDeviceInfo { get; set; }
        public RelayCommand GetAllCommand => new(() =>
        {
            try
            {
                UsbDeviceInfos = new(usbService.GetUsbDeviceInfos());
                ShowMessage(AppResources.DevicesCount + UsbDeviceInfos.Count);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        });
        public RelayCommand<object[]> ConnectDeviceCommand => new((items) =>
        {
            try
            {
                if (items is not null && items.Length == 5 && items[0] is UsbDeviceInfo usbDeviceInfo)
                {
                    if (items[1] is int baudRate &&
                        items[2] is byte dataBits &&
                        items[3] is byte stopBits &&
                        items[4] is Parity parity)
                    {

                        usbService.Open(usbDeviceInfo.DeviceId, baudRate, dataBits, stopBits, (byte)parity);
                        ShowMessage(AppResources.ConnectionSuccess);
                    }
                }
                else
                {
                    ShowMessage(AppResources.NoDeviceSelected);
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        });
        public RelayCommand<string?> SendCommand => new((text) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new Exception(AppResources.ContentEmptyException);

                var buffer = SendHexIsChecked
                    ? TextToBytes(text)
                    : Encoding.Default.GetBytes(text);
                usbService.Send(buffer);
                ShowMessage(AppResources.SentSucessfully);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        });
        public RelayCommand ReceiveCommand => new(() =>
        {
            try
            {
                var buffer = usbService.Receive();
                if (buffer is null)
                {
                    ShowMessage(AppResources.NoDataToRead);
                    return;
                }
                ReceivedText = ReceivedHexIsChecked
                    ? string.Join(' ', buffer.Select(c => c.ToString("X2")))
                    : Encoding.Default.GetString(buffer);
                ShowMessage(AppResources.ReceiveSuccess + buffer.Length);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        });
        [ObservableProperty] public partial bool? IsEnableWrite { get; set; } = true;
        [ObservableProperty] public partial string? WriteSpeed { get; set; }
        [ObservableProperty] public partial string? ReadSpeed { get; set; }
        [RelayCommand(IncludeCancelCommand = true)]
        public async Task SendReceive(object[] items, CancellationToken ct)
        {
            try
            {
                if (items is null
                || items.Length != 5
                || items[0] is not UsbDeviceInfo usbDeviceInfo
                || items[1] is not int baudRate
                || items[2] is not byte dataBits
                || items[3] is not byte stopBits
                || items[4] is not Parity parity)
                    return;
                var test = new IOTestModel() { EnableWrite = this.IsEnableWrite ?? false };
                test.PropertyChanged += (obj, arg) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        switch (arg.PropertyName)
                        {
                            default: break;
                            case nameof(WriteSpeed): WriteSpeed = test.WriteSpeed; break;
                            case nameof(ReadSpeed): ReadSpeed = test.ReadSpeed; break;
                        }
                    });
                };
                await Task.Run(() => test.StartTestAsync(usbDeviceInfo.DeviceId,
                    baudRate, dataBits, stopBits, (byte)parity, ct), ct);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
            WriteSpeed = null;
            ReadSpeed = null;
        }
        public RelayCommand TestConnectCommand => new(() =>
        {
            try
            {
                bool b = usbService.IsConnection();
                ShowMessage(b ? AppResources.Connected : AppResources.Disconnected);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        });
        private static byte[] TextToBytes(string hexString)
        {
            var text = hexString.ToUpper();
            if (text.Any(c => c < '0' && c > 'F'))
                throw new Exception(AppResources.ContentFormatExceptionHex);

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
        private static void ShowMessage(string msg)
        {
#if ANDROID
            Toast.MakeText(Android.App.Application.Context, msg, ToastLength.Short)?.Show();
#endif
        }
    }
}
