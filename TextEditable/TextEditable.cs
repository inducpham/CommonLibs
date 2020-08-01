using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TextEditable
{
    public class PropertyEditable : PropertyAttribute { }
    
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class HideDefault : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class DefaultStringField : System.Attribute { }

    class Hint
    {
        public string value;
        public int weight;
        public bool matched;
        public string content;
    }

#if UNITY_EDITOR
    public class CustomTextEditorWindow : EditorWindow
    {
        static CustomTextEditorWindow instance = null;
        System.Object target;
        Rect basePosition, hintPosition, textPosition, statusPosition;
        bool hintEnabled = false;
        TextEditor textEditor = null;
        bool keyEventUsed = false;
        bool skipNextNoneKey = false;
        string editContent;
        string propertyPath;
        Vector2 currentCursor = Vector2.zero;
        string statusString = "";

        Dictionary<string, string> fieldTypeNames;
        string defaultStringField;
        int hintStartCursor = -1;
        int startFieldHintCursor, startValueHintCursor;
        bool hintingField, hintingValue;
        int filteredHintCount = 0;

        SerializedProperty property;

        static GUIStyle styleHint, styleHintSelected;

        public static void Show(Rect r, SerializedProperty prop)
        {
            if (instance == null)
                instance = (CustomTextEditorWindow)EditorWindow.CreateInstance<CustomTextEditorWindow>();

            //instance.ShowAsDropDown(r, r.size);
            //instance.ShowUtility();
            r.y -= 16;
            r.height = 360;
            r.xMax += 1;
            var screen_rect = Screen.safeArea;
            if (r.yMax > screen_rect.yMax)
                r.y -= (r.yMax - screen_rect.yMax - 20);

            instance.target = (System.Object)prop.GetValue();
            instance.property = prop;
            instance.Enable();
            instance.ShowPopup();
            instance.basePosition = r;
            instance.position = r;
            instance.Focus();

            if (styleHint == null)
            {
                styleHint = new GUIStyle(EditorStyles.label);
                styleHintSelected = new GUIStyle(EditorStyles.label);
                Texture2D tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.blue); tex.Apply();
                styleHintSelected.normal.background = tex;
                styleHintSelected.normal.textColor = Color.white;
            }

        }

        private void Enable()
        {
            //LOAD FROM EDITING OBJECT
            this.editContent = target.DefaultToEditorContent(); // target.ToEditorContent();
            this.fieldTypeNames = target.GetFieldTypeNames();
            this.defaultStringField = target.DefaultStringField();
        }

        private void OnDisable()
        {
            //SAVE TO EDITING OBJECT
            target.DefaultFromEditorContent(this.editContent);
            // target.FromEditorContent(this.editContent);
            CustomTextEditor.RemapSerializedPropertyContent(this.property);
        }

        void UseCurrentKeyEvent()
        {
            keyEventUsed = true;
            skipNextNoneKey = true;
        }

        void RecalculatePositions()
        {
            var hintEnalbed = this.hintEnabled;
            var basePosition = this.basePosition;
            var safeScreen = Screen.safeArea;

            var position = basePosition;
            this.textPosition = new Rect(0, 0, position.width, position.height);
            this.statusPosition = this.textPosition;
            this.textPosition.yMin += 16;
            this.statusPosition.yMax = this.statusPosition.yMin + 16;

            if (hintEnabled)
            {
                var pop_left = this.basePosition.center.x > (Screen.currentResolution.width / 2);
                if (pop_left)
                {
                    position.xMin -= 360;
                    this.hintPosition = new Rect(0, 0, 360, basePosition.height);
                    this.textPosition.x += 360;
                    this.statusPosition.x += 360;
                }
                else
                {
                    position.xMax += 360;
                    this.hintPosition = new Rect(basePosition.width, 0, 360, basePosition.height);
                }
            }

            this.position = position;
        }

        void ReloadStatusString()
        {
            (var line, var cursor) = this.textEditor.ExtractCurrentLine();

            string currentField = line.ExtractCurrentField();
            if (this.fieldTypeNames.ContainsKey(currentField))
                this.statusString = this.fieldTypeNames[currentField];
            else if (defaultStringField != null)
                this.statusString = defaultStringField + ": default string";
            else
                this.statusString = "";
        }

        List<Hint> hints = new List<Hint>();

        void SetupHintSuggestion()
        {
            this.currentHintIndex = 0;
            (var line, var cursor) = this.textEditor.ExtractCurrentLine();
            this.hintingField = line.CanHintField(cursor);
            this.hintingValue = line.CanHintValue(cursor);

            hints.Clear();
            if (this.hintingField) {
                foreach (var field in this.fieldTypeNames.Keys)
                    hints.Add(new Hint() { value = field, content = this.fieldTypeNames[field] + " : " + field });
            } else if (this.hintingValue)
            {
                var hint_values = this.target.DefaultGetFieldValueHints(line.ExtractCurrentField());
                if (hint_values != null)
                    foreach (var hint_value in hint_values)
                        hints.Add(new Hint() { value = hint_value, content = hint_value });
            }

            if (hints.Count <= 0)
            {
                this.DisableHinting();
                return;
            }

            this.ResortHintSuggestion();
        }

        void ResortHintSuggestion()
        {
            if (this.hintEnabled == false) return;

            (var cursor, var endCursor) = this.textEditor.text.GetCurrentWordWrap(textEditor.cursorIndex);
            var current_word = this.textEditor.text.Substring(cursor, endCursor - cursor);

            this.filteredHintCount = 0;
            foreach (var hint in this.hints)
            {
                FuzzyFinder.FuzzyMatcher.FuzzyMatch(current_word, hint.content, out hint.weight);
                hint.matched = hint.content.MatchSearchString(current_word);
                if (hint.matched) this.filteredHintCount++;
            }
            this.hints.Sort((h2, h1) => h1.weight.CompareTo(h2.weight));
        }

        void UpdateHinting()
        {
            if (this.hintEnabled == false)
            {
                this.hintStartCursor = -1;
                return;
            }

            (var line, var cursor) = this.textEditor.ExtractCurrentLine();
            var canHintField = line.CanHintField(cursor);
            var canHintValue = line.CanHintValue(cursor);
            var hintStartCursor = 0;

            if (canHintField) hintStartCursor = line.StartOfHintField(cursor);
            else if (canHintValue) hintStartCursor = line.StartOfHintValue(cursor);
            else
            {
                DisableHinting();
                return;
            }

            if (hintStartCursor == this.hintStartCursor && this.hintStartCursor >= 0)
            { //RESORT HINT SUGGESTION
                this.ResortHintSuggestion();
            }
            else if (hintStartCursor != this.hintStartCursor && this.hintStartCursor >= 0)
            {
                //DISABLE HINTING
                this.DisableHinting();
            } else
            { //SETUP HINT SUGGESTION
                this.hintStartCursor = hintStartCursor;
                this.SetupHintSuggestion();
            }
        }

        void OnCursorChange()
        {
            ReloadStatusString();
            UpdateHinting();
        }

        private void EnableHinting()
        {
            hintEnabled = true;
            UpdateHinting();
        }

        private void DisableHinting()
        {
            this.hintStartCursor = -1;
            hintEnabled = false;
        }

        int currentHintIndex = 0;
        private void UpdateHintingInput()
        {
            if (!hintEnabled) return;
            bool up = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.UpArrow;
            bool down = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.DownArrow;
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
            if (up || down || enter) UseCurrentKeyEvent();
            if (this.filteredHintCount <= 0) return;

            if (up) this.currentHintIndex--;
            if (down) this.currentHintIndex++;
            if (enter)
            {
                this.ApplyCurrentHint();
                this.DisableHinting();
                return;
            }
            this.currentHintIndex = (this.currentHintIndex + this.filteredHintCount) % this.filteredHintCount;

            var pos = hintPosition;
            pos.size -= Vector2.one * 4;
            pos.position += Vector2.one * 2;
            var max_hint_count = pos.height / 16;
            pos.height = 16;

            var i = 0;
            foreach (var hint in this.hints) {
                if (i >= max_hint_count) break;
                if (hint.matched == false) continue;
                GUI.Label(pos, hint.content, i == this.currentHintIndex ? styleHintSelected : styleHint);
                pos.y += pos.height;
                i++;
            }
        }

        private void ApplyCurrentHint()
        {
            if (this.hintEnabled == false) return;
            var text = textEditor.text;

            Hint currentHint = null;
            var currentHintIndex = this.currentHintIndex;
            foreach (var hint in this.hints)
            {
                if (hint.matched == false) continue;
                if (currentHintIndex <= 0)
                {
                    currentHint = hint;
                    break;
                }
                currentHintIndex--;
            }
            (var cursor, var endCursor) = text.GetCurrentWordWrap(textEditor.cursorIndex);
            text = text.Remove(cursor, endCursor - cursor).Insert(cursor, currentHint.value);
            this.editContent = this.textEditor.text = text;
            this.textEditor.cursorIndex = this.textEditor.selectIndex = cursor + currentHint.value.Length;
        }

        private void UpdateStatus(Rect position)
        {
            EditorGUI.LabelField(position, this.statusString, EditorStyles.miniBoldLabel);
        }

        private void OnSelectionChange()
        {
            this.Close();
        }

        private void OnGUI()
        {
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive) return;
            this.Focus();

            RecalculatePositions();

            if (target == null)
            {
                Close();
                return;
            }

            bool closing = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            bool scrolling = Event.current.type == EventType.ScrollWheel;
            bool pageup = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.PageUp;
            bool pagedown = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.PageDown;
            bool hinting = Event.current.type == EventType.KeyDown && Event.current.control && (Event.current.keyCode == KeyCode.Space);

            if (closing || scrolling || pageup || pagedown || hinting) UseCurrentKeyEvent();

            this.UpdateHintingInput();

            if (scrolling) textEditor.scrollOffset += Event.current.delta * 10;
            if (pageup) textEditor.MoveCursorToPosition(textEditor.graphicalCursorPos - new Vector2(0, position.height / 2));
            if (pagedown) textEditor.MoveCursorToPosition(textEditor.graphicalCursorPos + new Vector2(0, position.height / 2));
            if (hinting) this.EnableHinting();
            if (closing)
                if (this.hintEnabled)
                {
                    this.DisableHinting();
                    skipNextNoneKey = false; //HACK
                }
                else this.Close();



            if (keyEventUsed) { Event.current.Use(); keyEventUsed = false; return; }
            if (skipNextNoneKey && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.None) { Event.current.Use(); skipNextNoneKey = false; return; }

            #region Render TextEditor
            GUI.SetNextControlName("te");
            try
            {
                GUI.TextArea(this.textPosition, this.editContent); //, EditorStyles.helpBox);
            }
            catch { this.target = null; }

            EditorGUI.FocusTextInControl("te");

            this.textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            textEditor.multiline = true;

            this.UpdateStatus(this.statusPosition);
            #endregion

            #region Check cursor position change
            if (currentCursor != textEditor.graphicalCursorPos || this.editContent.Length != textEditor.text.Length)
            {
                if (currentCursor.y != textEditor.graphicalCursorPos.y) DisableHinting();
                currentCursor = textEditor.graphicalCursorPos;
                this.OnCursorChange();
            }
            #endregion

            this.editContent = textEditor.text;
        }

    }

    [CustomPropertyDrawer(typeof(PropertyEditable))]
    public class CustomTextEditor : PropertyDrawer
    {
        static bool selectionChangedMapped = false;
        static Dictionary<string, string> propertyContentMap = new Dictionary<string, string>();

        static string defaultContent = "AttributeTextEditable target class must implements interface TextEditable";
        static GUIStyle style;

        static GUIStyle GetStyle()
        {
            if (style != null) return style;
            style = new GUIStyle(EditorStyles.helpBox);
            style.fontSize = 11;
            style.richText = true;
            style.padding = new RectOffset(6, 6, 6, 6);
            style.normal.textColor = new Color(0.2f, 0.2f, 0.2f); // Color.black;
            return style;
        }

        public static void RemapSerializedPropertyContent(SerializedProperty property)
        {
            var content = defaultContent;
            var obj = property.GetValue();
            if (obj == null) return;
            content = obj.DefaultToEditorContent(); //.ToEditorContent();
            propertyContentMap[property.propertyPath] = content;
            property.serializedObject.ApplyModifiedProperties();

            foreach (var o in property.serializedObject.targetObjects) EditorUtility.SetDirty(o);
        }

        static string MapSerializedPropertyContent(SerializedProperty property)
        {
            if (!selectionChangedMapped)
            {
                Selection.selectionChanged += () => propertyContentMap.Clear();
                selectionChangedMapped = true;
            }

            if (propertyContentMap.ContainsKey(property.propertyPath) == false)
            {
                var content = defaultContent;

                var obj = property.GetValue();
                if (obj == null) return "";
                content = obj.DefaultToEditorContent(); // .ToEditorContent();

                propertyContentMap[property.propertyPath] = content;
            }

            return propertyContentMap[property.propertyPath];
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetStyle().CalcHeight(new GUIContent(MapSerializedPropertyContent(property)), EditorGUIUtility.currentViewWidth) + EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var content = MapSerializedPropertyContent(property);

            var c = GUI.color;
            GUI.color = Color.clear;
            var show_editor = GUI.Button(position, "");
            GUI.color = c;

            var prop = property.FindPropertyRelative("content");
            var labelPosition = position;
            labelPosition.height = EditorGUIUtility.singleLineHeight;
            position.height -= labelPosition.height;
            position.y += labelPosition.height;
            EditorGUI.LabelField(labelPosition, label);
            EditorGUI.LabelField(position, content, GetStyle());

            if (show_editor)
            {
                position.center = EditorGUIUtility.GUIToScreenPoint(position.center);
                position.y -= 1;
                CustomTextEditorWindow.Show(position, property);
            }
        }
    }
#endif
}