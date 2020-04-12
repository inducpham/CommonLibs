using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClearCallbackOnSceneLoad
{
    public static void Setup(System.Object obj)
    {
        var type = obj.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (mode != LoadSceneMode.Single) return;

            foreach (var field in fields)
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    field.SetValue(obj, null);
        };
    }
}