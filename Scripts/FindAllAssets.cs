using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class FindAllAssets
{
    public static List<T> Find<T>() where T:UnityEngine.Object
    {
#if UNITY_EDITOR
#else
        return new List<T>();
#endif
        List<T> results = new List<T>();
        foreach (var uid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var path = AssetDatabase.GUIDToAssetPath(uid);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (typeof(T) != null && obj != null && typeof(T).IsAssignableFrom(obj.GetType()))
                        results.Add((T) obj);
        }
        return results;
    }
}