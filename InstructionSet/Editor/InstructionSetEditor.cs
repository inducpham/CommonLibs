using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InstructionSetEditor {

    [CustomPropertyDrawer(typeof(InstructionSet), true)]
    public class InstructionSetEditorPropertyDrawer : PropertyDrawer
    {

        static bool selectionChangedMapped = false;
        static Dictionary<string, string> propertyContentMap = new Dictionary<string, string>();
        static Dictionary<string, float> propertyWidthMap = new Dictionary<string, float>();
        static GUIStyle style;

        static GUIStyle GetStyle()
        {
            if (style != null) return style;
            style = new GUIStyle(EditorStyles.helpBox);
            style.fontSize = 11;
            style.richText = true;
            style.padding = new RectOffset(6, 6, 6, 6);
            //style.normal.textColor = new Color(0.2f, 0.2f, 0.2f); // Color.black;
            return style;
        }

        public static void RemapSerializedPropertyContent(SerializedProperty property)
        {
            var content = "-";
            var obj = (InstructionSet) property.GetValue();
            if (obj == null) return;
            content = obj.DefaultToEditorContent(highlight: true);
            propertyContentMap[property.propertyPath] = content;
            property.serializedObject.ApplyModifiedProperties();

            foreach (var o in property.serializedObject.targetObjects) EditorUtility.SetDirty(o);
        }

        static string MapSerializedPropertyContent(SerializedProperty property)
        {
            if (!selectionChangedMapped)
            {
                Selection.selectionChanged += () => propertyContentMap.Clear();
                selectionChangedMapped = true;
            }

            if (propertyContentMap.ContainsKey(property.propertyPath) == false)
            {
                var content = "-";

                var obj = (InstructionSet) property.GetValue();
                if (obj == null) return "";
                content = obj.DefaultToEditorContent(highlight: true);

                propertyContentMap[property.propertyPath] = content;
            }

            return propertyContentMap[property.propertyPath];
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var viewWidth = propertyWidthMap.ContainsKey(property.propertyPath) ? propertyWidthMap[property.propertyPath] : EditorGUIUtility.currentViewWidth;
            return GetStyle().CalcHeight(new GUIContent(MapSerializedPropertyContent(property)), viewWidth) + EditorGUIUtility.singleLineHeight;
        }

        static SerializedProperty currentProperty = null;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property != currentProperty)
            {
                currentProperty = property;
                propertyContentMap.Clear();
            }

            var content = MapSerializedPropertyContent(property);
            if (position.width > 1) propertyWidthMap[property.propertyPath] = position.width;

            var c = GUI.color;
            GUI.color = Color.clear;
            var show_editor = GUI.Button(position, "");
            GUI.color = c;

            var prop = property.FindPropertyRelative("content");
            var labelPosition = position;
            labelPosition.height = EditorGUIUtility.singleLineHeight;
            position.height -= labelPosition.height;
            position.y += labelPosition.height;
            EditorGUI.LabelField(labelPosition, label);
            EditorGUI.LabelField(position, content, GetStyle());

            if (show_editor)
            {
                position.center = EditorGUIUtility.GUIToScreenPoint(position.center);
                position.y -= 1;
                CustomTextEditorWindow.Show(position, property);
            }
        }

    }




















}