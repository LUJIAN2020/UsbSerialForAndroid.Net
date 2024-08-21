using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace AvaloniaDemo.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            var topLevel = TopLevel.GetTopLevel(this);
            App.NotificationManager = new WindowNotificationManager(topLevel)
            {
                Position = NotificationPosition.BottomCenter,
                MaxItems = 1
            };
            
            var insetsManager = TopLevel.GetTopLevel(this)?.InsetsManager;
            if (insetsManager is not null)
            {
                insetsManager.IsSystemBarVisible = true;
                insetsManager.SystemBarColor = Colors.Transparent;
            }
        }
    }
}