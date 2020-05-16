using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class SerializedSceneAsset : ISerializationCallbackReceiver
{
    public string path;
#if UNITY_EDITOR
    public SceneAsset scene;
#endif

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if (scene != null)
            path = AssetDatabase.GetAssetPath(scene);
        else
            path = "";
#endif
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SerializedSceneAsset))]
public class SceneAssetPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var path = property.FindPropertyRelative("path");
        var scene = property.FindPropertyRelative("scene");

        var new_scene = EditorGUI.ObjectField(position, label, scene.objectReferenceValue, typeof(SceneAsset), false) as SceneAsset;

        if (new_scene != scene.objectReferenceValue)
        {
            var new_path = AssetDatabase.GetAssetPath(new_scene);
            path.stringValue = new_path;
            scene.objectReferenceValue = new_scene;            
        }
    }
}
#endif