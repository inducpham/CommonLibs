using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class ScriptboundObjectExtension
{
    public static T DuplicateAndRun<T>(this T obj) where T : ScriptboundObject
    {
        var o = ScriptableObject.Instantiate(obj);
        o.RunInstance();
        return o;
    }

    public static T Duplicate<T>(this T obj) where T : ScriptboundObject
    {
        var o = ScriptableObject.Instantiate(obj);
        return o;
    }

    public static IEnumerable<ScriptboundObject.Instruction> DuplicateAndIterate<T>(this T obj) where T : ScriptboundObject
    {
        var o = ScriptableObject.Instantiate(obj);
        foreach (var instruction in o.IterateInstance())
            yield return instruction;
    }
}

public class ScriptboundObject : ScriptableObject
{
    public List<string> scriptStringValues;
    public List<int> scriptIntValues;
    public List<float> scriptFloatValues;
    public List<bool> scriptBoolValues;
    public List<UnityEngine.Object> scriptObjectValues;

    [System.Serializable]
    public class Instruction
    {
        [System.Serializable]
        public enum ParamType
        {
            STRING,
            INT,
            FLOAT,
            BOOL,
            ENUM,
            OBJECT
        }

        [System.Serializable]
        public class Parameter
        {
            public ParamType type;
            public int valueIndex;
        }

        [System.Serializable]
        public class Injectible
        {
            public int index;
            public UnityEngine.Object obj;
        }

        public string instructionName;
        public List<Parameter> parameters = new List<Parameter>();
        public List<Injectible> injectibles = new List<Injectible>();
        public int indent = 0;
        public bool controlIf, controlLoop;
        public bool negative;

        [System.NonSerialized]
        public int instructionNext, instructionChild;
    }

    public List<Instruction> scriptInstructions;

    void RemapInstructionSequences()
    {
        for (var i = 0; i < scriptInstructions.Count; i++)
        {
            var instruction = scriptInstructions[i];
            instruction.instructionChild = -1;
            instruction.instructionNext = -1;

            if (i == scriptInstructions.Count - 1) break;

            for (var k = i + 1; k < scriptInstructions.Count; k++)
                if (scriptInstructions[k].indent <= instruction.indent)
                {
                    instruction.instructionNext = k;
                    break;
                }
            if (scriptInstructions[i + 1].indent > instruction.indent) instruction.instructionChild = i + 1;
        }
    }

    public void RunInstance()
    {
        foreach (var instruction in IterateInstance()) ;
    }

    private Dictionary<string, MethodInfo> mapMethods;
    private string defaultStringMethod;
    private int recentInstructionIndex;
    protected List<Instruction.Injectible> RecentInstructionInjectibles => scriptInstructions[recentInstructionIndex].injectibles;

    void ExtractMapInstructions()
    {
        if (this.mapMethods != null) return;
        this.mapMethods = ExtractMethodReflections();
        foreach (var method in mapMethods.Values)
        {
            var prmts = method.GetParameters();
            if (prmts.Length != 1 || prmts[0].ParameterType != typeof(string)) continue;
            if (method.GetCustomAttribute(typeof(ScriptboundObject.Default), true) == null) continue;
            defaultStringMethod = method.Name;
        }
    }


    public IEnumerable<Instruction> IterateInstance()
    {
        // create the loop counts
        var loop_counts = new List<int>(scriptInstructions.Count);
        for (var i = 0; i < scriptInstructions.Count; i++) loop_counts.Add(0);

        ExtractMapInstructions();

        // register the instruction index
        var previous_instruction_index = -1;
        var current_instruction_index = 0;

        RemapInstructionSequences();

        // start looping through the instructions
        while (current_instruction_index < scriptInstructions.Count)
        {
            if (current_instruction_index == -1) break;

            // do stuffs here

            var current_instruction = scriptInstructions[current_instruction_index];
            var success = true;

            if (mapMethods.ContainsKey(current_instruction.instructionName))
            {
                var method = mapMethods[current_instruction.instructionName];
                var parameters = ExtractParameters(method, current_instruction);
                recentInstructionIndex = current_instruction_index;

                //if (current_instruction.parameters.Count > 0 && current_instruction.parameters[0].type == Instruction.ParamType.OBJECT) parameters[0] = null;
                if (parameters.Length > method.GetParameters().Length) Array.Resize(ref parameters, method.GetParameters().Length);
                var result = method.Invoke(this, parameters);

                if (current_instruction.controlIf && method.ReturnType == typeof(bool))
                {
                    success = (bool)result;
                    if (current_instruction.negative) success = !success;
                }
                yield return current_instruction;
            }
            

            // move on to the next instruction here
            // TODO: check for indentation
            previous_instruction_index = current_instruction_index;

            if (skipInstructionEntrance)
            {
                current_instruction_index = current_instruction.instructionNext;
                skipInstructionEntrance = false;
                continue;
            }

            if (current_instruction.controlIf == false && current_instruction.instructionChild >= 0)
                current_instruction_index = current_instruction.instructionChild;
            else if (current_instruction.controlIf && success && current_instruction.instructionChild >= 0)
                current_instruction_index = current_instruction.instructionChild;
            else
                current_instruction_index = current_instruction.instructionNext;
        }
    }

    //protected List<int> recentDefaultStringInjectibleObjects
    void ExtractDefaultStringInjectibles(object[] parameters)
    {
        //if (parameters.Length <= 1 || parameters.Length % 2 == 0) return;

        //for (var i = 1; i < parameters.Length; i++)
        //{
        //    if (parameters[i] == null) return;
        //    if (i % 2 == 1 && parameters[i].GetType() != typeof(int)) return;
        //    if (i % 2 == 0 && typeof(UnityEngine.Object).IsAssignableFrom(parameters[i].GetType()) == false) return;
        //}

        //var count = (parameters.Length - 1) / 2;
        //for (var i = count - 1; i >= 0; i--)
        //{
        //    var index = (int)parameters[i * 2 + 1];
        //    var obj = (UnityEngine.Object)parameters[i * 2 + 2];
        //    var obj_str = "[[" + ObjectToString(obj) + "]]";
        //    if (highlight) obj_str = "<b>" + obj_str + "</b>";
        //    result = result.Insert(index, obj_str);
        //}

        //return result;
    }

    public System.Object[] ExtractParameters(MethodInfo method, Instruction current_instruction)
    {
        var parameterFields = method.GetParameters();
        var results = new System.Object[parameterFields.Length];
        var count = Mathf.Min(parameterFields.Length, current_instruction.parameters.Count);

        for (var i = 0; i < count; i++)
        {
            var param = current_instruction.parameters[i];
            object val = null;

            switch (param.type) {
                case Instruction.ParamType.STRING: val = scriptStringValues[param.valueIndex]; break;
                case Instruction.ParamType.INT: val = scriptIntValues[param.valueIndex]; break;
                case Instruction.ParamType.FLOAT: val = scriptFloatValues[param.valueIndex]; break;
                case Instruction.ParamType.BOOL: val = scriptBoolValues[param.valueIndex]; break;
                case Instruction.ParamType.ENUM: val = System.Enum.ToObject(parameterFields[i].ParameterType, scriptIntValues[param.valueIndex]); break;
                case Instruction.ParamType.OBJECT:
                    val = scriptObjectValues[param.valueIndex];
                    UnityEngine.Object o = (UnityEngine.Object)val;
                    if (o == null) val = null;
                    break;
            }

            results[i] = val;
        }

        for (var i = 0; i < results.Length; i++)
            if (results[i] == null) results[i] = null;
        return results;
    }

    public Dictionary<string, System.Reflection.MethodInfo> ExtractMethodReflections()
    {
        var results = new Dictionary<string, System.Reflection.MethodInfo>();
        BindingFlags bindingAttrs = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        var methods = this.GetType().GetMethods(bindingAttrs);
        foreach (var method in methods)
        {
            if (method.IsSpecialName) continue;
            if (method.DeclaringType == typeof(System.Object)) continue;
            if (method.DeclaringType == typeof(UnityEngine.Object)) continue;
            if (method.DeclaringType == typeof(UnityEngine.ScriptableObject)) continue;
            if (method.DeclaringType == typeof(ScriptboundObject)) continue;
            results[method.Name] = method;
        }

        return results;
    }

    public string GetPreviewContents()
    {
        return " ";
    }

    protected bool skipInstructionEntrance = false;
    protected List<string> CollectChildrenDefaultStringEntries()
    {
        var results = new List<string>();
        if (defaultStringMethod == null) return results;

        var currentInstruction = scriptInstructions[recentInstructionIndex];
        for (var i = recentInstructionIndex + 1; i < scriptInstructions.Count; i++)
        {
            var instruction = scriptInstructions[i];
            if (instruction.indent <= currentInstruction.indent) break;
            if (instruction.instructionName != defaultStringMethod || instruction.indent != currentInstruction.indent + 1) continue;
            if (instruction.parameters.Count != 1 || instruction.parameters[0].type != Instruction.ParamType.STRING) continue;
            var string_value_index = instruction.parameters[0].valueIndex;
            results.Add(scriptStringValues[string_value_index]);
        }

        return results;
    }
    
    protected List<List<ScriptboundObject.Instruction.Injectible>> CollectChildrenDefaultStringInjectibles()
    {
        var results = new List<List<ScriptboundObject.Instruction.Injectible>>();
        if (defaultStringMethod == null) return results;

        var currentInstruction = scriptInstructions[recentInstructionIndex];
        for (var i = recentInstructionIndex + 1; i < scriptInstructions.Count; i++)
        {
            var instruction = scriptInstructions[i];
            if (instruction.indent <= currentInstruction.indent) break;
            if (instruction.instructionName != defaultStringMethod || instruction.indent != currentInstruction.indent + 1) continue;
            if (instruction.parameters.Count != 1 || instruction.parameters[0].type != Instruction.ParamType.STRING) continue;
            results.Add(instruction.injectibles);
        }

        return results;

    }

    [AttributeUsage(AttributeTargets.Method)]
    public class Default : System.Attribute { }

    public class StringInjectible : System.Attribute
    {
        public Type[] types = new Type[0];
        public string[] labels = new string[0];

        public StringInjectible(params System.Type[] types)
        {
            this.types = types;
        }

        public StringInjectible(params string[] labels)
        {
            this.labels = labels;
        }
    }
}