using SehensWerte.Utils;
using System.Net;
using System.Security.Cryptography;

namespace SehensWerte
{
    public class RFC6238OTP
    {
        // rfc6238 Time-Based One-Time Password Algorithm (google authenticator compatible)
        // QR: qrencode -o - -t ANSI otpauth://totp/Example:alice@google.com?secret=ABCD2345IJKL67OP&issuer=Example
        // secret is base 32 (16 characters from A-Z, 2-7)

        private const int IntervalSeconds = 30;

        public static string GenerateSecret(int length = 16)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                var result = new char[length];
                for (int loop = 0; loop < result.Length; loop++)
                {
                    var random = new byte[1];
                    rng.GetBytes(random);
                    result[loop] = FormatBits.Base32Chars[random[0] & 31];
                }
                return new String(result);
            }
        }

        private static ulong Index(DateTime dateTime)
        {
            TimeSpan since = dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (ulong)(since.TotalSeconds / IntervalSeconds);
        }

        public static string GetVerificationCode(string secret, DateTime date)
        {
            ulong index = Index(date);
            byte[] challenge = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)index));
            var key = FormatBits.FromBase32(secret);
            var hash = new HMACSHA1(key).ComputeHash(challenge);
            int offset = hash[hash.Length - 1] & 0xf;
            int code = 0;
            for (int loop = 0; loop < 4; loop++)
            {
                code = (code << 8) | hash[offset + loop];
            }
            return ((code & 0x7fffffff) % 1000000).ToString().PadLeft(6, '0');
        }

        public static bool CheckVerificationCode(string secret, string code, DateTime now, int allowedRange = 2)
        {
            for (int loop = -allowedRange; loop <= allowedRange; loop++)
            {
                var check = now.AddSeconds(IntervalSeconds * loop);
                if (GetVerificationCode(secret, check).CompareConstantTime(code))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
