using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Utils
{
    public static class EnumExtension
    {
        public static object NextEnumValue(this Enum from)
        {
            string[] values = Enum.GetNames(from.GetType());
            string oldName = from.ToString();
            string newName = values[0];
            for (int loop = values.Length - 1; loop >= 0 && oldName != values[loop]; loop--)
            {
                newName = values[loop];
            }
            return Enum.Parse(from.GetType(), newName);
        }

        public static object EnumValue(this Enum from, string str)
        {
            if (string.IsNullOrEmpty(str)) return from;
            try
            {
                return Enum.Parse(from.GetType(), str);
            }
            catch (ArgumentException)
            {
                return from;
            }
        }

    }

    [TestClass]
    public class EnumExtensionTest
    {
        [TestMethod]
        public void TestEnumExtension()
        {
        }
    }
}
