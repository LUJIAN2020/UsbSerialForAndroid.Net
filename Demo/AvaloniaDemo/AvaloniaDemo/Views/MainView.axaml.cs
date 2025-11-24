using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using AvaloniaDemo.Extensions;
using AvaloniaDemo.Services;
using AvaloniaDemo.ViewModels;
using Microsoft.Extensions.Logging;
using System;

namespace AvaloniaDemo.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            DataContext = App.GlobalHost.GetService<MainViewModel>();
        }
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            var topLevel = TopLevel.GetTopLevel(this);
            var notificationService = App.GlobalHost.GetService<NotificationService>();
            notificationService?.SetTopLevel(topLevel);
            if (OperatingSystem.IsAndroid())
            {
                this.Margin = new Avalonia.Thickness(0, 40, 0, 40);
                var insetsManager = topLevel?.InsetsManager;
                if (insetsManager != null)
                {
                    insetsManager.DisplayEdgeToEdgePreference = true;
                    insetsManager.IsSystemBarVisible = false;
                    insetsManager.SystemBarColor = Avalonia.Media.Colors.Red;
                }
            }

        }
    }
}