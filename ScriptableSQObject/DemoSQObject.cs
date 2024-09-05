using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu]
public class DemoSQObject : SSQObject.ScriptableSQObject
{
    [DefaultStringCommand]
    public class SetName : Command
    {
        public string name;
    }

    public class SetPortrait : Command
    {
        public Sprite portrait;
    }

    public class PlayMood : Command
    {
        public Mood mood;
    }

    public enum Mood
    {
        Happy,
        Sad,
        Angry,
        Neutral
    }

    public class ShowRect : Command
    {
        public float x, y, width, height;
    }

    public void DemoExtractCommands()
    {
        var commands = ExtractCommands();
        foreach (var command in commands)
        {
            Debug.Log(JsonConvert.SerializeObject(command));
        }

    }
}