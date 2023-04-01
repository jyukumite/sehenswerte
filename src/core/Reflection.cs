using System.Reflection;

namespace SehensWerte.Utils
{
    public class Reflect
    {
        public static Dictionary<string, double[]> GetStringDoubleDictionary(Type type)
        {
            var dictionary = new Dictionary<string, double[]>();
            MemberInfo[] members = type.GetMembers(BindingFlags.Static | BindingFlags.Public);
            foreach (MemberInfo memberInfo in members.Where(x => x is FieldInfo))
            {
                object? value = ((FieldInfo)memberInfo).GetValue(null);
                if (value != null && value is double[])
                {
                    dictionary.Add(memberInfo.Name, (double[])value);
                }
            }
            return dictionary;
        }

        public static Dictionary<string, string> GetStringStringDictionary(Type type)
        {
            var dictionary = new Dictionary<string, string>();
            var members = type.GetMembers(BindingFlags.Static | BindingFlags.Public);
            foreach (MemberInfo memberInfo in members.Where(x => x is FieldInfo))
            {
                object? value = ((FieldInfo)memberInfo).GetValue(null);
                if (value is string)
                {
                    dictionary.Add(memberInfo.Name, (string)value);
                }
            }
            return dictionary;
        }
    }
}
