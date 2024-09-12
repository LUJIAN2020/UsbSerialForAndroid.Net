using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaDemo.Extensions;
using AvaloniaDemo.Services;
using AvaloniaDemo.ViewModels;
using AvaloniaDemo.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace AvaloniaDemo
{
    public partial class App : Application
    {
        public static Action<IServiceCollection>? RegisterPlatformService;
        public static IHost GlobalHost => Host.CreateDefaultBuilder()
            .ConfigureServices((services) =>
            {
                RegisterPlatformService?.Invoke(services);
                services.AddTransient<NotificationService>();
                services.AddSingleton<MainWindow>();
                services.RegisterSingleton<MainView, MainViewModel>();
            }).Build();
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = GlobalHost.GetService<MainWindow>();
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = GlobalHost.GetService<MainView>();
            }
            base.OnFrameworkInitializationCompleted();
            await GlobalHost.StartAsync();
        }
    }
}