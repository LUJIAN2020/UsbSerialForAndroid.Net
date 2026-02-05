using MauiDemo.ViewModels;

namespace MauiDemo
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            baudRatePicker.SelectedIndex = 9;
            dataBitsPicker.SelectedIndex = 3;
            stopBitsPicker.SelectedIndex = 0;
            parityPicker.SelectedIndex = 0;
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is not MainViewModel vm)
                return;
            // if device already connected
            // get all devices to list
            // and select first
            vm.GetAllCommand.Execute(null);
            if (0 < vm.UsbDeviceInfos.Count)
                vm.SelectedDeviceInfo = vm.UsbDeviceInfos[0];
        }
    }
}
