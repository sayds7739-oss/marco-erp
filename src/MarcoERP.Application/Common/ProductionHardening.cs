using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Common
{
    public static class ProductionHardening
    {
        public const string ProductionModeSettingKey = "IsProductionMode";

        public static async Task<bool> IsProductionModeAsync(
            ISystemSettingRepository settingRepository,
            CancellationToken cancellationToken = default)
        {
            if (settingRepository == null)
                return true;

            var setting = await settingRepository.GetByKeyAsync(ProductionModeSettingKey, cancellationToken);
            if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
                return true;

            return bool.TryParse(setting.SettingValue, out var value) ? value : true;
        }

        /// <summary>
        /// Checks whether the posting date is in the past relative to a reference "now" date.
        /// IMPORTANT: Both <paramref name="postingDate"/> and <paramref name="now"/> must be
        /// in the same timezone (both local or both UTC). Posting dates are typically entered
        /// in local time, so callers should pass local DateTime.Now, not UtcNow.
        /// </summary>
        public static bool IsBackdated(DateTime postingDate, DateTime now)
            => postingDate.Date < now.Date;

        /// <summary>
        /// Convenience overload using local DateTime.Now.
        /// DEPRECATED: Use the two-parameter overload with IDateTimeProvider for testability.
        /// </summary>
        [Obsolete("Use IsBackdated(DateTime postingDate, DateTime now) with IDateTimeProvider for testable code")]
        public static bool IsBackdated(DateTime postingDate)
            => postingDate.Date < DateTime.Now.Date;
    }
}