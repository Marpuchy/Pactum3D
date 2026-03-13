using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveSystem
{
    public class SaveSystem : ISaveSystem
    {
        private const int CurrentSchemaVersion = 1;
        private const int AesBlockSizeBytes = 16;
        private const int KeySizeBytes = 32;
        private const int Pbkdf2Iterations = 100000;

        private readonly string saveDirectory;
        private readonly byte[] encryptionKey;
        private readonly byte[] signatureKey;

        public SaveSystem()
        {
            saveDirectory = Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(saveDirectory);

            byte[] salt = BuildSalt();
            string passphrase = BuildPassphrase();

            using (var deriveBytes = new Rfc2898DeriveBytes(passphrase, salt, Pbkdf2Iterations))
            {
                encryptionKey = deriveBytes.GetBytes(KeySizeBytes);
                signatureKey = deriveBytes.GetBytes(KeySizeBytes);
            }
        }

        public void Save<TState>(string slot, TState state) where TState : struct
        {
            string path = GetSlotPath(slot);
            Directory.CreateDirectory(saveDirectory);

            var wrapper = new StateWrapper<TState> { Value = state };
            string payloadJson = JsonUtility.ToJson(wrapper);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            byte[] iv = GenerateRandomBytes(AesBlockSizeBytes);
            byte[] cipherBytes = Encrypt(payloadBytes, iv);
            byte[] signature = ComputeSignature(iv, cipherBytes);

            var envelope = new EncryptedSaveEnvelope
            {
                SchemaVersion = CurrentSchemaVersion,
                StateType = GetStateTypeName<TState>(),
                SavedAtUtc = DateTime.UtcNow.ToString("O"),
                Iv = Convert.ToBase64String(iv),
                CipherText = Convert.ToBase64String(cipherBytes),
                Signature = Convert.ToBase64String(signature)
            };

            string envelopeJson = JsonUtility.ToJson(envelope, false);
            WriteAtomic(path, envelopeJson);
        }

        public bool TryLoad<TState>(string slot, out TState state) where TState : struct
        {
            state = default;

            string path = GetSlotPath(slot);
            if (!File.Exists(path))
                return false;

            try
            {
                string envelopeJson = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(envelopeJson))
                    return false;

                EncryptedSaveEnvelope envelope = JsonUtility.FromJson<EncryptedSaveEnvelope>(envelopeJson);
                if (!IsValidEnvelope(envelope))
                    return false;

                if (envelope.SchemaVersion != CurrentSchemaVersion)
                {
                    Debug.LogWarning($"SaveSystem: unsupported schema version {envelope.SchemaVersion} in slot '{slot}'.");
                    return false;
                }

                string expectedType = GetStateTypeName<TState>();
                if (!string.Equals(envelope.StateType, expectedType, StringComparison.Ordinal))
                {
                    Debug.LogWarning($"SaveSystem: slot '{slot}' does not contain '{expectedType}' data.");
                    return false;
                }

                byte[] iv = Convert.FromBase64String(envelope.Iv);
                byte[] cipherBytes = Convert.FromBase64String(envelope.CipherText);
                byte[] signature = Convert.FromBase64String(envelope.Signature);

                byte[] computedSignature = ComputeSignature(iv, cipherBytes);
                if (!SecureEquals(signature, computedSignature))
                {
                    Debug.LogWarning($"SaveSystem: signature mismatch in slot '{slot}'.");
                    return false;
                }

                byte[] payloadBytes = Decrypt(cipherBytes, iv);
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);

                StateWrapper<TState> wrapper = JsonUtility.FromJson<StateWrapper<TState>>(payloadJson);
                if (wrapper == null)
                    return false;

                state = wrapper.Value;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SaveSystem: failed loading slot '{slot}'. {ex.Message}");
                return false;
            }
        }

        public bool Exists(string slot)
        {
            return File.Exists(GetSlotPath(slot));
        }

        public bool Delete(string slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            string backupPath = path + ".bak";
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            return true;
        }

        private static string GetStateTypeName<TState>() where TState : struct
        {
            Type stateType = typeof(TState);
            return stateType.AssemblyQualifiedName ?? stateType.FullName ?? stateType.Name;
        }

        private static bool IsValidEnvelope(EncryptedSaveEnvelope envelope)
        {
            return envelope != null &&
                   !string.IsNullOrWhiteSpace(envelope.Iv) &&
                   !string.IsNullOrWhiteSpace(envelope.CipherText) &&
                   !string.IsNullOrWhiteSpace(envelope.Signature) &&
                   !string.IsNullOrWhiteSpace(envelope.StateType);
        }

        private byte[] Encrypt(byte[] plainBytes, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySizeBytes * 8;
                aes.BlockSize = AesBlockSizeBytes * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }
        }

        private byte[] Decrypt(byte[] cipherBytes, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySizeBytes * 8;
                aes.BlockSize = AesBlockSizeBytes * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }
            }
        }

        private byte[] ComputeSignature(byte[] iv, byte[] cipherBytes)
        {
            byte[] data = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, data, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, data, iv.Length, cipherBytes.Length);

            using (var hmac = new HMACSHA256(signatureKey))
            {
                return hmac.ComputeHash(data);
            }
        }

        private static byte[] BuildSalt()
        {
            string baseSalt = $"{Application.companyName}|{Application.productName}|{Application.identifier}|SaveSystem.v1";
            byte[] saltBytes = Encoding.UTF8.GetBytes(baseSalt);
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(saltBytes);
            }
        }

        private static string BuildPassphrase()
        {
            return $"{Application.companyName}:{Application.productName}:{Application.identifier}:encrypted-json";
        }

        private string GetSlotPath(string slot)
        {
            string safeSlot = SanitizeSlot(slot);
            return Path.Combine(saveDirectory, $"{safeSlot}.save.json");
        }

        private static string SanitizeSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("Slot name cannot be null or empty.", nameof(slot));

            var builder = new StringBuilder(slot.Length);
            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < slot.Length; i++)
            {
                char c = slot[i];
                if (Array.IndexOf(invalidChars, c) >= 0 || c == '/' || c == '\\')
                    builder.Append('_');
                else
                    builder.Append(c);
            }

            string sanitized = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "slot";

            return sanitized;
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return bytes;
        }

        private static bool SecureEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }

        private static void WriteAtomic(string path, string content)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            File.WriteAllText(tempPath, content, Encoding.UTF8);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
            catch (IOException)
            {
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
        }

        [Serializable]
        private sealed class StateWrapper<TState> where TState : struct
        {
            public TState Value;
        }

        [Serializable]
        private sealed class EncryptedSaveEnvelope
        {
            public int SchemaVersion;
            public string StateType;
            public string SavedAtUtc;
            public string Iv;
            public string CipherText;
            public string Signature;
        }
    }
}
