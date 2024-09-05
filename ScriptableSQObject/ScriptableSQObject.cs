using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSQObject
{
    public abstract class ScriptableSQObject : ScriptableObject
    {
        [SerializeField, HideInInspector]
        public List<SQCommandContainer> commands;

        public abstract class Command
        {

        }

        [System.Serializable]
        public class SQCommandContainer
        {
            public int indent;
            public string commandName;
            public string contents;

            public List<UnityEngine.Object> objectParams;
            public List<string> stringParams;

            [System.NonSerialized]
            public object[] deserializedCommandParams;

            public SQCommandContainer Duplicate()
            {
                var newCommand = new SQCommandContainer();
                newCommand.indent = indent;
                newCommand.commandName = commandName;
                newCommand.contents = contents;
                newCommand.objectParams = new List<UnityEngine.Object>(objectParams);
                newCommand.stringParams = new List<string>(stringParams);
                if (deserializedCommandParams != null)
                {
                    newCommand.deserializedCommandParams = new object[deserializedCommandParams.Length];
                    for (int i = 0; i < deserializedCommandParams.Length; i++)
                        newCommand.deserializedCommandParams[i] = deserializedCommandParams[i];
                }

                return newCommand;
            }
        }

        public List<Command> ExtractCommands()
        {
            var commands = new List<Command>();
            foreach (var command in this.commands)
            {
                var type = GetType().GetNestedType(command.commandName);
                if (type == null) continue;
                var instance = Activator.CreateInstance(type);

                command.DeserializeParams(type);

                var fields = type.GetFields();
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];

                    //if field is assignable to UnityEngine.Object, use objectParams
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        field.SetValue(instance, command.objectParams[i]);
                    else
                        field.SetValue(instance, command.deserializedCommandParams[i]);
                }

                commands.Add((Command)instance);
            }
            return commands;

        }

        public class DefaultStringCommand : Attribute { }
    }
}