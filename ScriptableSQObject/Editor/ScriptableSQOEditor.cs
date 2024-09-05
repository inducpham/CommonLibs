using NUnit.Framework.Internal.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal.VR;
using UnityEngine;
using SQCommandContainer = SSQObject.ScriptableSQObject.SQCommandContainer;

namespace SSQObject
{
    [CustomEditor(typeof(ScriptableSQObject), true)]
    public class ScriptableSQOEditor : UnityEditor.Editor
    {
        private List<SQCommandContainer> commands;

        private int selectingLineIndex = -1;
        private HashSet<SQCommandContainer> selectingLines = new HashSet<SQCommandContainer>();
        private bool draggingSelectingGroup;
        private ScriptableSQOCommandEditor commandEditor;

        private List<SQCommandContainer> clipboards = new List<SQCommandContainer>();

        ScriptableSQObject Target => (ScriptableSQObject)target;

        private void OnEnable()
        {
            commandEditor = new ScriptableSQOCommandEditor(this, Target);
            draggingSelectingGroup = false;
            SetupStyles();

            this.commands = Target.commands;
            if (commands.Count <= 0)
            {
                commands.Add(new SQCommandContainer() { contents = "" });
            }
        }

        void SetupStyles()
        {
        }

        void SaveSelectingLinesToClipboards()
        {
            clipboards.Clear();
            foreach (var line in selectingLines)
            {
                clipboards.Add(line);
            }
        }

        void PasteClipboards()
        {
            foreach (var line in clipboards)
            {
                commands.Insert(selectingLineIndex + 1, line.Duplicate());
                selectingLineIndex++;
            }
            EditorUtility.SetDirty(Target);
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            //DrawScript();
            base.OnInspectorGUI();

            var r = GUIViewHelper.GetCurrentEditorWindowRect();
            var count = 0;

            if (CheckForTabInput()) return;

            foreach ((var index, var rect) in IterateRectForCommandRender())
            {
                if (Event.current.type != EventType.Layout && r.Overlaps(rect) == false) continue;
                DrawLine(commands, index, rect);
                count++;
            }

            if (KeyDown(KeyCode.PageDown) && selectingLineIndex < 0)
            {
                if (commands.Count <= 0) AddLine(0);
                SelectLine(0, editing: true);
            }
            if (KeyDown(KeyCode.Delete) && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
            {
                RemoveLines(selectingLines);
                SelectLine(selectingLineIndex, false);
            }

            //if event is copy
            if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "Copy" && selectingLines.Count > 0 && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                SaveSelectingLinesToClipboards();

            if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "Paste" && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                PasteClipboards();
        }

        bool CheckForTabInput()
        {
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.RightArrow || Event.current.keyCode == KeyCode.LeftArrow) && CurrentEvtCommandOrControl && Event.current.alt)
            {
                var offset = Event.current.keyCode == KeyCode.LeftArrow ? -1 : 1;
                foreach (var command in selectingLines)
                {
                    command.indent += offset;
                    if (command.indent < 0) command.indent = 0;
                }

                Repaint();
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void DrawScript()
        {
            EditorGUI.BeginDisabledGroup(true);

            // It can be a MonoBehaviour or a ScriptableObject
            var monoScript = (target as MonoBehaviour) != null
                ? MonoScript.FromMonoBehaviour((MonoBehaviour)target)
                : MonoScript.FromScriptableObject((ScriptableObject)target);

            EditorGUILayout.ObjectField("Script", monoScript, GetType(), false);

            EditorGUI.EndDisabledGroup();
        }

        IEnumerable<(int index, Rect handle_rect)> IterateRectForCommandRender()
        {
            //allocate rects for each command
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var rect = EditorGUILayout.GetControlRect();

                CalculateCommandRects(commands, i, rect, out var content_rect, out var handle_rect);
                this.commandEditor.CalculateCommandHeight(command, content_rect.width, out var content_height);

                rect.height = content_height + 4;
                EditorGUILayout.GetControlRect(GUILayout.Height(rect.height - EditorGUIUtility.singleLineHeight - 3f));
                yield return (i, rect);
            }
        }

        void DrawLine(List<SQCommandContainer> commands, int index, Rect rect)
        {
            var command = commands[index];
            Rect handle_rect, content_rect;

            DrawOutlineAndHandle(commands, index, rect, out content_rect, out handle_rect);

            if (MouseDownOnRect(handle_rect))
            {
                OnCommandMouseSelectHandle(Event.current, commands, index);
                GUI.FocusControl(null);
                return;
            }
            else if (MouseDownOnRect(rect)) OnCommandMouseSelected(Event.current, commands, index);

            var dot_rect = content_rect;
            dot_rect.x -= 6;
            dot_rect.width = 6;
            dot_rect.height = EditorGUIUtility.singleLineHeight;
            GUI.Label(dot_rect, ".");

            var is_current = index == selectingLineIndex;
            var return_pressed = KeyDown(KeyCode.Return);
            var is_editing = DrawLineContents(commands, index, content_rect);

            if (is_current && KeyDown(KeyCode.PageDown))
            {
                Event.current.Use();
                if (index + 1 >= commands.Count) AddLine(index + 1);
                this.SelectLine(index + 1, editing: true);
                return;
            }
            else if (is_current && KeyDown(KeyCode.PageUp))
            {
                Event.current.Use();
                if (index == commands.Count - 1 && IsLineEmpty(index)) RemoveLine(index);
                this.SelectLine(index - 1, true);
                return;
            }
            else if (is_editing && KeyDown(KeyCode.Escape))
            {
                Event.current.Use();
                GUI.FocusControl(null);
            }
            else if (is_editing && selectingLineIndex != index)
            {
                selectingLineIndex = index;
                selectingLines.Clear();
                selectingLines.Add(command);
            }
            else if (is_editing && return_pressed)
            {
                Event.current.Use();
                if (index + 1 >= commands.Count) AddLine(index + 1);
                this.SelectLine(index + 1, editing: true);
            }

            UpdateLineDrag(Event.current, commands, index, rect, handle_rect);
        }

        private bool DrawLineContents(List<SQCommandContainer> commands, int index, Rect rect)
        {
            var command = commands[index];
            var editing = commandEditor.RenderCommand(command, index, rect);
            return editing;
        }

        private void ClearSelectLine()
        {
            this.selectingLineIndex = -1;
            this.selectingLines.Clear();
            GUI.FocusControl(null);
        }

        private void SelectLine(int index, bool editing = false)
        {
            if (index < 0) index = 0;
            if (index > commands.Count) index = commands.Count;
            if (index < 0 || index >= commands.Count) index = -1;

            this.selectingLineIndex = index;
            this.selectingLines.Clear();
            if (index >= 0 && index < commands.Count) this.selectingLines.Add(commands[this.selectingLineIndex]);
            if (editing) commandEditor.FocusLine(this.selectingLineIndex);
            else GUI.FocusControl(null);

            EditorApplication.delayCall += () =>
            {
                if (editing) commandEditor.FocusLine(this.selectingLineIndex);
                else GUI.FocusControl(null);
                Repaint();
            };

            //set dirty the target 
            EditorUtility.SetDirty(Target);
            Repaint();
        }

        private void AddLine(int index, int indent = 0)
        {
            commands.Insert(index, new SQCommandContainer() { contents = "" });
            Repaint();
        }

        private void RemoveLine(int index)
        {
            if (index < 0 || index >= commands.Count) return;
            commands.RemoveAt(index);
            Repaint();
        }

        private void RemoveLines(HashSet<SQCommandContainer> lines)
        {
            lines = new HashSet<SQCommandContainer>(lines);
            foreach (var line in lines)
            {
                commands.Remove(line);
                this.selectingLines.Remove(line);
            }
            Repaint();
        }

        private void OnCommandMouseSelectHandle(Event evt, List<SQCommandContainer> commands, int index)
        {
            var command = commands[index];

            //if this nub is in selecting, do nothing
            if (selectingLines.Contains(command)) return;

            //if this nub is not in selecting, select it
            selectingLines.Clear();
            selectingLines.Add(command);
            this.selectingLineIndex = index;
            Repaint();
        }

        private void OnCommandMouseSelected(Event evt, List<SQCommandContainer> commands, int index)
        {
            var command = commands[index];
            if (EvtCommandOrControl(evt))
            {
                if (selectingLines.Contains(command))
                {
                    selectingLines.Remove(command);
                }
                else
                {
                    selectingLines.Add(command);
                }
            }
            else if (evt.shift)
            {
                var min = Mathf.Min(selectingLineIndex, index);
                var max = Mathf.Max(selectingLineIndex, index);
                if (min < 0) min = 0;
                if (max >= commands.Count) max = commands.Count - 1;
                for (var i = min; i <= max; i++) selectingLines.Add(commands[i]);
                selectingLines.Add(command);
            }
            else
            {
                selectingLines.Clear();
                selectingLines.Add(command);
            }

            this.selectingLineIndex = index;
            if (selectingLines.Count > 1)
            {
                Event.current.Use();
                GUI.FocusControl(null);
            }
            Repaint();
        }

        void UpdateLineDrag(Event evt, List<SQCommandContainer> commands, int index, Rect rect, Rect handle_rect)
        {
            rect.y -= 1;
            rect.height += 2;

            if (evt.type == EventType.MouseDrag && handle_rect.Contains(Event.current.mousePosition))
            {
                GUI.FocusControl(null);
                this.draggingSelectingGroup = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.StartDrag("DSLCommandMove");
                Event.current.Use();
            }

            if (this.draggingSelectingGroup == false || rect.Contains(Event.current.mousePosition) == false) return;

            var above_rect = rect;
            above_rect.height = 2;
            var below_rect = rect;
            below_rect.y += rect.height - 2;
            below_rect.height = 2;

            var x = Event.current.mousePosition.x;
            var y = Event.current.mousePosition.y;
            bool dropping_above = y < rect.y + rect.height / 2;
            bool dropping_below = y > rect.y + rect.height / 2;
            bool dropping_on = !dropping_above && !dropping_below;

            var color = new Color(0.4f, 0.5f, 1f, 1f);
            if (dropping_above) EditorGUI.DrawRect(above_rect, color);
            if (dropping_below) EditorGUI.DrawRect(below_rect, color);

            if (Event.current.type == EventType.DragUpdated && rect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                Event.current.Use();
            }

            if (Event.current.type == EventType.DragPerform)
            {
                draggingSelectingGroup = false;

                if (rect.Contains(Event.current.mousePosition) && !dropping_on)
                {
                    DragAndDrop.AcceptDrag();
                    Event.current.Use();
                    Repaint();
                    EditorApplication.delayCall += () =>
                    {
                        DropDraggingItems(index, commands, dropping_above ? -1 : 1);
                    };
                }
            }
        }

        void DropDraggingItems(int index, List<SQCommandContainer> commands, int position)
        {
            if (index < 0 || index >= commands.Count) return;
            var dragging_target_item = commands[index];
            var target_included_in_dragging = selectingLines.Contains(dragging_target_item);

            if (selectingLines.Contains(dragging_target_item)) selectingLines.Remove(dragging_target_item);

            var dragging_items = new List<SQCommandContainer>();
            for (var i = 0; i < commands.Count; i++)
            {
                if (selectingLines.Contains(commands[i])) dragging_items.Add(commands[i]);
            }

            //remove dragging items from commands
            foreach (var item in dragging_items)
            {
                commands.Remove(item);
            }

            index = commands.IndexOf(dragging_target_item);
            if (position > 0) index++;
            selectingLineIndex = index;

            //insert dragging items to commands
            foreach (var item in dragging_items)
            {
                commands.Insert(index, item);
                index++;
            }

            if (target_included_in_dragging) selectingLines.Add(dragging_target_item);

            Repaint();
        }

        bool CurrentEvtCommandOrControl => Event.current.control || Event.current.command;
        bool EvtCommandOrControl(Event evt) => evt.control || evt.command;

        bool KeyDown(KeyCode key)
        {
            return Event.current.type == EventType.KeyDown && Event.current.keyCode == key;
        }

        bool MouseDownOnRect(Rect rect)
        {
            return Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);
        }

        void DrawOutlineAndHandle(List<SQCommandContainer> commands, int index, Rect rect, out Rect content_rect, out Rect handle_rect)
        {
            var command = commands[index];
            EditorGUI.LabelField(rect, "", EditorStyles.helpBox);

            CalculateCommandRects(commands, index, rect, out content_rect, out handle_rect);

            var label_rect = handle_rect;
            label_rect.x += 6;
            label_rect.y += 2;
            //draw the icon
            //var icon = EditorGUIUtility.IconContent("align_vertically_center");
            GUI.Label(label_rect, (index + 1).ToString(), EditorStyles.miniBoldLabel);

            //if current content is selected
            if (selectingLines.Contains(command))
            {
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }
        }

        void CalculateCommandRects(List<SQCommandContainer> commmands, int index, Rect rect, out Rect content_rect, out Rect handle_rect)
        {
            var command = commands[index];

            //inset the rect by 5
            var icon_rect = rect;
            icon_rect.width = 45;
            handle_rect = icon_rect;

            var line_rect = rect;
            line_rect.x += icon_rect.width;
            line_rect.width -= icon_rect.width;
            line_rect.y += 2;
            line_rect.height -= 4;
            line_rect.x += 24 * command.indent;
            line_rect.width -= 24 * command.indent;

            content_rect = line_rect;
            handle_rect = icon_rect;
        }

        bool IsLineEmpty(int index)
        {
            if (index < 0 || index >= commands.Count) return true;
            return string.IsNullOrEmpty(commands[index].contents) && string.IsNullOrEmpty(commands[index].commandName);
        }
    }
}