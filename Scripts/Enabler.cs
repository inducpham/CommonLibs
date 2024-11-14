using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enabler : MonoBehaviour
{

    public GameObject[] targets;

    private void Awake()
    {
        if (this.targets != null)
            foreach (var target in this.targets)
                if (target != null) target.SetActive(true);
    }
}