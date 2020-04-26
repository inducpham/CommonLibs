using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace TextEditable
{
    public static class EditableExtensions
    {

        static BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Dictionary<string, string> GetFieldTypeNames(this System.Object textEditable)
        {
            var objectType = textEditable.GetType();
            var result = new Dictionary<string, string>();
            foreach (var field in objectType.GetFields(flags))
            {
                if (FieldValid(field) == false) continue;
                result[field.Name] = field.FieldType.Name;
            }

            return result;
        }

        public static string DefaultStringField(this System.Object textEdiable)
        {
            foreach (var field in textEdiable.GetType().GetFields(flags))
            {
                if (FieldValid(field) == false) continue;
                if (field.GetCustomAttribute<DefaultStringField>() != null && field.FieldType == typeof(string))
                    return field.Name;
            }
            return null;

        }

        public static string DefaultToEditorContent(this System.Object textEditable)
        {
            var objectType = textEditable.GetType();
            var valueMap = new Dictionary<string, object>();
            var defaultStringField = textEditable.DefaultStringField();

            foreach (var field in objectType.GetFields(flags))
            {
                if (FieldValid(field) == false) continue;
                valueMap[field.Name] = field.GetValue(textEditable);
            }

            var results = "";
            string default_string = null;
            foreach (var field in objectType.GetFields(flags))
            {
                var name = field.Name;
                //do something with this huh
                if (valueMap.ContainsKey(name) == false) continue;
                var value = valueMap[name];
                var type = field.FieldType;

                if (type == typeof(System.Boolean) && (bool)value == false) continue;
                if (value == null) continue;

                if (type == typeof(System.Boolean) && (bool)value == true)
                {
                    results += "\n" + field.Name;
                    continue;
                }

                bool fieldHideDefault = field.GetCustomAttribute<HideDefault>() != null;
                if (type.IsEnum && fieldHideDefault && (int)value <= 0)
                    continue;
                if (type == typeof(string) && fieldHideDefault && (value == null || ((string)value).Length <= 0)) continue;

                string valueStr = value.ToString();

                // Check for default string field
                if (field.Name == defaultStringField)
                {
                    default_string = value == null ? null : valueStr.Trim('\n', ' ');
                    continue;
                }

                //PREFAB COMPONENT
                if (typeof(Component).IsAssignableFrom(type))
                    valueStr = ObjectToAssetPath((UnityEngine.Object)value);
                //PREFAB
                else if (typeof(GameObject).IsAssignableFrom(type))
                    valueStr = ObjectToAssetPath((UnityEngine.Object)value);
                //OBJECT OR SCRIPTABLE OBJECT
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    valueStr = ObjectToAssetPath((UnityEngine.Object)value);

                if (valueStr == null)
                    continue;

                if (results.Length > 0) results += "\n";
                results += $"{field.Name}: {valueStr}";
            }

            if (default_string != null)
                results += "\n\n" + default_string;
            return results.Trim('\n', ' ');
        }

        public static void DefaultFromEditorContent(this System.Object textEditable, string editorContent)
        {
            var object_type = textEditable.GetType();
            var fieldNames = new HashSet<string>();
            var valueMap = new Dictionary<string, object>();
            var typeMap = new Dictionary<string, Type>();
            var fieldMap = new Dictionary<string, FieldInfo>();
            var defaultStringField = textEditable.DefaultStringField();
            string defaultString = "";

            foreach (var field in object_type.GetFields(flags))
            {
                if (FieldValid(field) == false) continue;
                fieldNames.Add(field.Name);
                var fieldtype = field.FieldType;
                typeMap[field.Name] = fieldtype;

                valueMap[field.Name] = fieldtype.IsValueType ? Activator.CreateInstance(fieldtype) : null;
                if (field.FieldType == typeof(string)) valueMap[field.Name] = "";

                fieldMap[field.Name] = field;
            }

            var lines = editorContent.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().Length <= 0) continue;

                string key = null;
                string value = null;

                var colon_index = line.IndexOf(':');
                var single_key = line.IndexOf(':') < 0 && line.IndexOf(' ') < 0;
                if (colon_index < 0) key = line;
                else
                {
                    key = line.Substring(0, colon_index).Trim();
                    value = line.Substring(colon_index + 1).Trim();

                    if (key.IndexOf(' ') > 0) key = null;
                    if (key?.Length <= 0) key = null;
                }

                if (key == null || fieldMap.ContainsKey(key) == false)
                {
                    key = null;
                    value = line.Trim();
                }
                if (value?.Length <= 0) value = null;

                if (key == null && value != null && defaultStringField != null)
                    defaultString += "\n" + value;

                if (key == null || typeMap.ContainsKey(key) == false) continue;

                var type = typeMap[key];

                try
                {
                    if (type == typeof(bool) && single_key)
                        valueMap[key] = true;
                    else if (type == typeof(bool))
                        valueMap[key] = bool.Parse(value);
                    else if (type == typeof(int))
                        valueMap[key] = int.Parse(value);
                    else if (type == typeof(float))
                        valueMap[key] = float.Parse(value);
                    else if (type == typeof(long))
                        valueMap[key] = long.Parse(value);
                    else if (type == typeof(double))
                        valueMap[key] = double.Parse(value);
                    else if (type.IsEnum)
                        valueMap[key] = System.Enum.Parse(type, value);
                    else if (typeof(UnityEngine.GameObject).IsAssignableFrom(type))
                        valueMap[key] = AssetPathToPrefabObject(value);
                    else if (typeof(UnityEngine.Component).IsAssignableFrom(type))
                        valueMap[key] = AssetPathToPrefabObject(value, type);
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                        valueMap[key] = AssetPathToObject(value, type);
                    else if (type == typeof(string))
                        valueMap[key] = value == null ? "" : value;
                }
                catch { }

            }

            if (defaultStringField != null)
                valueMap[defaultStringField] = defaultString.Trim('\n', ' ');

            foreach (var fieldname in typeMap.Keys)
            {
                if (valueMap.ContainsKey(fieldname) == false) continue;
                var field = object_type.GetField(fieldname);
                if (field == null) continue;
                try
                {
                    field.SetValue(textEditable, valueMap[fieldname]);
                }
                catch { }
            }
        }


        public static List<string> DefaultGetFieldValueHints(this System.Object textEditable, string fieldName)
        {
            var field = textEditable.GetType().GetField(fieldName);
            if (field == null) return null;
            var type = field.FieldType;
            if (type.IsEnum) return type.GetHintsEnum();
            if (typeof(GameObject).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type)) return type.GetHintsPrefab();
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return type.GetHintsUnityObject();

            return null;            
        }

        private static List<string> GetHintsEnum(this Type type)
        {
            return new List<string>(System.Enum.GetNames(type));
        }

        private static List<string> GetHintsPrefab(this Type type)
        {
            Type component = null;
            if (typeof(Component).IsAssignableFrom(type))
                component = type;

            var results = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:GameObject");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject asset = (GameObject) AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject));
                if (asset == null) continue;
                if (component != null && asset.GetComponent(component) == null) continue;
                results.Add(ObjectToAssetPath(asset));
            }
            return results;
        }

        private static List<string> GetHintsUnityObject(this Type type)
        {
            var results = new List<string>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", type.Name));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
                if (asset != null)
                {
                    results.Add(ObjectToAssetPath(asset));
                }
            }
            return results;
        }

        static Regex FILE_TEMPLATE = new Regex(@"\[([^\[\]]+)\]\(([^\(\)]+)\)");
        static UnityEngine.Object AssetPathToPrefabObject(string fullpath, System.Type component = null)
        {
            var match = FILE_TEMPLATE.Match(fullpath);
            var path = match.Groups[2].Value;

            var result = component != null ? AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path) : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (result == null) return null;

            if (component != null) return ((UnityEngine.GameObject)result).GetComponent(component);
            return result;
        }

        static UnityEngine.Object AssetPathToObject(string fullpath, System.Type component = null)
        {
            var match = FILE_TEMPLATE.Match(fullpath);
            var path = match.Groups[2].Value;
            return AssetDatabase.LoadAssetAtPath(path, component);
        }

        static string ObjectToAssetPath(UnityEngine.Object obj)
        {
            if (obj == null) return null;
            if (typeof(Component).IsAssignableFrom(obj.GetType()))
                try
                {
                    obj = ((Component)obj).gameObject;
                } catch
                {
                    obj = null;
                }
            if (obj == null) return null;

            var path = AssetDatabase.GetAssetPath(obj);
            if (path == null) return null;
            return $"[{obj.name}]({path})";
        }

        static bool FieldValid(FieldInfo fieldInfo)
        {
            var type = fieldInfo.FieldType;
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))) return false;
            return IsUnitySerialized(fieldInfo);
        }

        #region Check field valid
        public static bool IsUnitySerialized(FieldInfo fieldInfo)
        {
            object[] customAttributes = fieldInfo.GetCustomAttributes(true);
            if (customAttributes.Any(x => x is NonSerializedAttribute))
            {
                return false;
            }
            if (fieldInfo.IsPrivate && !customAttributes.Any(x => x is SerializeField))
            {
                return false;
            }

            return IsUnitySerialized(fieldInfo.FieldType);
        }

        public static bool IsUnitySerialized(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return IsUnitySerialized(type.GetGenericArguments()[0]);
                }
                return false;
            }
            if (type.IsEnum)
            {
                return true;
            }
            if (type.IsValueType)
            {
                return true;
            }
            Type typeUnityObject = typeof(UnityEngine.Object);
            if (type.IsAssignableFrom(typeUnityObject))
            {
                return true;
            }
            Type[] typesNative =
            {
             typeof(bool),
             typeof(byte),
             typeof(float),
             typeof(int),
             typeof(string)
         };
            if (typesNative.Contains(type) || (type.IsArray && typesNative.Contains(type.GetElementType())))
            {
                return true;
            }
            return false;
        }
        #endregion

        public static int StartOfHintField(this string line, int cursor)
        {
            while (cursor > 0 && line[cursor - 1] != '\n' && line[cursor - 1] != ' ' && line[cursor - 1] != '\t')
                cursor--;
            return cursor;
        }

        public static int StartOfHintValue(this string line, int cursor)
        {
            while (cursor > 0 && line[cursor - 1] != '\n' && line[cursor - 1] != ' ' && line[cursor - 1] != ':' && line[cursor - 1] != '\t')
                cursor--;
            return cursor;
        }

        public static bool CanHintField(this string line, int cursor)
        {
            var sstr = line.Substring(0, cursor);
            if (Regex.Match(sstr, @"^[\s]*[^\s\s]+[\s]+").Success) return false;
            return Regex.Match(sstr, @"^[\s]*[^\s\:]*$").Success;
        }

        public static bool CanHintValue(this string line, int cursor)
        {
            return Regex.Match(line.Substring(0, cursor), @"^[\s]*[^\s\:]+[\s]*\:[\s]*[^\s\:]*$").Success;
        }

        public static string ExtractCurrentField(this string line)
        {
            var match = (Regex.Match(line, @"^[\s]*[^\s\:]+[\s]*"));
            if (match.Success == false) return "";
            return match.Groups[0].Value.Trim();
        }

        public static (int, int) GetCurrentWordWrap(this string text, int cursor)
        {
            var endCursor = cursor - 1;

            if (cursor > 0)
                while (cursor >= 0 && text[cursor - 1] != ' ' && text[cursor - 1] != '\n' && text[cursor - 1] != '\t' && text[cursor - 1] != ':') {
                    cursor--;
                    if (cursor <= 0) break;
                }

            if (endCursor < text.Length - 1)
                while (endCursor < text.Length && text[endCursor + 1] != ' ' && text[endCursor + 1] != '\n' && text[endCursor + 1] != '\t' && text[endCursor + 1] != ':') {
                    endCursor++;
                    if (endCursor >= text.Length - 1) break;

                }
            return (cursor, endCursor + 1);
        }

        public static (string, int) ExtractCurrentLine(this TextEditor editor)
        {
            var text = editor.text;
            var cursorIndex = editor.cursorIndex;
            var startIndex = cursorIndex;
            var endIndex = cursorIndex;
            while (startIndex > 0 && text[startIndex - 1] != '\n') startIndex--;
            while (endIndex < text.Length && text[endIndex] != '\n') endIndex++;
            return (text.Substring(startIndex, endIndex - startIndex), cursorIndex - startIndex);
        }

        public static bool MatchSearchString(this string str, string search_str)
        {
            var index = 0;

            foreach (var ch in search_str)
            {
                if (ch == ' ' || ch == '\n' || ch == '\t') continue;
                index = str.IndexOf(ch.ToString(), index, StringComparison.InvariantCultureIgnoreCase);
                if (index < 0) return false;
            }

            return true;
        }
    }
}