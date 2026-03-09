using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Common;

namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// In-app notification engine. Generates alerts for business events:
    /// low stock, pending invoices, payment reminders, etc.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Gets the latest unread notifications (max 50).</summary>
        Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetUnreadAsync(CancellationToken ct = default);

        /// <summary>Gets all notifications for the current user.</summary>
        Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetAllAsync(int take = 100, CancellationToken ct = default);

        /// <summary>Marks a notification as read.</summary>
        Task<ServiceResult> MarkAsReadAsync(int notificationId, CancellationToken ct = default);

        /// <summary>Marks all notifications as read.</summary>
        Task<ServiceResult> MarkAllAsReadAsync(CancellationToken ct = default);

        /// <summary>Runs all notification rules and generates new alerts.</summary>
        Task<ServiceResult<int>> RefreshAsync(CancellationToken ct = default);

        /// <summary>Gets the count of unread notifications.</summary>
        Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    }
}
