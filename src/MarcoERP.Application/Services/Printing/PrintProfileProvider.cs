using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Services.Printing
{
    /// <summary>
    /// Reads/writes <see cref="PrintProfile"/> from SystemSetting key-value store.
    /// Keys are prefixed with "Print_".
    /// </summary>
    public sealed class PrintProfileProvider : IPrintProfileProvider
    {
        private readonly ISystemSettingRepository _settings;
        private readonly IUnitOfWork _uow;

        public PrintProfileProvider(ISystemSettingRepository settings, IUnitOfWork uow)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public async Task<PrintProfile> GetProfileAsync(CancellationToken ct = default)
        {
            var profile = new PrintProfile();

            profile.CompanyName = await ReadAsync("CompanyName", profile.CompanyName, ct);
            profile.CompanyNameEn = await ReadAsync("CompanyNameEn", profile.CompanyNameEn, ct);
            profile.CompanyAddress = await ReadAsync("CompanyAddress", profile.CompanyAddress, ct);
            profile.CompanyPhone = await ReadAsync("CompanyPhone", profile.CompanyPhone, ct);
            profile.CompanyEmail = await ReadAsync("CompanyEmail", profile.CompanyEmail, ct);
            profile.CompanyTaxNumber = await ReadAsync("CompanyTaxNumber", profile.CompanyTaxNumber, ct);
            profile.CompanyLogoBase64 = await ReadAsync("CompanyLogo", profile.CompanyLogoBase64, ct);

            profile.PrimaryColor = await ReadAsync("Print_PrimaryColor", profile.PrimaryColor, ct);
            profile.HeaderBgColor = await ReadAsync("Print_HeaderBgColor", profile.HeaderBgColor, ct);
            profile.BorderColor = await ReadAsync("Print_BorderColor", profile.BorderColor, ct);
            profile.TextColor = await ReadAsync("Print_TextColor", profile.TextColor, ct);
            profile.SubtitleColor = await ReadAsync("Print_SubtitleColor", profile.SubtitleColor, ct);
            profile.FontFamily = await ReadAsync("Print_FontFamily", profile.FontFamily, ct);
            profile.TitleFontSize = int.TryParse(await ReadAsync("Print_TitleFontSize", "", ct), out var ts) ? ts : profile.TitleFontSize;
            profile.BodyFontSize = int.TryParse(await ReadAsync("Print_BodyFontSize", "", ct), out var bs) ? bs : profile.BodyFontSize;

            profile.ShowLogo = await ReadBoolAsync("Print_ShowLogo", profile.ShowLogo, ct);
            profile.ShowCompanyName = await ReadBoolAsync("Print_ShowCompanyName", profile.ShowCompanyName, ct);
            profile.ShowAddress = await ReadBoolAsync("Print_ShowAddress", profile.ShowAddress, ct);
            profile.ShowContact = await ReadBoolAsync("Print_ShowContact", profile.ShowContact, ct);
            profile.ShowTaxNumber = await ReadBoolAsync("Print_ShowTaxNumber", profile.ShowTaxNumber, ct);
            profile.ShowFooter = await ReadBoolAsync("Print_ShowFooter", profile.ShowFooter, ct);
            profile.FooterText = await ReadAsync("Print_FooterText", profile.FooterText, ct);

            profile.CustomHeaderHtml = await ReadAsync("Print_CustomHeaderHtml", profile.CustomHeaderHtml, ct);
            profile.CustomFooterHtml = await ReadAsync("Print_CustomFooterHtml", profile.CustomFooterHtml, ct);

            var sectionOrderRaw = await ReadAsync("Print_SectionOrder", "", ct);
            if (!string.IsNullOrWhiteSpace(sectionOrderRaw))
                profile.SectionOrder = new System.Collections.Generic.List<string>(sectionOrderRaw.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var hiddenColumnsRaw = await ReadAsync("Print_HiddenColumns", "", ct);
            if (!string.IsNullOrWhiteSpace(hiddenColumnsRaw))
                profile.HiddenColumns = new System.Collections.Generic.List<string>(hiddenColumnsRaw.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            return profile;
        }

        public async Task SaveProfileAsync(PrintProfile profile, CancellationToken ct = default)
        {
            await WriteAsync("Print_PrimaryColor", profile.PrimaryColor, ct);
            await WriteAsync("Print_HeaderBgColor", profile.HeaderBgColor, ct);
            await WriteAsync("Print_BorderColor", profile.BorderColor, ct);
            await WriteAsync("Print_TextColor", profile.TextColor, ct);
            await WriteAsync("Print_SubtitleColor", profile.SubtitleColor, ct);
            await WriteAsync("Print_FontFamily", profile.FontFamily, ct);
            await WriteAsync("Print_TitleFontSize", profile.TitleFontSize.ToString(), ct);
            await WriteAsync("Print_BodyFontSize", profile.BodyFontSize.ToString(), ct);
            await WriteAsync("Print_ShowLogo", profile.ShowLogo.ToString(), ct);
            await WriteAsync("Print_ShowCompanyName", profile.ShowCompanyName.ToString(), ct);
            await WriteAsync("Print_ShowAddress", profile.ShowAddress.ToString(), ct);
            await WriteAsync("Print_ShowContact", profile.ShowContact.ToString(), ct);
            await WriteAsync("Print_ShowTaxNumber", profile.ShowTaxNumber.ToString(), ct);
            await WriteAsync("Print_ShowFooter", profile.ShowFooter.ToString(), ct);
            await WriteAsync("Print_FooterText", profile.FooterText, ct);

            await WriteAsync("Print_CustomHeaderHtml", profile.CustomHeaderHtml ?? "", ct);
            await WriteAsync("Print_CustomFooterHtml", profile.CustomFooterHtml ?? "", ct);
            await WriteAsync("Print_SectionOrder", profile.SectionOrder != null ? string.Join(",", profile.SectionOrder) : "", ct);
            await WriteAsync("Print_HiddenColumns", profile.HiddenColumns != null ? string.Join(",", profile.HiddenColumns) : "", ct);

            await _uow.SaveChangesAsync(ct);
        }

        private async Task<string> ReadAsync(string key, string fallback, CancellationToken ct)
        {
            var setting = await _settings.GetByKeyAsync(key, ct);
            return setting != null && !string.IsNullOrEmpty(setting.SettingValue)
                ? setting.SettingValue
                : fallback;
        }

        private async Task<bool> ReadBoolAsync(string key, bool fallback, CancellationToken ct)
        {
            var val = await ReadAsync(key, "", ct);
            return string.IsNullOrEmpty(val) ? fallback : bool.TryParse(val, out var b) && b;
        }

        private async Task WriteAsync(string key, string value, CancellationToken ct)
        {
            var setting = await _settings.GetByKeyAsync(key, ct);
            if (setting != null)
            {
                setting.UpdateValue(value);
            }
            else
            {
                var newSetting = new MarcoERP.Domain.Entities.Settings.SystemSetting(
                    key, value, key, "إعدادات الطباعة", "string");
                await _settings.AddAsync(newSetting, ct);
            }
        }
    }
}
