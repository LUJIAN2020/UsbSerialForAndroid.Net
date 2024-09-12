using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaDemo.Android
{
    [Activity(
        Label = "AvaloniaDemo.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.RegisterPlatformService = OnRegisterPlatformService;
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }

        private void OnRegisterPlatformService(IServiceCollection services)
        {
            services.AddSingleton<IUsbService, UsbService>();
        }
    }
}
