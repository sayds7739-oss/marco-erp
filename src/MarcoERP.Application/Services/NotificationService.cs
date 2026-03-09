using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Treasury;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services
{
    public sealed class NotificationService : INotificationService
    {
        private readonly ConcurrentDictionary<int, NotificationDto> _notifications = new();
        private int _nextId;
        private readonly IProductService _productService;
        private readonly ICashboxService _cashboxService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IProductService productService = null,
            ICashboxService cashboxService = null,
            ILogger<NotificationService> logger = null)
        {
            _productService = productService;
            _cashboxService = cashboxService;
            _logger = logger;
        }

        public Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetUnreadAsync(CancellationToken ct = default)
        {
            var result = _notifications.Values
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToList();
            return Task.FromResult(ServiceResult<IReadOnlyList<NotificationDto>>.Success(result));
        }

        public Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetAllAsync(int take = 100, CancellationToken ct = default)
        {
            var result = _notifications.Values
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToList();
            return Task.FromResult(ServiceResult<IReadOnlyList<NotificationDto>>.Success(result));
        }

        public Task<ServiceResult> MarkAsReadAsync(int notificationId, CancellationToken ct = default)
        {
            if (_notifications.TryGetValue(notificationId, out var n))
                n.IsRead = true;
            return Task.FromResult(ServiceResult.Success());
        }

        public Task<ServiceResult> MarkAllAsReadAsync(CancellationToken ct = default)
        {
            foreach (var n in _notifications.Values)
                n.IsRead = true;
            return Task.FromResult(ServiceResult.Success());
        }

        public async Task<ServiceResult<int>> RefreshAsync(CancellationToken ct = default)
        {
            int generated = 0;
            try
            {
                generated += await CheckLowStockAsync(ct);
                generated += await CheckLowCashboxAsync(ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Notification refresh partial failure");
            }
            return ServiceResult<int>.Success(generated);
        }

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_notifications.Values.Count(n => !n.IsRead));
        }

        private async Task<int> CheckLowStockAsync(CancellationToken ct)
        {
            if (_productService == null) return 0;
            int count = 0;
            try
            {
                var result = await _productService.SearchAsync("", ct);
                if (!result.IsSuccess || result.Data == null) return 0;

                foreach (var p in result.Data)
                {
                    if (p.TotalStock <= 0 || (p.TotalStock > 0 && p.TotalStock <= 5))
                    {
                        var existing = _notifications.Values.FirstOrDefault(
                            n => n.Category == "LowStock" && n.ActionRoute == $"Product:{p.Id}" && !n.IsRead);
                        if (existing != null) continue;

                        AddNotification(new NotificationDto
                        {
                            Title = "مخزون منخفض",
                            Message = $"الصنف «{p.NameAr}» — الرصيد الحالي: {p.TotalStock:N0}",
                            Level = p.TotalStock <= 0 ? NotificationLevel.Error : NotificationLevel.Warning,
                            Category = "LowStock",
                            ActionRoute = $"Product:{p.Id}"
                        });
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Low stock check failed");
            }
            return count;
        }

        private async Task<int> CheckLowCashboxAsync(CancellationToken ct)
        {
            if (_cashboxService == null) return 0;
            int count = 0;
            try
            {
                var result = await _cashboxService.GetAllAsync(ct);
                if (!result.IsSuccess || result.Data == null) return 0;

                foreach (var cb in result.Data)
                {
                    if (cb.Balance < 0)
                    {
                        var existing = _notifications.Values.FirstOrDefault(
                            n => n.Category == "NegativeCashbox" && n.ActionRoute == $"Cashbox:{cb.Id}" && !n.IsRead);
                        if (existing != null) continue;

                        AddNotification(new NotificationDto
                        {
                            Title = "رصيد صندوق سالب",
                            Message = $"الصندوق «{cb.NameAr}» رصيده سالب: {cb.Balance:N2}",
                            Level = NotificationLevel.Error,
                            Category = "NegativeCashbox",
                            ActionRoute = $"Cashbox:{cb.Id}"
                        });
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Low cashbox check failed");
            }
            return count;
        }

        private void AddNotification(NotificationDto dto)
        {
            dto.Id = Interlocked.Increment(ref _nextId);
            dto.CreatedAt = DateTime.Now;
            _notifications[dto.Id] = dto;

            // Keep only last 200 notifications
            if (_notifications.Count > 200)
            {
                var oldest = _notifications.Values.OrderBy(n => n.CreatedAt).Take(50);
                foreach (var old in oldest)
                    _notifications.TryRemove(old.Id, out _);
            }
        }
    }
}
