using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BinancePulse.Services
{
    /// <summary>
    /// Сервис для шифрования и дешифрования строк с использованием Windows DPAPI.
    /// </summary>
    public static class EncryptionService
    {
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes ("BinancePulse-Salt-2024");

        /// <summary>
        /// Шифрует строку с использованием DPAPI.
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty (plainText))
                return plainText;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes (plainText);
                byte[] encryptedBytes = ProtectedData.Protect (plainBytes, _entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String (encryptedBytes);
            }
            catch
            {
                // Если шифрование не удалось, возвращаем исходную строку (например, при отсутствии прав)
                return plainText;
            }
        }

        /// <summary>
        /// Дешифрует строку с использованием DPAPI.
        /// </summary>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty (encryptedText))
                return encryptedText;

            // Если строка не выглядит как Base64, считаем, что она не зашифрована
            if (!IsBase64String (encryptedText))
                return encryptedText;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String (encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect (encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString (plainBytes);
            }
            catch
            {
                // Если расшифровка не удалась, возвращаем как есть (возможно, уже открытый текст)
                return encryptedText;
            }
        }

        private static bool IsBase64String(string base64)
        {
            Span<byte> buffer = new Span<byte> (new byte[base64.Length]);
            return Convert.TryFromBase64String (base64, buffer, out _);
        }
    }
}