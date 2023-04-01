using System.Security.Cryptography;
using System.Text;

namespace SehensWerte.Utils
{
    public class AES
    {
        public static byte[]? Encrypt(byte[] plain, byte[] key32B, byte[] iv16B)
        {
            using MemoryStream ms = new MemoryStream();
            using (Aes aes = Aes.Create())
            {
                aes.Key = key32B;
                aes.IV = iv16B;
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    try
                    {
                        cs.Write(plain, 0, plain.Length);
                    }
                    catch
                    {
                        return null;
                    }
                    cs.Close();
                    return ms.ToArray();
                }
            }
        }

        public static byte[]? Decrypt(byte[] cipher, byte[] key32B, byte[] iv16B)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                using (Aes val = Aes.Create())
                {
                    val.Key = key32B;
                    val.IV = iv16B;
                    using (CryptoStream cs = new CryptoStream(ms, val.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipher, 0, cipher.Length);
                        cs.Close();
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string EncryptString(string data, string pw)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            byte[] key32B = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(pw + "-SehensWerte"));
            byte[] iv16B = RandomNumberGenerator.GetBytes(16);
            byte[] cypher = Encrypt(bytes, key32B, iv16B) ?? new byte[0];
            byte[] result = iv16B.Concat(cypher).ToArray();
            return Convert.ToBase64String(result);
        }

        public static string DecryptString(string data, string pw)
        {
            byte[] key32B = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(pw + "-SehensWerte"));
            byte[] bytes = Convert.FromBase64String(data);
            byte[] iv16B = bytes.Take(16).ToArray();
            byte[] cypher = bytes.Skip(16).ToArray();
            byte[] result = Decrypt(cypher, key32B, iv16B) ?? new byte[0];
            return Encoding.Default.GetString(result);
        }
    }
}
