using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Usage:
/// 
/// [System.Serializable]
/// public class Preset
/// {
///     public enum Type
///     {
///         BUTTON,
///         CANCEL,
///         CHAT,
///         OPTION,
///         ROOM,
///         TAP
///     }
///     public Type type;
///     public AudioClip clip;
/// }
/// 
/// [PropertyMapEnum("type")]
/// public List<Preset> presets;
/// 
/// </summary>


public class PropertyMapEnum : PropertyAttribute
{
    string field;
    public string Field => field;
    public PropertyMapEnum(string field)
    {
        this.field = field;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(PropertyMapEnum))]
public class PropertyMapEnumDrawer : PropertyDrawer
{
    HashSet<string> initializedPaths = new HashSet<string>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        property.isExpanded = true;
        return EditorGUI.GetPropertyHeight(property, label) - EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyPath.EndsWith("[0]") && initializedPaths.Contains(property.propertyPath) == false) SetupPropertyMapEnum(property);

        position.height = EditorGUIUtility.singleLineHeight;
        var depth = property.depth;
        property.NextVisible(true);
        do
        {
            if (property.depth <= depth) break;

            if (property.name == ((PropertyMapEnum)attribute).Field)
                EditorGUI.LabelField(position, property.enumDisplayNames[property.enumValueIndex], EditorStyles.boldLabel);
            else
                EditorGUI.PropertyField(position, property, new GUIContent(property.displayName));
            position.y += EditorGUIUtility.singleLineHeight;
        } while (property.Next(false));
    }

    void SetupPropertyMapEnum(SerializedProperty property)
    {
        PropertyMapEnum attr = (PropertyMapEnum)this.attribute;

        initializedPaths.Add(property.propertyPath);

        var arrayProperty = GetArrayProperty(property);
        if (arrayProperty.arraySize <= 0) arrayProperty.InsertArrayElementAtIndex(0);

        var first_element = arrayProperty.GetArrayElementAtIndex(0);
        var enum_prop = first_element.FindPropertyRelative(attr.Field);
        if (enum_prop.propertyType != SerializedPropertyType.Enum) return;

        var enum_names = enum_prop.enumNames;
        while (arrayProperty.arraySize > enum_names.Length) arrayProperty.DeleteArrayElementAtIndex(arrayProperty.arraySize - 1);
        while (arrayProperty.arraySize < enum_names.Length) arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);

        for (var i = 0; i < enum_names.Length; i++)
            arrayProperty.GetArrayElementAtIndex(i).FindPropertyRelative(attr.Field).enumValueIndex = i;
        property.serializedObject.ApplyModifiedProperties();
    }

    static SerializedProperty GetArrayProperty(SerializedProperty prop)
    {
        var trimIndex = prop.propertyPath.LastIndexOf(".Array.data[");
        if (trimIndex < 0) return null;

        var path = prop.propertyPath.Substring(0, trimIndex);
        return prop.serializedObject.FindProperty(path);
    }
}
#endif