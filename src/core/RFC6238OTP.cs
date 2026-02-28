using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(challenge);
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

    [TestClass]
    public class RFC6238OTPTests
    {
        // Base32 encoding of ASCII "12345678901234567890" (RFC 6238 Appendix B test secret)
        private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void TestKnownVectors()
        {
            // RFC 6238 Appendix B TOTP-SHA1 test vectors
            Assert.AreEqual("287082", RFC6238OTP.GetVerificationCode(RfcSecret, Epoch.AddSeconds(59)));
            Assert.AreEqual("081804", RFC6238OTP.GetVerificationCode(RfcSecret, Epoch.AddSeconds(1111111109)));
            Assert.AreEqual("005924", RFC6238OTP.GetVerificationCode(RfcSecret, Epoch.AddSeconds(1234567890)));
        }

        [TestMethod]
        public void TestCheckAcceptReject()
        {
            var now = DateTime.UtcNow;
            string code = RFC6238OTP.GetVerificationCode(RfcSecret, now);
            Assert.IsTrue(RFC6238OTP.CheckVerificationCode(RfcSecret, code, now, allowedRange: 0));
            Assert.IsFalse(RFC6238OTP.CheckVerificationCode("AAAAAAAAAAAAAAAA", code, now, allowedRange: 0));
            string oldCode = RFC6238OTP.GetVerificationCode(RfcSecret, now.AddSeconds(-300)); // 10 intervals ago
            Assert.IsFalse(RFC6238OTP.CheckVerificationCode(RfcSecret, oldCode, now, allowedRange: 1));
        }

        [TestMethod]
        public void TestGenerateSecretLength()
        {
            string secret = RFC6238OTP.GenerateSecret(16);
            Assert.AreEqual(16, secret.Length);
            Assert.IsTrue(secret.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c)));
        }
    }
}
