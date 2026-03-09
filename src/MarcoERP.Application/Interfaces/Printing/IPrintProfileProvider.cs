using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Printing;

namespace MarcoERP.Application.Interfaces.Printing
{
    /// <summary>
    /// Provides the company print profile (logo, name, colors, fonts, etc.)
    /// used by all document templates.
    /// </summary>
    public interface IPrintProfileProvider
    {
        /// <summary>Loads the current print profile from system settings.</summary>
        Task<PrintProfile> GetProfileAsync(CancellationToken ct = default);

        /// <summary>Saves updated print profile to system settings.</summary>
        Task SaveProfileAsync(PrintProfile profile, CancellationToken ct = default);
    }
}
