using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class EnumerableRandom
{
    public static T GetRandom<T>(this IEnumerable<T> sequence)
    {
        var count = 0;
        foreach (var item in sequence) count++;
        if (count == 0) return default(T);
        var index = UnityEngine.Random.Range(0, count);
        foreach (var item in sequence)
        {
            if (index == 0) return item;
            index--;
        }
        return default(T);
    }
}