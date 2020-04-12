﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InstantiableBehaviour<T> : MonoBehaviour
{
    protected T instanceData;
    protected int instanceIndex;
    private List<InstantiableBehaviour<T>> instances = new List<InstantiableBehaviour<T>>();

    public void ClearInstances()
    {
        if (instances == null) return;
        foreach (var instance in instances)
            Destroy(instance.gameObject);
        instances.Clear();
    }

    public List<InstantiableBehaviour<T>> ReInstantiate(IEnumerable<T> datas)
    {
        ClearInstances();
        return Instantiate(datas);
    }

    public List<InstantiableBehaviour<T>> Instantiate(IEnumerable<T> datas)
    {
        var results = new List<InstantiableBehaviour<T>>();
        foreach (var data in datas)
            results.Add(Instantiate(data));
        return results;
    }

    public InstantiableBehaviour<T> Instantiate(T data)
    {
        if (instances == null) instances = new List<InstantiableBehaviour<T>>();

        var instance = GameObject.Instantiate<InstantiableBehaviour<T>>(this, transform.parent);
        instance.instanceData = data;
        instance.instanceIndex = instances.Count;
        instance.gameObject.SetActive(true);
        instance.OnInstantiated();
        return instance;
    }

    public abstract void OnInstantiated();
}
