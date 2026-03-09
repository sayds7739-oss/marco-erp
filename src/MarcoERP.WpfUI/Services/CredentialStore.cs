using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarcoERP.WpfUI.Services
{
    /// <summary>
    /// تخزين بيانات تسجيل الدخول محلياً بتشفير DPAPI مرتبط بالمستخدم الحالي.
    /// يستخدم لميزة "تذكرني" في شاشة تسجيل الدخول.
    /// </summary>
    internal sealed class SavedCredential
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    internal static class CredentialStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarcoERP", "credentials.dat");

        /// <summary>حفظ بيانات الدخول مشفّرة على القرص.</summary>
        public static void Save(string username, string password)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(new SavedCredential
                {
                    Username = username,
                    Password = password
                });

                var encrypted = Encrypt(json);
                File.WriteAllBytes(FilePath, encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CredentialStore.Save error: {ex.Message}");
            }
        }

        /// <summary>قراءة بيانات الدخول المحفوظة. يعيد null إن لم توجد.</summary>
        public static SavedCredential Load()
        {
            if (!File.Exists(FilePath))
                return null;

            try
            {
                var encrypted = File.ReadAllBytes(FilePath);
                var json = Decrypt(encrypted);
                return JsonSerializer.Deserialize<SavedCredential>(json);
            }
            catch
            {
                Clear();
                return null;
            }
        }

        /// <summary>حذف بيانات الدخول المحفوظة.</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch { /* ignore */ }
        }

        #region DPAPI Encryption

        private static byte[] Encrypt(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        }

        private static string Decrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new InvalidOperationException("بيانات الاعتماد غير صالحة.");

            var plainBytes = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }

        #endregion
    }
}
