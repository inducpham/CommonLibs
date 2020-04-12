using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class ListShuffle
{
    private static System.Random rng = new System.Random();

    public static List<T> Shuffle<T>(this List<T> list)
    {
        int n = list.Count;
        while (n > 0)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
        return list;
    }
}
