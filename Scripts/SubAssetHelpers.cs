using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public class SubAssetHelpers : Editor
{

    static List<Type> SUB_ASSET_TYPES = new List<Type>() { typeof(ScriptableObject), typeof(AnimationClip) };

    static SubAssetHelpers()
    {
        EditorApplication.projectWindowItemOnGUI += new EditorApplication.ProjectWindowItemCallback(Callback);
    }

    static void Callback(string path, Rect rect)
    {
        if (rect.Contains(Event.current.mousePosition) == false) return;
        if (DragAndDrop.objectReferences.Length <= 0) return;

        foreach (var asset in DragAndDrop.objectReferences)
        {
            if (asset == null) return;
            bool valid = false;
            foreach (var type in SUB_ASSET_TYPES)
            {
                if (type.IsAssignableFrom(asset.GetType()))
                    valid = true;
            }

            if (!valid) return;
        }

        foreach (var asset in DragAndDrop.objectReferences)
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GetAssetPath(asset)) != asset) return;

        path = AssetDatabase.GUIDToAssetPath(path);
        var this_asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (typeof(DefaultAsset).IsAssignableFrom(this_asset.GetType())
            || typeof(MonoScript).IsAssignableFrom(this_asset.GetType()))
            return;

        foreach (var asset in DragAndDrop.objectReferences)
            if (asset == this_asset) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Move;

        if (Event.current.type == EventType.DragUpdated)
        {
            DragAndDrop.AcceptDrag();
        }

        if (Event.current.type == EventType.DragPerform)
        {
            Debug.Log("Dropping subassets");
            foreach (var asset in DragAndDrop.objectReferences)
            {
                var asset_path = AssetDatabase.GetAssetPath(asset);
                var asset_name = Path.GetFileName(asset_path);
                var lower_asset_name = asset_name.ToLower();

                if (lower_asset_name.EndsWith(".asset"))
                    asset_name = asset_name.Substring(0, asset_name.Length - 6);
                else if (lower_asset_name.EndsWith(".anim"))
                    asset_name = asset_name.Substring(0, asset_name.Length - 5);
                else if (lower_asset_name.EndsWith(".controller"))
                    asset_name = asset_name.Substring(0, asset_name.Length - 11);

                AssetDatabase.RemoveObjectFromAsset(asset);
                AssetDatabase.AddObjectToAsset(asset, path);

                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.DeleteAsset(asset_path);
                    AssetDatabase.ImportAsset(asset_path);
                };
                asset.name = asset_name;
            }
            AssetDatabase.ImportAsset(path);
        }
    }

    [MenuItem("Assets/Unpack from subassets %$u", priority = 20)]
    public static void UnpackSubAssets()
    {
        if (ValidateUnpackSubAssets() == false) return;

        string asset_path = AssetDatabase.GetAssetPath(Selection.objects[0]);
        string folder_path = Path.GetDirectoryName(asset_path);
        UnityEngine.Object host_object = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset_path);

        var assets = new List<UnityEngine.Object>();
        if (Selection.objects.Length == 1 && Selection.objects[0] == host_object)
            assets.AddRange(AssetDatabase.LoadAllAssetsAtPath(asset_path));
        else
            assets.AddRange(Selection.objects);

        assets.Remove(host_object);
        assets.RemoveAll((a) => AssetDatabase.IsSubAsset(a) == false);
        var new_paths = new List<string>();

        foreach (var asset in assets)
        {
            AssetDatabase.RemoveObjectFromAsset(asset);
            var ext = "asset";
            if (asset is AnimationClip) ext = "anim";
            if (asset is RuntimeAnimatorController) ext = "controller";

            var path = folder_path + "/" + asset.name + "." + ext;
            new_paths.Add(path);
        }

        EditorApplication.delayCall += () => ReaddAssets(assets, new_paths);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(host_object));
    }

    static void ReaddAssets(List<UnityEngine.Object> assets, List<string> new_paths)
    {
        for (var i = 0; i < assets.Count; i++)
        {
            var asset = assets[i];
            var path = new_paths[i];
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path);
        }
    }

    [MenuItem("Assets/Unpack from subassets", priority = 20, validate = true)]
    public static bool ValidateUnpackSubAssets()
    {
        if (Selection.objects.Length <= 0) return false;

        string asset_path = AssetDatabase.GetAssetPath(Selection.objects[0]);
        UnityEngine.Object host_object = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset_path);

        if (Selection.objects.Length == 1 && Selection.objects[0] == host_object) return true;

        foreach (var obj in Selection.objects)
        {
            if (obj == null) return false;
            if (obj == host_object) return false;
            if (AssetDatabase.GetAssetPath(obj.GetInstanceID()) != asset_path) return false;
        }
        return true;
    }
}

#endif