using NUnit.Framework.Internal.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal.VR;
using UnityEngine;
using UnityEngine.AddressableAssets;
using SQCommandContainer = SSQObject.ScriptableSQObject.SQCommandContainer;

namespace SSQObject
{
    public class ScriptableSQOCommandEditor
    {

        ScriptableSQOEditor editor;
        Dictionary<string, System.Type> mapCommandToType = new Dictionary<string, Type>();

        public ScriptableSQOCommandEditor(ScriptableSQOEditor editor, ScriptableSQObject target)
        {
            this.editor = editor;

            foreach (var type in target.ExtractTypes())
            {
                mapCommandToType[type.Name] = type;
            }

            foreach (var command in target.commands)
            {
                if (mapCommandToType.ContainsKey(command.commandName ?? "") == false)
                {
                    command.commandName = "";
                    continue;
                }
                command.SyncCommandParamsSlotCount(mapCommandToType[command.commandName]);
                command.DeserializeParams(mapCommandToType[command.commandName]);
            }
        }

        public void CalculateCommandHeight(SQCommandContainer command, float width, out float content_height)
        {
            if (command.stringParams == null || command.stringParams.Count <= 1)
                content_height = EditorGUIUtility.singleLineHeight;
            else
                content_height = EditorGUIUtility.singleLineHeight * command.stringParams.Count;
        }

        public bool RenderCommand(SQCommandContainer command, int index, Rect rect)
        {
            if (mapCommandToType.ContainsKey(command.commandName ?? "") == false) RenderCommandLine(command, index, rect);
            else RenderCommandType(command, index, rect);

            var editing = GUI.GetNameOfFocusedControl().StartsWith("DSLC" + index + ".");
            return editing;
        }

        public void FocusLine(int line)
        {
            EditorGUI.FocusTextInControl("DSLC" + (line) + ".0");
        }


        void RenderCommandLine(SQCommandContainer command, int index, Rect rect)
        {
            var control_name = "DSLC" + index + ".0";

            var editing = GUI.GetNameOfFocusedControl() == control_name && EditorGUIUtility.editingTextField;
            if (editing && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space && (Event.current.control || Event.current.command))
            {
                if (mapCommandToType.ContainsKey(command.contents))
                {
                    command.commandName = command.contents;
                    command.SyncCommandParamsSlotCount(mapCommandToType[command.commandName]);
                    command.DeserializeParams(mapCommandToType[command.commandName]);

                    FocusLine(index);
                    editor.Repaint();
                    EditorApplication.delayCall += () =>
                    {
                        FocusLine(index);
                        editor.Repaint();
                    };
                }
                Event.current.Use();
                return;
            }

            GUI.SetNextControlName(control_name);
            command.contents = EditorGUI.TextField(rect, command.contents, EditorStyles.label);
        }
        void RenderCommandType(SQCommandContainer command, int index, Rect rect)
        {
            var editing = GUI.GetNameOfFocusedControl().StartsWith("DSLC" + index + ".");

            var type = mapCommandToType[command.commandName];

            //get public fields of type
            var fields = type.GetFields();

            command.SyncCommandParamsSlotCount(type);

            rect.height = EditorGUIUtility.singleLineHeight;
            for (var i = 0; i < fields.Length; i++)
            {

                var field = fields[i];
                var field_type = field.FieldType;

                //calculate label rect and field rect
                var label_rect = rect;
                label_rect.width = 100;
                EditorGUI.LabelField(label_rect, field.Name);
                var field_rect = rect;
                field_rect.x += 100;
                field_rect.width -= 100;

                var control_name = "DSLC" + index + "." + i;
                GUI.SetNextControlName(control_name);

                if (typeof(UnityEngine.Object).IsAssignableFrom(field_type))
                {
                    command.objectParams[i] = EditorGUI.ObjectField(field_rect, command.objectParams[i], field_type, false);
                    command.deserializedCommandParams[i] = command.objectParams[i];
                }
                else
                {
                    if (field_type == typeof(string))
                    {
                        command.deserializedCommandParams[i] = EditorGUI.TextField(field_rect, command.deserializedCommandParams[i] as string, EditorStyles.label);
                    }
                    else if (field_type == typeof(float))
                    {
                        if (command.deserializedCommandParams[i] == null) command.deserializedCommandParams[i] = 0f;
                        command.deserializedCommandParams[i] = EditorGUI.FloatField(field_rect, (float)command.deserializedCommandParams[i]);
                    }
                    else if (field_type == typeof(int))
                    {
                        if (command.deserializedCommandParams[i] == null) command.deserializedCommandParams[i] = 0;
                        command.deserializedCommandParams[i] = EditorGUI.IntField(field_rect, (int)command.deserializedCommandParams[i]);
                    }
                    else if (field_type == typeof(bool))
                    {
                        if (command.deserializedCommandParams[i] == null) command.deserializedCommandParams[i] = false;
                        command.deserializedCommandParams[i] = EditorGUI.Toggle(field_rect, (bool)command.deserializedCommandParams[i]);
                    }
                    else if (field_type == typeof(double))
                    {
                        if (command.deserializedCommandParams[i] == null) command.deserializedCommandParams[i] = 0d;
                        command.deserializedCommandParams[i] = EditorGUI.DoubleField(field_rect, (double)command.deserializedCommandParams[i]);
                    }
                    else if (field_type.IsEnum)
                    {
                        command.deserializedCommandParams[i] = EditorGUI.EnumPopup(field_rect, (Enum)command.deserializedCommandParams[i]);
                    }
                    else
                    {
                        EditorGUI.LabelField(field_rect, "Unsupported type: " + field_type);
                    }

                    if (editing) command.stringParams[i] = command.deserializedCommandParams[i].ToString();
                }

                rect.y += rect.height;
            }
        }
    }
}