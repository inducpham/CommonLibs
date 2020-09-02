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
        public int indent;

        [System.NonSerialized]
        public System.Object value = null;
    }

    private InstructionNode startingInstructionNode;
    public InstructionNode StartingInstructionNode => GetStartingInstructionNode();
    public class InstructionNode
    {
        public string type;
        public System.Object value = null;
        public int indent;
        public InstructionNode child, next;
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class Default : System.Attribute { }

    public IEnumerable<(string, System.Object)> IterateInstruction { get { foreach (var (key, val, indent) in FunctionIterateInstructions()) yield return (key, val); } }
    #region ITERATE INSTRUCTION
    public IEnumerable<(string, System.Object, int)> FunctionIterateInstructions(bool force = false)
    {
        if (instructions == null) yield break;
        if (force) valueCollected = false;
        if (force || !valueCollected) CollectValues();

        foreach (var instruction in instructions)
            yield return (instruction.type, instruction.value, instruction.indent);
    }
    #endregion

    InstructionNode GetStartingInstructionNode()
    {
        CollectValues();
        return this.startingInstructionNode;
    }

    void CollectValues()
    {
        if (valueCollected) return;
        valueCollected = true;
        Dictionary<string, IList> mapTypeToList = new Dictionary<string, IList>();
        var type = this.GetType();
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (typeof(IList).IsAssignableFrom(field.FieldType)) mapTypeToList[field.Name] = (IList)field.GetValue(this);

        foreach (var instruction in instructions)
        {
            if (mapTypeToList.ContainsKey(instruction.type) == false) continue;
            var list = mapTypeToList[instruction.type];
            if (instruction.valueIndex < 0 || instruction.valueIndex >= list.Count) continue;
            instruction.value = list[instruction.valueIndex];
        }

        List<InstructionNode> nodes = new List<InstructionNode>();
        foreach (var instruction in instructions)
            nodes.Add(new InstructionNode()
            {
                type = instruction.type,
                value = instruction.value,
                indent = instruction.indent
            });

        for (var i = 0; i < nodes.Count - 1; i++)
        {
            if (nodes[i + 1].indent > nodes[i].indent)
                nodes[i].child = nodes[i + 1];
            for (var y = i + 1; y < nodes.Count; y++)
                if (nodes[y].indent <= nodes[i].indent)
                {
                    nodes[i].next = nodes[y];
                    break;
                }
        }
        if (nodes.Count > 0) this.startingInstructionNode = nodes[0];
    }
    
}