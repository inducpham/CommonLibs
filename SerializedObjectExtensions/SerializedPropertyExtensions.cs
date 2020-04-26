using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class MyExtensions
{
#if UNITY_EDITOR
    // Gets value from SerializedProperty - even if value is nested
    public static object GetValue(this UnityEditor.SerializedProperty property)
    {
        if (property == null || property.serializedObject == null) return null;
        object obj = property.serializedObject.targetObject;

        FieldInfo field = null;

        var tokens = property.propertyPath.Split('.');
        for (var i = 0; i < tokens.Length; i++) {
            var path = tokens[i];
            var type = obj.GetType();
            field = type.GetField(path);
            obj = field.GetValue(obj);
            if (obj == null) return null;

            type = obj.GetType();
            if (type.IsArray)
            {
                var arr = (object[])obj;
                var index = ExtractArrayPathIndex(tokens, i);
                if (index >= arr.Length) index = arr.Length - 1;
                obj = arr[index];
                i += 2;
            }

            if (typeof(System.Collections.IList).IsAssignableFrom(type))
            {                
                var arr = (System.Collections.IList) obj;
                var index = ExtractArrayPathIndex(tokens, i);
                if (index >= arr.Count) index = arr.Count - 1;
                if (index < 0) return null;
                obj = arr[index];
                i += 2;
            }
        }
        return obj;
    }

    static int ExtractArrayPathIndex(string[] tokens, int currentToken)
    {
        if (currentToken >= tokens.Length - 2) return -1; //not enough positions
        var tok_arr = tokens[currentToken + 1];
        if (tok_arr != "Array") return -1;
        var tok_index = tokens[currentToken + 2];
        var match = Regex.Match(tok_index, @"^data\[(\d+)\]$");
        if (match.Success == false) return -1;
        return int.Parse(match.Groups[1].Value);
    }

    // Sets value from SerializedProperty - even if value is nested
    public static void SetValue(this UnityEditor.SerializedProperty property, object val)
    {
        object obj = property.serializedObject.targetObject;

        List<KeyValuePair<FieldInfo, object>> list = new List<KeyValuePair<FieldInfo, object>>();

        FieldInfo field = null;
        foreach (var path in property.propertyPath.Split('.'))
        {
            var type = obj.GetType();
            field = type.GetField(path);
            list.Add(new KeyValuePair<FieldInfo, object>(field, obj));
            obj = field.GetValue(obj);
        }

        // Now set values of all objects, from child to parent
        for (int i = list.Count - 1; i >= 0; --i)
        {
            list[i].Key.SetValue(list[i].Value, val);
            // New 'val' object will be parent of current 'val' object
            val = list[i].Value;
        }
    }
#endif // UNITY_EDITOR
}