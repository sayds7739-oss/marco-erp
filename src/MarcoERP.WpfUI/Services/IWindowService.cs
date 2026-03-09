using System.Threading.Tasks;

namespace MarcoERP.WpfUI.Services
{
    public interface IWindowService
    {
        Task ShowMainWindowAsync();

        void OpenPosWindow();

        void LogoutToLogin();
    }
}
