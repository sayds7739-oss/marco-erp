using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;

namespace MarcoERP.Infrastructure.Services
{
    /// <summary>
    /// Windows ESC/POS receipt printer adapter.
    /// Placeholder implementation until a concrete ESC/POS library is integrated.
    /// </summary>
    public sealed class WindowsEscPosPrinterService : IReceiptPrinterService
    {
        public bool IsAvailable()
        {
            // TODO: Detect configured printer and availability.
            return false;
        }

        public Task PrintReceiptAsync(ReceiptDto dto, CancellationToken ct = default)
        {
            // TODO: Integrate ESC/POS library and implement print output.
            return Task.CompletedTask;
        }

        public Task OpenCashDrawerAsync(CancellationToken ct = default)
        {
            // TODO: Integrate drawer open command via ESC/POS.
            return Task.CompletedTask;
        }
    }
}
