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

        public string instructionName;
        public List<Parameter> parameters = new List<Parameter>();
        public int indent = 0;
        public bool controlIf, controlLoop;

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

    public IEnumerable<Instruction> IterateInstance()
    {
        // create the loop counts
        var loop_counts = new List<int>(scriptInstructions.Count);
        for (var i = 0; i < scriptInstructions.Count; i++) loop_counts.Add(0);

        // map the instruction name to 
        var mapMethods = ExtractMethodReflections();

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
                var result = method.Invoke(this, parameters);

                if (current_instruction.controlIf && method.ReturnType == typeof(bool)) success = (bool)result;
                yield return current_instruction;
            }

            // move on to the next instruction here
            // TODO: check for indentation
            previous_instruction_index = current_instruction_index;
            if (current_instruction.controlIf && success && current_instruction.instructionChild >= 0)
                current_instruction_index = current_instruction.instructionChild;
            else
                current_instruction_index = current_instruction.instructionNext;
        }
    }

    public object[] ExtractParameters(MethodInfo method, Instruction current_instruction)
    {
        var parameterFields = method.GetParameters();
        var results = new object[parameterFields.Length];
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
                case Instruction.ParamType.OBJECT: val = scriptObjectValues[param.valueIndex]; break;
            }

            results[i] = val;
        }

        return results;
    }

    public Dictionary<string, System.Reflection.MethodInfo> ExtractMethodReflections()
    {
        var results = new Dictionary<string, System.Reflection.MethodInfo>();
        BindingFlags bindingAttrs = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        var methods = this.GetType().GetMethods(bindingAttrs);
        foreach (var method in methods)
        {
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
}