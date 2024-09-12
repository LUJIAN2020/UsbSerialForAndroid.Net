using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaDemo.Extensions;
using AvaloniaDemo.Services;
using AvaloniaDemo.ViewModels;

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
        }
    }
}