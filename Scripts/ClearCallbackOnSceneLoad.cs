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

        SceneManager.sceneUnloaded += (scene) =>
        {
            foreach (var field in fields)
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    Delegate delegateContainer = (Delegate)field.GetValue(obj);
                    if (delegateContainer == null) continue;

                    var delegates = new List<Delegate>(delegateContainer.GetInvocationList());
                    delegateContainer = null;

                    foreach (var del in delegates)
                    {
                        if (del.Target == null || del.Target.Equals(null))
                        {
                            continue;
                        }

                        if (typeof(Component).IsAssignableFrom(del.Target.GetType()))
                        {
                            try
                            {
                                var go = ((Component)del.Target).gameObject;
                                if (go == null || go.Equals(null)) continue;
                            }
                            catch (MissingReferenceException)
                            {
                                continue;
                            }
                        }

                        if (delegateContainer == null) delegateContainer = del;
                        else Delegate.Combine(delegateContainer, del);
                    }

                    field.SetValue(obj, delegateContainer);
                }

        };
    }
}