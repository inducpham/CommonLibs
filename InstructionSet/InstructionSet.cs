using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InstructionSet
{

    public List<Instruction> instructions;
    [System.NonSerialized]
    bool valueCollected = false;

    [System.Serializable]
    public class Instruction
    {
        public string type;
        public int valueIndex;

        [System.NonSerialized]
        public System.Object value = null;
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class Default : System.Attribute { }

    public IEnumerable<(string, System.Object)> IterateInstruction { get { foreach (var result in FunctionIterateInstructions()) yield return result; } }
    #region ITERATE INSTRUCTION
    public IEnumerable<(string, System.Object)> FunctionIterateInstructions(bool force = false)
    {
        if (force) valueCollected = false;
        if (valueCollected)
        {
            foreach (var instruction in instructions) yield return (instruction.type, instruction.value);
            yield break;
        }

        if (instructions == null) yield break;

        Dictionary<string, IList> mapTypeToList = new Dictionary<string, IList>();
        var type = this.GetType();
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (typeof(IList).IsAssignableFrom(field.FieldType)) mapTypeToList[field.Name] = (IList)field.GetValue(this);

        foreach (var instruction in instructions) {
            if (mapTypeToList.ContainsKey(instruction.type) == false) continue;
            var list = mapTypeToList[instruction.type];
            if (instruction.valueIndex < 0 || instruction.valueIndex >= list.Count) continue;
            instruction.value = list[instruction.valueIndex];
        }

        foreach (var instruction in instructions)
            yield return (instruction.type, instruction.value);
        valueCollected = true;
    }
    #endregion
    
}