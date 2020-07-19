using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using System.IO;

/* 
 * TODO: ADD A DEFAULT CREATOR FOLDER AND ONCREATE FOLDER
 */

public abstract class ScritpableObjectBrowserEditor
{
    protected UnityEditor.Editor cachedEditor = null;

    public virtual void SetTargetObjects(UnityEngine.Object[] objs) { }
    public virtual void RenderInspector() { }

    protected string defaultStoragePath = null;
    public string DefaultStoratePath => defaultStoragePath;

    protected T CreateAsset<T>(string path) where T : UnityEngine.Object
    {
        if (path.EndsWith(".asset") == false) path += ".asset";
        if (new FileInfo(path).Exists) return null;

        var result = System.Activator.CreateInstance<T>();
        AssetDatabase.CreateAsset(result, path);
        AssetDatabase.ImportAsset(path);

        return result;
    }

    protected T CreateSubAsset<T>(ScriptableObject obj, string name) where T : UnityEngine.Object
    {
        var path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return null;

        var asset = System.Activator.CreateInstance<T>();
        asset.name = name;
        AssetDatabase.AddObjectToAsset(asset, path);
        AssetDatabase.ImportAsset(path);

        return asset;
    }

    protected void RemoveAllSubAsset<T>(ScriptableObject obj) where T:UnityEngine.Object {
        var path = AssetDatabase.GetAssetPath(obj);
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != obj) return;

        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            if (asset == obj) continue;
            if (!typeof(T).IsAssignableFrom(asset.GetType())) continue;
            AssetDatabase.RemoveObjectFromAsset(asset);
        }

        AssetDatabase.ImportAsset(path);
    }

    protected GameObject CreatePrefab(string path, Action<GameObject> onPrefabCreated = null)
    {
        var go = new GameObject();
        go.hideFlags = HideFlags.HideInHierarchy;
        if (onPrefabCreated != null) onPrefabCreated(go);
        var new_go = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
        GameObject.DestroyImmediate(go);
        new_go.hideFlags = HideFlags.None;
        AssetDatabase.ImportAsset(path);
        return new_go;
    }

    protected string GetAssetContainingFolder(UnityEngine.Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return null;

        var dir = new FileInfo(path).Directory.FullName;
        var i = dir.IndexOf("Assets");
        dir = dir.Substring(i);

        return dir;
    }

    protected List<T> FindAllLocalAssets<T>(UnityEngine.Object asset) where T:UnityEngine.Object
    {
        List<T> results = new List<T>();
        HashSet<string> assetPaths = new HashSet<string>();
        string path = GetAssetContainingFolder(asset);

        foreach (var objUID in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { path }))
            assetPaths.Add(AssetDatabase.GUIDToAssetPath(objUID));

        foreach (var assetPath in assetPaths)
            foreach (var loadedAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (loadedAsset.GetType() == typeof(T)) results.Add((T) loadedAsset);

        return results;
    }
}

public abstract class ScriptableObjectBrowserEditor<T> : ScritpableObjectBrowserEditor where T : UnityEngine.Object
{
    T targetObject;

    protected T Target => (T)cachedEditor.target;
    protected IEnumerable<T> Targets { get { foreach (UnityEngine.Object t in cachedEditor.targets) yield return (T)t; } }

    public override void SetTargetObjects(UnityEngine.Object[] objs)
    {
        if (objs == null || objs.Length <= 0) targetObject = null;
        else targetObject = (T)objs[0];

        UnityEditor.Editor.CreateCachedEditor(objs, null, ref this.cachedEditor);
        if (this.cachedEditor != null) this.cachedEditor.ResetTarget();
    }

    private bool draw_default_inspector = false;
    public override void RenderInspector()
    {
        if (targetObject == null) return;
        CustomInspector(this.cachedEditor.serializedObject);
    }

    protected void DrawDefaultInspector()
    {
        this.cachedEditor.DrawDefaultInspector();
    }

    public virtual void CustomInspector(SerializedObject obj)
    {
        DrawDefaultInspector();
    }

    protected void ButtonRun(string label, Action<T> action)
    {
        if (Targets.Count() > 1) return;
        if (GUILayout.Button(label, EditorStyles.miniButton)) action(Target);
    }

    protected void ButtonRunForEach(string label, Action<T> action)
    {
        if (GUILayout.Button(label, EditorStyles.miniButton)) RunForEach(action);
    }

    protected void RunForEach(Action<T> action)
    {
        foreach (var target in this.Targets) action(target);
    }
}