using System;
using System.Collections.Generic;
using SQCommandContainer = SSQObject.ScriptableSQObject.SQCommandContainer;
using Command = SSQObject.ScriptableSQObject.Command;
using UnityEngine;
using UnityEditor;

public static class ScriptableSQObjectCommandGenericExtension
{


    //void SyncCommandContents(SQCommandContainer command, Type type)

    public static void SyncCommandParamsSlotCount(this SQCommandContainer command, Type type)
    {
        var fields = type.GetFields();

        if (command.objectParams == null || command.objectParams.Count != fields.Length)
        {
            command.objectParams ??= new List<UnityEngine.Object>();
            if (command.objectParams.Count > fields.Length) command.objectParams.RemoveRange(fields.Length, command.objectParams.Count - fields.Length);
            while (command.objectParams.Count < fields.Length) command.objectParams.Add(null);
        }

        if (command.stringParams == null || command.stringParams.Count != fields.Length)
        {
            command.stringParams ??= new List<string>();
            if (command.stringParams.Count > fields.Length) command.stringParams.RemoveRange(fields.Length, command.stringParams.Count - fields.Length);
            while (command.stringParams.Count < fields.Length) command.stringParams.Add("");
        }

        if (command.deserializedCommandParams == null || command.deserializedCommandParams.Length != fields.Length)
            command.deserializedCommandParams = new object[fields.Length];
    }

    public static void DeserializeParams(this SQCommandContainer command, Type type)
    {
        command.SyncCommandParamsSlotCount(type);

        var fields = type.GetFields();

        for (var i = 0; i < fields.Length; i++)
        {
            if (i < command.stringParams.Count)
            {
                var field = fields[i];
                var field_type = field.FieldType;
                if (field_type == typeof(string))
                {
                    command.deserializedCommandParams[i] = command.stringParams[i];
                }
                else if (field_type == typeof(float))
                {
                    var value = 0f;
                    float.TryParse(command.stringParams[i], out value);
                    command.deserializedCommandParams[i] = value;
                }
                else if (field_type == typeof(int))
                {
                    var value = 0;
                    int.TryParse(command.stringParams[i], out value);
                    command.deserializedCommandParams[i] = value;
                }
                else if (field_type == typeof(bool))
                {
                    var value = false;
                    bool.TryParse(command.stringParams[i], out value);
                    command.deserializedCommandParams[i] = value;
                }
                else if (field_type == typeof(double))
                {
                    var value = 0d;
                    double.TryParse(command.stringParams[i], out value);
                    command.deserializedCommandParams[i] = value;
                }
                else if (field_type.IsEnum)
                {
                    //get default enum value from field type
                    var value = Enum.GetValues(field_type).GetValue(0);
                    //try parse enum to int
                    if (Enum.TryParse(field_type, command.stringParams[i], out var parsed_value)) value = parsed_value;
                    command.deserializedCommandParams[i] = value;
                }
                else if (field_type == typeof(UnityEngine.Object))
                {
                    command.deserializedCommandParams[i] = command.objectParams[i];
                }
            }
        }
    }

    public static Command DeserializeToCommand(this SQCommandContainer command, Type type)
    {
        if (typeof(Command).IsAssignableFrom(type) == false) throw new Exception("Type is not a Command type.");

        var instance = Activator.CreateInstance(type);
        var fields = type.GetFields();

        for (var i = 0; i < fields.Length; i++)
        {
            fields[i].SetValue(instance, command.deserializedCommandParams[i]);
        }

        return (Command) instance;
    }
}