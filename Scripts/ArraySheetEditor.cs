///
/// credit: inducpham@gmail.com
/// 
/// This script allows the PropertyAttribute ArraySheetEditor
/// Put this one on an array with simple serializable class
/// and it will show up as a spreadsheet.
/// 
/// Usage:
/// 
/// [System.Serializable]
/// public class Entry {
///     public int id;
///     public string content;
/// }
/// 
/// // Inside a scriptable object
/// [ArraySheetEditor]
/// public List<Entry> entries;
///

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
using System.Text.RegularExpressions;
#endif

public class ArraySheetEditor : PropertyAttribute {

    public int lines;
    public float[] widths;
    public bool recalculated = false;
    public int width, height;

    public ArraySheetEditor(int lines = 1, params float[] widths)
    {
        this.lines = lines;
        this.widths = widths;
    }
}
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ArraySheetEditor))]
public class ArraySheetEditorDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var attribute = (ArraySheetEditor) this.attribute;

        var parent = GetParent(property);
        return EditorGUIUtility.singleLineHeight * attribute.lines + EditorGUIUtility.singleLineHeight / parent.arraySize * 2;
    }

    static float[] weights = new float[999];

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attribute = (ArraySheetEditor)this.attribute;
        position = EditorGUI.IndentedRect(position);

        var is_first = property.propertyPath.EndsWith("[0]");
        if (is_first == false) return;

        var parent = GetParent(property);
        //EditorGUI.LabelField(position, "New fun stuffs to try " + parent.arraySize);

        // calculate the width and height and weights

        var depth = property.depth;
        var height = parent.arraySize;
        var width = 0;
        var counter = property.Copy();
        counter.Next(true);
        
        while (counter.depth > depth)
        {
            if (counter.depth == depth + 1 && ValidateProp(counter))
            {
                var x = width;
                width += 1;
                weights[x] = x < attribute.widths.Length ? attribute.widths[x] : GetDefaultWidthWeight(counter);
            }
            if (counter.Next(true) == false) break;
        }

        float total_width = 0;
        for (var i = 0; i < width; i++) total_width += weights[i];

        position.y += EditorGUIUtility.singleLineHeight / 2;
        //Draw background here
        var rect_bg = position;
        rect_bg.height *= height;
        rect_bg.height -= EditorGUIUtility.singleLineHeight;
        rect_bg.y -= 2;
        rect_bg.x -= 2;
        rect_bg.width += 4;
        rect_bg.height += 4;
        EditorGUI.DrawRect(rect_bg, Color.gray);

        // Add keyboard navigation
        var focusing = GUI.GetNameOfFocusedControl().StartsWith(parent.propertyPath);
        var change_focus = Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow;
        change_focus &= Event.current.type == EventType.KeyDown;
        change_focus &= focusing;
        var current_x = 0;
        var current_y = 0;
        if (change_focus)
        {
            var groups = Regex.Match(GUI.GetNameOfFocusedControl(), @"^*\.(\d+)\.(\d+)$").Groups;
            current_x = int.Parse(groups[1].Value);
            current_y = int.Parse(groups[2].Value);

            if (current_x > 0 && Event.current.keyCode == KeyCode.LeftArrow) current_x -= 1;
            if (current_x < width - 1 && Event.current.keyCode == KeyCode.RightArrow) current_x += 1;
            if (current_y > 0 && Event.current.keyCode == KeyCode.UpArrow) current_y -= 1;
            if (current_y < height - 1 && Event.current.keyCode == KeyCode.DownArrow) current_y += 1;

            Event.current.Use();
        }

        //EditorGUI.DrawRect(position, Color.black);

        //starts the rendering now
        var cell = position;
        cell.height = EditorGUIUtility.singleLineHeight * attribute.lines;
        var current_cell = cell;

        var il = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        counter = property.Copy();
        counter.Next(true);
        {
            var x = 0;
            while (counter.depth > depth)
            {
                if (counter.depth == depth + 1 && ValidateProp(counter))
                {
                    current_cell.width = cell.width / total_width * weights[x];
                    RenderHeader(current_cell, counter);
                    current_cell.x += current_cell.width;
                    x += 1;
                }
                if (counter.Next(true) == false) break;
            }
            cell.y += EditorGUIUtility.singleLineHeight;
        }
        current_cell = cell;

        EditorGUIUtility.GetControlID(FocusType.Passive);
        var remaining_cells = width * height;
        for (var y = 0; y < height; y++)
        {
            var success = property.Next(true);
            if (success == false) break;
            var x = 0;
            while (remaining_cells > 0 && property.depth > depth)
            {
                if (property.depth == depth + 1 && ValidateProp(property))
                {
                    var focus = change_focus && x == current_x && y == current_y;
                    current_cell.width = cell.width / total_width * weights[x] - 1;
                    RenderCell(current_cell, property, x, y, focus);
                    current_cell.x += current_cell.width + 1;
                    x++;
                    remaining_cells -= 1;
                }
                property.Next(true);
            }
            cell.y += cell.height;
            current_cell = cell;
        }
        EditorGUI.indentLevel = il;
    }

    private bool ValidateProp(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.Generic) return false;
        return true;
    }

    float GetDefaultWidthWeight(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Boolean: return 1;
            case SerializedPropertyType.Character: return 1;
            case SerializedPropertyType.ObjectReference: return 4;
            case SerializedPropertyType.String: return 4;
            default: return 2;
        }
    }

    void RenderHeader(Rect position, SerializedProperty prop)
    {
        EditorGUI.LabelField(position, ObjectNames.NicifyVariableName(prop.name), EditorStyles.miniBoldLabel);

    }

    void RenderCell(Rect position, SerializedProperty prop, int x, int y, bool focus = false)
    {
        var control_name = prop.propertyPath + "." + x + "." + y;
        var focusing = GUI.GetNameOfFocusedControl() == control_name;
        //if (focusing && prop.propertyType == SerializedPropertyType.String)
        //{
        //    position.x -= 20;
        //    position.y -= 20;
        //    position.width += 20;
        //    position.height += 20;
        //}
        GUI.SetNextControlName(control_name);
        if (focus)
        {
            GUI.FocusControl(control_name);
            EditorGUI.FocusTextInControl(control_name);
        }
        EditorGUI.PropertyField(position, prop, GUIContent.none, false);
        //GUI.Button(position, "A");
        //EditorGUI.LabelField(position, "A");
    }

    static SerializedProperty GetParent(SerializedProperty property)
    {
        string path = property.propertyPath;
        if (path.EndsWith("]"))
        {
            for (int i = 0; i < path.Length; i++)
            {
                if (path[path.Length - 1] != 'A')
                    path = path.Remove(path.Length - 1, 1);
                else
                {
                    break;
                }
            }
        }

        string parentPath = path.Substring(0, path.LastIndexOf('.'));
        return property.serializedObject.FindProperty(parentPath);
    }
}
#endif