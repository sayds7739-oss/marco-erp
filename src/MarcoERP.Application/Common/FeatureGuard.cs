using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Static helper for checking feature availability before executing operations.
    /// Phase 2: Feature Governance Engine — Wired into all main Application services.
    /// Each service calls CheckAsync at the entry point of create/update operations.
    /// </summary>
    public static class FeatureGuard
    {
        private const string FeatureDisabledMsg = "هذه الميزة معطلة حاليًا: {0}";

        /// <summary>
        /// Checks if a feature is enabled. Returns a failed ServiceResult if disabled; null if enabled.
        /// Usage: var guard = await FeatureGuard.CheckAsync(featureService, "AdvancedAccounting", ct);
        ///        if (guard != null) return guard;
        /// </summary>
        public static async Task<ServiceResult> CheckAsync(
            IFeatureService featureService,
            string featureKey,
            CancellationToken ct = default)
        {
            var result = await featureService.IsEnabledAsync(featureKey, ct);

            // If feature not found or DB error, block the operation (fail-closed for security)
            if (result.IsFailure)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FeatureGuard] فشل التحقق من الميزة '{featureKey}': {result.ErrorMessage}. تم حظر العملية.");
                return ServiceResult.Failure("تعذر التحقق من حالة الميزة. يرجى المحاولة لاحقاً.");
            }

            if (!result.Data)
                return ServiceResult.Failure(string.Format(FeatureDisabledMsg, featureKey));

            return null; // enabled — caller proceeds
        }

        /// <summary>
        /// Generic version for services returning ServiceResult{T}.
        /// </summary>
        public static async Task<ServiceResult<T>> CheckAsync<T>(
            IFeatureService featureService,
            string featureKey,
            CancellationToken ct = default)
        {
            var result = await featureService.IsEnabledAsync(featureKey, ct);

            // If feature not found or DB error, block the operation (fail-closed for security)
            if (result.IsFailure)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FeatureGuard] فشل التحقق من الميزة '{featureKey}': {result.ErrorMessage}. تم حظر العملية.");
                return ServiceResult<T>.Failure("تعذر التحقق من حالة الميزة. يرجى المحاولة لاحقاً.");
            }

            if (!result.Data)
                return ServiceResult<T>.Failure(string.Format(FeatureDisabledMsg, featureKey));

            return null; // enabled — caller proceeds
        }
    }
}
