using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public static class DictTryGetExtension
{
    public static V TryGet<K, V>(this Dictionary<K, V> dict, K key)
    {
        if (dict == null) return default(V);
        if (dict.ContainsKey(key) == false) return default(V);
        return dict[key];
    }

}