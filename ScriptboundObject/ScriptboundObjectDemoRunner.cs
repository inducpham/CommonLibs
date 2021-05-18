using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptboundObjectDemoRunner : MonoBehaviour
{
    public ScriptboundObjectDemo demo;

    // Start is called before the first frame update
    void Start()
    {
        demo.DuplicateAndRun();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
