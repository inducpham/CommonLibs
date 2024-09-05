using System;
using System.Collections.Generic;
using System.Reflection;

namespace SSQObject
{
    public static class ScriptableSQObjectTypeExtractor
    {
        public static List<Type> ExtractTypes(this ScriptableSQObject target)
        {
            var target_type = target.GetType();
            //get all types declared inside the target type
            var types = new List<System.Type>(target_type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));

            types.RemoveAll((t) => t.DeclaringType == typeof(ScriptableSQObject));
            return types;
        }
    }
}