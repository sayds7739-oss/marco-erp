using System;

namespace MarcoERP.Application.DTOs.Common
{
    public sealed class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationLevel Level { get; set; }
        public string Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string ActionRoute { get; set; }

        public string LevelIcon => Level switch
        {
            NotificationLevel.Info => "Information",
            NotificationLevel.Warning => "Alert",
            NotificationLevel.Error => "AlertCircle",
            NotificationLevel.Success => "CheckCircle",
            _ => "Bell"
        };

        public string LevelColor => Level switch
        {
            NotificationLevel.Info => "#1976D2",
            NotificationLevel.Warning => "#F57C00",
            NotificationLevel.Error => "#D32F2F",
            NotificationLevel.Success => "#388E3C",
            _ => "#616161"
        };

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - CreatedAt;
                if (diff.TotalMinutes < 1) return "الآن";
                if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
                if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
                return $"منذ {(int)diff.TotalDays} يوم";
            }
        }
    }

    public enum NotificationLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Success = 3
    }
}
