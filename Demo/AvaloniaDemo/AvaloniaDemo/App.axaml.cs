using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AvaloniaDemo.ViewModels;
using AvaloniaDemo.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaDemo
{
    public partial class App : Application
    {
        public static ServiceProvider? services;
        public static readonly ServiceCollection serviceCollection = new ServiceCollection();
        public static ServiceCollection CurrentServiceCollection => serviceCollection;
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Line below is needed to remove Avalonia data validation.
                // Without this line you will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
            services = serviceCollection.BuildServiceProvider();
        }
        public static WindowNotificationManager? NotificationManager { get; set; }
        public static T? GetService<T>()
        {
            if (services is null) return default;
            return services.GetService<T>();
        }
    }
}