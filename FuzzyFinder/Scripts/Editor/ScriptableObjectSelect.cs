using UnityEngine;
using UnityEditor;
using System.Collections;

public class ScriptableObjectSelect
{

    //hotkey Ctrl + Space
    [MenuItem("Tools/Ping Selected Object %L")]
    public static void PingSelectedObject()
    {
        if (Selection.activeObject != null)
        {
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}