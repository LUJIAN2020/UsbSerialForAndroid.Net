using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaDemo.Models;

public partial class UsbDeviceInfo : ObservableObject
{
    [ObservableProperty] private int vendorId;
    [ObservableProperty] private string? serialNumber;
    [ObservableProperty] private string? productName;
    [ObservableProperty] private int productId;
    [ObservableProperty] private string? manufacturerName;
    [ObservableProperty] private int interfaceCount;
    [ObservableProperty] private int deviceProtocol;
    [ObservableProperty] private string? deviceName;
    [ObservableProperty] private int deviceId;
    [ObservableProperty] private int configurationCount;
    [ObservableProperty] private string? version;
}