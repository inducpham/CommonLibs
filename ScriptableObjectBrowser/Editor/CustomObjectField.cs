using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

//[CustomPropertyDrawer(typeof(ScriptableObject), true)]
//public class CustomScriptableObjectField : PropertyDrawer
//{
//    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
//    {
//        var e = Event.current;
//        if (e.isMouse && e.type == EventType.MouseDown && e.clickCount == 2 && position.Contains(e.mousePosition)) {
//            var obj = property.objectReferenceValue;
//            if (obj != null) AssetDatabase.OpenAsset(obj);
//            e.Use();
//        }
//        EditorGUI.ObjectField(position, property, label);
//    }
//}