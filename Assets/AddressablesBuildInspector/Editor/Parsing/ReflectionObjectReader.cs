using System;
using System.Collections;
using System.Reflection;

namespace AddressablesBuildInspector.Editor.Parsing
{
    internal static class ReflectionObjectReader
    {
        public static string ReadString(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return string.Empty;
            }

            foreach (string memberName in memberNames)
            {
                object value = ReadMember(target, memberName);
                if (value is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        public static long ReadLong(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return 0;
            }

            foreach (string memberName in memberNames)
            {
                object value = ReadMember(target, memberName);
                if (value == null)
                {
                    continue;
                }

                switch (value)
                {
                    case long longValue:
                        return longValue;
                    case int intValue:
                        return intValue;
                    case ulong ulongValue:
                        return unchecked((long)ulongValue);
                    case uint uintValue:
                        return uintValue;
                }
            }

            return 0;
        }

        public static IEnumerable ReadEnumerable(object target, params string[] memberNames)
        {
            if (target == null)
            {
                yield break;
            }

            foreach (string memberName in memberNames)
            {
                object value = ReadMember(target, memberName);
                if (value is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        if (item != null)
                        {
                            yield return item;
                        }
                    }

                    yield break;
                }
            }
        }

        public static object ReadMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(target);
            }

            FieldInfo field = type.GetField(memberName, flags);
            return field?.GetValue(target);
        }
    }
}
