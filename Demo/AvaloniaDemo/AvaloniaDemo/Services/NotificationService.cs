using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace AvaloniaDemo.Services
{
    public class NotificationService
    {
        private static WindowNotificationManager? windowNotificationManager;
        public NotificationPosition Position { get; set; } = NotificationPosition.BottomCenter;
        public int MaxItems { get; set; } = 1;
        public void SetTopLevel(TopLevel? topLevel)
        {
            windowNotificationManager = new WindowNotificationManager(topLevel)
            {
                Position = this.Position,
                MaxItems = this.MaxItems
            };
        }
        public void ShowMessage(string msg, NotificationType type = NotificationType.Information)
        {
            windowNotificationManager?.Show(new Notification()
            {
                Title = "消息",
                Message = msg,
                Type = type
            });
        }
    }
}
