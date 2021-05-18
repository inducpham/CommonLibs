using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ScriptboundObjectDemo : ScriptboundObject
{
    System.Action<string> callbackDialoguePlay;
    public int val = 0;

    public bool Increment()
    {
        val++;
        return true;
    }

    public void Add(int amount)
    {
        val += amount;
    }

    public void PrintTotalValue()
    {
        Debug.Log(val);
    }

    public void Test(int count, string content)
    {

    }

    public enum HelloType { HEY, HI, HOWDY }

    public void Hello(HelloType type, string name)
    {
        Debug.Log(type + " " + name);
    }

    public void Line(string line)
    {
        Debug.Log(line);
    }
}