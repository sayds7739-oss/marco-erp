using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Common
{
    public static class SystemSettingHelpers
    {
        public static async Task<bool> GetBoolAsync(
            ISystemSettingRepository settingRepository,
            string settingKey,
            bool defaultValue = false,
            CancellationToken cancellationToken = default)
        {
            if (settingRepository == null || string.IsNullOrWhiteSpace(settingKey))
                return defaultValue;

            var setting = await settingRepository.GetByKeyAsync(settingKey, cancellationToken);
            if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
                return defaultValue;

            return bool.TryParse(setting.SettingValue, out var value) ? value : defaultValue;
        }
    }
}
