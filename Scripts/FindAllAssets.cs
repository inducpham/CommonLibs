using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class FindAllAssets
{
    public static List<T> Find<T>() where T:UnityEngine.Object
    {
#if UNITY_EDITOR
        List<T> results = new List<T>();
        foreach (var uid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var path = AssetDatabase.GUIDToAssetPath(uid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (typeof(T) != null && obj != null && typeof(T).IsAssignableFrom(obj.GetType()))
                    results.Add((T)obj);
        }
        return results;
#else
        return new List<T>();
#endif
    }

    public static List<T> Find<T>(Func<string, bool> filter) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        List<T> results = new List<T>();
        foreach (var uid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var path = AssetDatabase.GUIDToAssetPath(uid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (typeof(T) != null && obj != null && typeof(T).IsAssignableFrom(obj.GetType()))
                    if (filter(path))
                        results.Add((T)obj);
        }
        return results;
#else
        return new List<T>();
#endif

    }

    public static List<T> Find<T>(string startingPath, Func<string, bool> filter) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        List<T> results = new List<T>();
        foreach (var uid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new string[] { startingPath }))
        {
            var path = AssetDatabase.GUIDToAssetPath(uid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (typeof(T) != null && obj != null && typeof(T).IsAssignableFrom(obj.GetType()))
                    if (filter(path))
                        results.Add((T)obj);
        }
        return results;
#else
        return new List<T>();
#endif

    }
}