using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;

namespace MarcoERP.API.Services;

/// <summary>
/// No-op implementation of IAlertService for the API layer.
/// The alert system is only used by the WPF background job scheduler.
/// </summary>
public class ApiNoOpAlertService : IAlertService
{
    public IReadOnlyList<AlertItem> ActiveAlerts => Array.Empty<AlertItem>();
    public event EventHandler? AlertsChanged
    {
        add { }
        remove { }
    }
    public void AddAlert(string message, string category, AlertSeverity severity) { }
    public void ClearAlerts(string category) { }
    public void ClearAll() { }
}

/// <summary>
/// No-op implementation of IReceiptPrinterService for the API layer.
/// Physical receipt printing is only relevant on the desktop.
/// </summary>
public class ApiNoOpReceiptPrinterService : IReceiptPrinterService
{
    public Task PrintReceiptAsync(ReceiptDto dto, CancellationToken ct = default)
        => Task.CompletedTask;

    public bool IsAvailable() => false;

    public Task OpenCashDrawerAsync(CancellationToken ct = default)
        => Task.CompletedTask;
}
