using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections;

namespace InstructionSetEditor
{

    public static class SerializedPropertyExtensions
    {
        // Gets value from SerializedProperty - even if value is nested
        public static object GetValue(this UnityEditor.SerializedProperty property, object obj = null)
        {
            if (property == null || property.serializedObject == null) return null;
            if (obj == null) obj = property.serializedObject.targetObject;

            FieldInfo field = null;

            var tokens = property.propertyPath.Split('.');
            for (var i = 0; i < tokens.Length; i++)
            {
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
                    var arr = (System.Collections.IList)obj;
                    var index = ExtractArrayPathIndex(tokens, i);
                    if (index >= arr.Count) index = arr.Count - 1;
                    if (index < 0) return null;
                    obj = arr[index];
                    i += 2;
                }
            }
            return obj;
        }

        public static IEnumerable<object> GetValues(this UnityEditor.SerializedProperty property)
        {
            if (property == null || property.serializedObject == null || property.serializedObject.targetObjects == null) yield break;
            foreach (var obj in property.serializedObject.targetObjects) yield return GetValue(property, obj);
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
    }


    public static class EditableExtensions
    {

        static BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

































        
        public static string DefaultToEditorContent(this InstructionSet instructionSet, bool highlight = false)
        {
            string result = "";
            string defaultStringField = instructionSet.DefaultStringField();

            foreach (var (field, value, indent) in instructionSet.FunctionIterateInstructions(force: true))
            {
                string valueStr = value.ValueToString();
                if (valueStr == null) continue;
                if (result.Length > 0) result += "\n\n";

                var indentTxt = "";
                for (var i = 0; i < indent; i++) indentTxt += '\t';
                valueStr = valueStr.Replace("\n", $"\n{indentTxt}");

                result += indentTxt;
                if (field == defaultStringField) result += valueStr;
                else
                {
                    if (highlight)
                        result += $"<b>{field} </b>: {valueStr}";
                    else
                        result += $"{field}: {valueStr}";
                }
            }

            if (highlight) result += "\n";
            return result;
        }

        public static void DefaultFromEditorContent(this InstructionSet instructionSet, string editorContent)
        {
            var listMap = instructionSet.GetInstructionListMap();
            var typeMap = instructionSet.GetInstructionFieldMap();
            var defaultStringField = instructionSet.DefaultStringField();

            if (listMap == null) return;

            foreach (var list in listMap.Values) list.Clear();
            instructionSet.instructions.Clear();

            string recent_key = null;
            int recent_indent = 0;

            var lines = editorContent.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().Length <= 0)
                {
                    recent_key = null;
                    continue;
                }
                int indent = 0;
                foreach (char c in line)
                    if (c == '\t') indent++;
                    else break;
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

                if (key == null || typeMap.ContainsKey(key) == false)
                {
                    key = null;
                    value = line.Trim();
                }

                if (value?.Length <= 0) value = null;
                if (key == null && value != null && defaultStringField != null)
                    key = defaultStringField;

                if (key == null || typeMap.ContainsKey(key) == false) continue;

                var valueObject = value.StringToValue(typeMap[key]);
                var list = listMap[key];

                //check for multiple line string default
                if (key == defaultStringField && key == recent_key && indent == recent_indent)
                {
                    var recent_string = (string)list[list.Count - 1];
                    recent_string += "\n" + valueObject;
                    list[list.Count - 1] = recent_string;
                }
                else
                {
                    instructionSet.instructions.Add(new InstructionSet.Instruction()
                    {
                        type = key,
                        valueIndex = list.Count,
                        indent = indent
                    });
                    list.Add(valueObject);
                }

                recent_key = key;
                recent_indent = indent;
            }
        }

        private static string ValueToString(this object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            string valueStr = null;

            if (typeof(Component).IsAssignableFrom(type))
                valueStr = ObjectToAssetPath((UnityEngine.Object)value);
            else if (typeof(GameObject).IsAssignableFrom(type))
                valueStr = ObjectToAssetPath((UnityEngine.Object)value);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                valueStr = ObjectToAssetPath((UnityEngine.Object)value);
            else
                valueStr = value?.ToString();

            return valueStr;
        }

        public static string DefaultStringField(this InstructionSet instructionSet)
        {
            foreach (var field in instructionSet.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                bool defaultAttr = field.GetCustomAttribute<InstructionSet.Default>() != null;
                bool isGenericList = field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>);
                if (defaultAttr && isGenericList && field.FieldType.GetGenericArguments()[0] == typeof(string)) return field.Name;
            }
            return null;
        }

        private static object StringToValue(this string str, Type type)
        {
            try
            {
                if (type == typeof(bool) && string.IsNullOrEmpty(str)) return true;
                else if (type == typeof(bool)) return bool.Parse(str);
                else if (type == typeof(int)) return int.Parse(str);
                else if (type == typeof(float)) return float.Parse(str);
                else if (type == typeof(long)) return long.Parse(str);
                else if (type == typeof(double)) return double.Parse(str);
                else if (type.IsEnum) return System.Enum.Parse(type, str);
                else if (typeof(UnityEngine.GameObject).IsAssignableFrom(type)) return AssetPathToPrefabObject(str);
                else if (typeof(UnityEngine.Component).IsAssignableFrom(type)) return AssetPathToPrefabObject(str, type);
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return AssetPathToObject(str, type);
                else if (type == typeof(string)) return str == null ? "" : str;
                else return null;
            } catch
            {
                return null;
            }
        }


        public static List<string> DefaultGetFieldValueHints(this InstructionSet instructionSet, string fieldName)
        {
            var field = instructionSet.GetType().GetField(fieldName);
            if (field == null) return null;

            var type = field.FieldType;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>)) return null;
            type = type.GetGenericArguments()[0];

            if (type.IsEnum) return type.GetHintsEnum();
            if (typeof(GameObject).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type)) return type.GetHintsPrefab();
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return type.GetHintsUnityObject();

            return null;
        }

        public static Dictionary<string, IList> GetInstructionListMap(this InstructionSet instructionSet)
        {
            Dictionary<string, IList> results = new Dictionary<string, IList>();
            if (instructionSet == null) return results;

            foreach (var field in instructionSet.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = field.FieldType;
                if (typeof(IList).IsAssignableFrom(type))
                {
                    var list = (IList)field.GetValue(instructionSet);
                    if (list == null) list = (IList) Activator.CreateInstance(type);
                    results[field.Name] = list;
                }
            }

            return results;
        }

        public static Dictionary<string, Type> GetInstructionFieldMap(this InstructionSet instructionSet)
        {
            Dictionary<string, Type> results = new Dictionary<string, Type>();

            foreach (var field in instructionSet.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = field.FieldType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) results[field.Name] = type.GetGenericArguments()[0];
            }

            return results;
        }

        public static Dictionary<string, string> GetFieldTypeNameMap(this InstructionSet instructionSet)
        {
            var objectType = instructionSet.GetType();
            var result = new Dictionary<string, string>();

            foreach (var field in instructionSet.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = field.FieldType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) result[field.Name] = type.GetGenericArguments()[0].Name;
            }

            return result;
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
                GameObject asset = (GameObject)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject));
                if (asset == null) continue;
                if (component != null && asset.GetComponent(component) == null) continue;
                results.Add(ObjectToAssetPath(asset));
            }
            return results;
        }

        private static List<string> GetHintsUnityObject(this Type type)
        {
            var collectedAssets = new HashSet<UnityEngine.Object>();
            var results = new List<string>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", type.Name));
            string recent_guids = null;
            for (int i = 0; i < guids.Length; i++)
            {
                if (guids[i] == recent_guids) continue;
                recent_guids = guids[0];
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in assets)
                {
                    if (collectedAssets.Contains(asset)) continue;
                    collectedAssets.Add(asset);
                    if (type.IsAssignableFrom(asset.GetType())) results.Add(ObjectToAssetPath(asset));
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
            var name = match.Groups[1].Value;
            var path = match.Groups[2].Value;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset.name == name) return asset;

            return null;
        }

        static string ObjectToAssetPath(UnityEngine.Object obj)
        {
            if (obj == null) return null;
            if (typeof(Component).IsAssignableFrom(obj.GetType()))
                try
                {
                    obj = ((Component)obj).gameObject;
                }
                catch
                {
                    obj = null;
                }
            if (obj == null) return null;

            var path = AssetDatabase.GetAssetPath(obj);
            if (path == null) return null;
            return $"[{obj.name}]({path})";
        }

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
                while (cursor >= 0 && text[cursor - 1] != ' ' && text[cursor - 1] != '\n' && text[cursor - 1] != '\t' && text[cursor - 1] != ':')
                {
                    cursor--;
                    if (cursor <= 0) break;
                }

            if (endCursor < text.Length - 1)
                while (endCursor < text.Length && text[endCursor + 1] != ' ' && text[endCursor + 1] != '\n' && text[endCursor + 1] != '\t' && text[endCursor + 1] != ':')
                {
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