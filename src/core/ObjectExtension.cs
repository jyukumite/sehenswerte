using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace SehensWerte.Utils
{
    public static class ObjectExtension
    {
        public static void CopyMembersFrom(this object target, object source)
        {
            Type sourceType = source.GetType();
            Type targetType = target.GetType();
            FieldInfo[] sourceFields = sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo[] sourceProperties = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in sourceFields)
            {
                FieldInfo? targetField = targetType.GetField(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                targetField?.SetValue(target, field.GetValue(source));
            }

            foreach (PropertyInfo property in sourceProperties)
            {
                PropertyInfo? targetProperty = targetType.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetProperty != null && targetProperty.CanWrite)
                {
                    targetProperty.SetValue(target, property.GetValue(source));
                }
            }
        }
    }

    [TestClass]
    public class ObjectExtensionTest
    {
        class SourceClass
        {
            public int IntField;
            public string StringField { get; set; } = "";
            public int Other1;
        }

        class TargetClass
        {
            public int IntField;
            public string StringField { get; set; } = "";
            public int Other2;
        }

        [TestMethod]
        public void TestCopyMembersFrom()
        {
            SourceClass source = new SourceClass
            {
                IntField = 42,
                StringField = "Test"
            };
            TargetClass target = new TargetClass();
            target.CopyMembersFrom(source);
            Assert.AreEqual(source.IntField, target.IntField);
            Assert.AreEqual(source.StringField, target.StringField);
        }
    }
}
