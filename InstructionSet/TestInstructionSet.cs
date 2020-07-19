using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Data/TestInstruction")]
public class TestInstructionSet : ScriptableObject
{

    [System.Serializable]
    public class Instructions : InstructionSet
    {
        [InstructionSet.Default]
        public List<string> lines;
        public List<PortraitInstance> portrait;
        public List<PortraitInstance.Mood> mood;
        public List<string> title;

        public List<bool> clear;
    }

    public Instructions instructions;

    void OnValidate()
    {
        //foreach (var (type, value) in this.instructions.IterateInstruction) ; // Debug.Log(value);
    }


}
