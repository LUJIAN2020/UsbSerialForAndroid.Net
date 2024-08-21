using MauiDemo.ViewModels;

namespace MauiDemo
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            this.BindingContext = vm;
            baudRatePicker.SelectedIndex = 6;
            dataBitsPicker.SelectedIndex = 3;
            stopBitsPicker.SelectedIndex = 0;
            parityPicker.SelectedIndex = 0;
        }
    }
}
