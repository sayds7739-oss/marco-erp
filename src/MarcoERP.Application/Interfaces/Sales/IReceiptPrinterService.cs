using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    public interface IReceiptPrinterService
    {
        Task PrintReceiptAsync(ReceiptDto dto, CancellationToken ct = default);
        bool IsAvailable();
        Task OpenCashDrawerAsync(CancellationToken ct = default);
    }
}
