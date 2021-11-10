using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

[CustomEditor(typeof(ScriptboundObject), true)]
public partial class ScriptboundObjectEditor : UnityEditor.Editor
{
    private Dictionary<string, MethodInfo> methodReflections;
    private Dictionary<string, string> methodDescriptions;
    private string defaultStringMethod = null;
    private System.Type[] defaultStringInjectableTypes = null;

    private string previewContent;
    private string previewMethods;
    private string controlName;
    private const int MIN_HEIGHT = 480;

    ScriptboundObject Target => (ScriptboundObject)target;

    private void OnEnable()
    {
        this.methodReflections = Target.ExtractMethodReflections();
        CheckForDefaultStringMethod();
        CachePreviewAPIs();
        CachePreviewContents();
        controlName = Target.GetHashCode().ToString();
    }

    void CheckForDefaultStringMethod()
    {
        defaultStringMethod = null;
        defaultStringInjectableTypes = null;

        foreach (var method in this.methodReflections.Values)
        {
            var prmts = method.GetParameters();
            if (prmts.Length != 1 || prmts[0].ParameterType != typeof(string)) continue;
            if (method.GetCustomAttribute(typeof(ScriptboundObject.Default), true) == null) continue;

            defaultStringMethod = method.Name;
            var injectable_types = (ScriptboundObject.StringInjectible) method.GetCustomAttribute(typeof(ScriptboundObject.StringInjectible), true);
            if (injectable_types != null) this.defaultStringInjectableTypes = injectable_types.types;

            return;
        }
    }

    public override void OnInspectorGUI()
    {
        SetupStyles();
        DrawScriptReference();

        DrawAPI();

        GUILayout.Space(16);

        if (preview_showing)
            DrawPreview();
        else
            DrawEditor();
    }

    static Color default_cursor_color;

    private void SetupStyles()
    {
        if (editor_style == null)
        {
            editor_style = new GUIStyle(EditorStyles.textArea);
            editor_style.wordWrap = false;
            default_cursor_color = GUI.skin.settings.cursorColor;
        }

        if (preview_style == null)
        {
            preview_style = new GUIStyle(EditorGUIUtility.isProSkin ? EditorStyles.whiteLabel : EditorStyles.label);
            preview_style.richText = true;
            preview_style.wordWrap = false;
        }
    }

    void CachePreviewAPIs()
    {
        previewMethods = "";
        foreach (var key in this.methodReflections.Keys)
        {
            var method = this.methodReflections[key];
            var methodInfoStr = "";
            methodInfoStr += key + " (";

            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                if (param != parameters[0]) methodInfoStr += ", ";
                methodInfoStr += "<b>" + param.ParameterType.Name + "</b> " + param.Name;
            }

            methodInfoStr += ")";

            if (method.ReturnType != typeof(void))
                methodInfoStr += " -> <b>" + method.ReturnType.Name + "</b>";

            previewMethods += methodInfoStr + "\n";
        }
        previewMethods = previewMethods.Substring(0, previewMethods.Length - 1);
    }

    void SwitchToEditMode()
    {
        GUI.FocusControl("");
        editing_contents = GetEditContents();
        preview_showing = false;
        Repaint();
    }

    void SwitchFromEditMode(bool parse)
    {
        if (parse == false) preview_showing = true;
        else
        {
            try
            {
                ParseInstructions(Target, editing_contents);
                CachePreviewContents();
                preview_showing = true;
                EditorUtility.SetDirty(Target);
            }
            catch (Exception e) { Debug.LogException(e, this); }
            Repaint();
        }
    }

    void CachePreviewContents()
    {
        previewContent = GetPreviewContents();
    }

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW SCRIPT REFERENCE
    #region DrawScriptReference
    void DrawScriptReference()
    {
        var e = GUI.enabled;
        GUI.enabled = false;
        SerializedProperty prop = serializedObject.FindProperty("m_Script");
        EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
        GUI.enabled = e;
    }
    #endregion

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW API
    static bool api_showing = false;
    static GUIStyle api_style = null;
    static GUIStyle method_hint_style = null;
    #region DrawAPI

    void DrawAPI()
    {
        if (api_style == null)
        {
            api_style = new GUIStyle(EditorGUIUtility.isProSkin ? EditorStyles.whiteMiniLabel : EditorStyles.miniLabel); api_style.richText = true;
            api_style.wordWrap = true;
        }

        api_showing = EditorGUILayout.Foldout(api_showing, "Code reference", true);
        if (api_showing)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(previewMethods, api_style);
            EditorGUILayout.EndVertical();
        }

        if (method_hint_style == null)
        {
            method_hint_style = new GUIStyle(GUI.skin.label);
            method_hint_style.richText = true;
        }
    }
    #endregion

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW PREVIEW
    bool preview_showing = true;
    static GUIStyle preview_style = null;
    Vector2 scrollPosition, oldScrollPosition;
    void DrawPreview()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preview contents", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        bool editPressed = GUILayout.Button("Edit", EditorStyles.miniButton);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(" ");
        EditorGUILayout.LabelField(" ");

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(MIN_HEIGHT));
        GUILayout.Label(previewContent, preview_style, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        var last_rect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && last_rect.Contains(Event.current.mousePosition))
            SwitchToEditMode();
        else if (editPressed)
            SwitchToEditMode();
    }

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW EDITOR
    string editing_contents = "";
    bool hint_popup = false;
    int hint_cursor_position = -1;
    bool hint_popup_completed = false;
    string hint_result_input = null;
    UnityEngine.Object hint_result_input_object = null;
    int hint_popup_completed_frameskip = 2;
    Rect recent_editor_scrol_rect = new Rect();
    Rect hint_rect_position = new Rect();
    Vector2 recent_hint_cursor_position;
    private FieldInfo cachedSelectAllField;
    int editorControlID = -1;
    private int recentControlID;
    static GUIStyle editor_style = null;
    Vector2 recentTextEditorCursorPos = Vector2.zero;
    string recentTextEditorFunctionHint = "";
    string recentTextEditorObjectHint = "";

    Rect recent_editor_rect = new Rect();

    void DrawEditor()
    {

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Edit contents", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        bool cancelPressed = GUILayout.Button("Cancel", EditorStyles.miniButton);
        bool savePressed = GUILayout.Button("Save", EditorStyles.miniButton);
        EditorGUILayout.EndHorizontal();

        //disable select all on mouse up
        //Disable selecting all on mouse up 
        if (cachedSelectAllField == null)
            cachedSelectAllField = typeof(EditorGUI).GetField("s_SelectAllOnMouseUp", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Default);
        cachedSelectAllField.SetValue(null, false);

        //var text_area_rect = EditorGUILayout.GetControlRect(false, 640);

        var focusing = GUI.GetNameOfFocusedControl() == controlName;
        var keydown = Event.current.type == EventType.KeyDown;
        var keyshift = Event.current.shift;
        var keycode = Event.current.keyCode;
        var keychar = Event.current.character;
        var keyctrl = Event.current.control || Event.current.command;
        var keycursor = keycode == KeyCode.UpArrow || keycode == KeyCode.DownArrow || keycode == KeyCode.LeftArrow || keycode == KeyCode.RightArrow;
        var keypg = keycode == KeyCode.Home || keycode == KeyCode.End || keycode == KeyCode.PageUp || keycode == KeyCode.PageDown;
        var keynav = keycursor || keypg;

        var tabbing = (keydown && keycode == KeyCode.Tab) || keychar == '\t';
        var entering = (keydown && keycode == KeyCode.Return) || keychar == '\n';
        var escaping = keydown && keycode == KeyCode.Escape;
        var hinting = keyctrl && keychar == ' ';
        var undo = ((keydown && keycode == KeyCode.Z) || keychar == 'z') && keyctrl;

        //block input from entering the text here
        if (hint_popup == false)
            if (undo || hinting || entering)
            {
                if (Event.current.type == EventType.Layout) return;
                Event.current.Use();
            }

        #region Block input and update hinting
        #endregion

        //check for exit editting
        #region Check for exit editting
        if (focusing && escaping)
        {
            var esc_not_saving = Event.current.shift;
            SwitchFromEditMode(esc_not_saving == false);
            Event.current.Use();
        }
        else if (savePressed)
            SwitchFromEditMode(true);
        else if (cancelPressed)
            SwitchFromEditMode(false);
        if (preview_showing)
        {
            EditorGUILayout.EndVertical();
            return;
        }
        #endregion

        TextEditor tEditor;
        #region Render TextEditor

        {
            var bgc = GUI.backgroundColor;
            try
            {
                EditorGUILayout.LabelField(recentTextEditorFunctionHint, method_hint_style);
                EditorGUILayout.LabelField(recentTextEditorObjectHint, method_hint_style);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(MIN_HEIGHT));
            }
            catch { }
            GUI.SetNextControlName(controlName);
            try
            {
                GUI.backgroundColor = Color.clear;
                if (recent_editor_rect.height < MIN_HEIGHT + 1)
                    editing_contents = EditorGUILayout.TextArea(editing_contents, GUILayout.ExpandWidth(true), GUILayout.MinHeight(MIN_HEIGHT));
                else
                    editing_contents = EditorGUILayout.TextArea(editing_contents, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint) recent_editor_rect = GUILayoutUtility.GetLastRect();
                GUI.backgroundColor = bgc;
                GUILayout.FlexibleSpace();
            } catch { }
            EditorGUILayout.EndScrollView();
        }
        tEditor = typeof(EditorGUI)
            .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as TextEditor;
        if (tEditor != null && tEditor.graphicalCursorPos != recentTextEditorCursorPos)
        {
            recentTextEditorCursorPos = tEditor.graphicalCursorPos;
            UpdateCursorScroll();
            ExtractCurrentLineFunctionHint(tEditor);
            if (recentTextEditorFunctionHint.Length <= 0) ExtractDefaultLineObjectHint(tEditor);
            else ExtractCurrentLineObjectHint(tEditor);
        }

        var keyboardControlID = EditorGUIUtility.GetControlID(FocusType.Keyboard);
        if (tEditor != null && tEditor.controlID == keyboardControlID - 1)
        {
            tEditor.scrollOffset = Vector2.zero; //no scroll offset
            //sync in case of deselect
            if (tEditor.text != editing_contents) editing_contents = tEditor.text;
        }

        if (Event.current.type == EventType.Repaint) recent_editor_scrol_rect = GUILayoutUtility.GetLastRect();
        #endregion

        //enable hints here
        hint_popup = false;
        #region Create suggestion popup here
        if (focusing && hinting)
        {
            hint_popup = true;
            hint_popup_completed = false;
            hint_cursor_position = tEditor.cursorIndex;
            hint_rect_position = recent_editor_scrol_rect;
            hint_rect_position.y = 0;
            hint_rect_position.min += tEditor.graphicalCursorPos - tEditor.scrollOffset;
            hint_rect_position.width = 100;
            hint_rect_position.height = 100;
            recent_hint_cursor_position = tEditor.graphicalCursorPos;
            oldScrollPosition = scrollPosition;

            // Get the context here, depends on the context to create suggestion
            var (line_mod, line_method, line_index, line_var) = ExtractCurrentLineContext(tEditor);
            if (methodReflections.ContainsKey(line_method) == false && EditingDefaultLineInjectible(tEditor))
                CreateDefaultLineObjectInjectibleSuggestion(tEditor);
            else
                CreateSuggestionPopup(line_mod, line_method, line_index, line_var);
        }
        #endregion

        #region Render suggestion popup here
        #endregion

        //collect data from hints here
        #region Collect returning text from hint popup
        if (hint_popup_completed)
        {
            hint_popup = false;
            hint_popup_completed = false;
            hint_popup_completed_frameskip = 2;
            GUI.skin.settings.cursorColor = new Color(0, 0, 0, 0);
            if (tEditor == null) EditorGUI.FocusTextInControl(controlName);
        }

        if (hint_popup_completed_frameskip > 0 && tEditor != null)
        {
            GUI.skin.settings.cursorColor = default_cursor_color;
            tEditor.SelectNone();
            tEditor.cursorIndex = hint_cursor_position;
            hint_popup_completed_frameskip--;
            if (hint_popup_completed_frameskip == 0)
            {
                if (hint_result_input != null)
                {
                    var hint_result = hint_result_input;
                    if (hint_result_input_object != null) hint_result = ObjectToString(hint_result_input_object);
                    AutoCompleteEditingContents(tEditor, hint_cursor_position, hint_result);
                }
                tEditor.selectIndex = tEditor.cursorIndex;
                scrollPosition = oldScrollPosition;
            }
            Repaint();
        }
        #endregion

        if (focusing && keydown)
        {
            // UNDOING
            //if (focusing && undo) try { tEditor.Undo(); } catch { } // undo does not work and full of bugs, might as well disable it
            if (entering) InputNewLineWithIndents(tEditor);

            // PGUP & PGDOWN
            if (keycode == KeyCode.PageUp) for (var i = 0; i < 10; i++) if (keyshift) tEditor.SelectUp(); else tEditor.MoveUp();
            if (keycode == KeyCode.PageDown) for (var i = 0; i < 10; i++) if (keyshift) tEditor.SelectDown(); else tEditor.MoveDown();
            if (keyctrl && keycode == KeyCode.Home) if (keyshift) tEditor.SelectTextStart(); else tEditor.MoveTextStart();
            if (keyctrl && keycode == KeyCode.End) if (keyshift) tEditor.SelectTextEnd(); else tEditor.MoveTextEnd();
            if (keynav) Repaint();
        }

        //if (hint_popup)
        //{
        //    EditorGUI.DrawRect(hint_rect_position, Color.red);
        //}

        EditorGUILayout.EndVertical();
    }

    private void UpdateCursorScroll()
    {
        var r = recent_editor_scrol_rect;
        var cursor = recentTextEditorCursorPos;
        var scroll = scrollPosition;

        if (cursor.y - scroll.y < 0) scroll.y = cursor.y;
        if (cursor.y - scroll.y + 32 > r.height) scroll.y = cursor.y + 32 - r.height;
        if (cursor.x - scroll.x < 0) scroll.x = cursor.x;
        if (cursor.x - scroll.x + 32 > r.width) scroll.x = cursor.x + 32 - r.width;

        scrollPosition = scroll;
    }

    bool hintingObjectField = false;
    private void ExtractCurrentLineFunctionHint(TextEditor te)
    {
        var (control, instruction, index, value) = ExtractCurrentLineContext(te);
        if (instruction == null || methodReflections.ContainsKey(instruction) == false)
        {
            recentTextEditorFunctionHint = "";
            return;
        }

        var method = methodReflections[instruction];
        var hint = method.Name + " (";
        var parameters = method.GetParameters();
        var i = 0;
        var hinting_object = false;
        foreach (var param in parameters)
        {
            if (param != parameters[0]) hint += ", ";
            var param_hint = param.ParameterType.Name + " " + param.Name;
            if (i == index) param_hint = "<b>" + param_hint + "</b>";
            if (i == index && (typeof(UnityEngine.Object).IsAssignableFrom(parameters[i].ParameterType) || typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(parameters[i].ParameterType))) hinting_object = true;
            hint += param_hint;
            i++;
        }

        hint += ")";

        if (method.ReturnType != typeof(void))
            hint += " -> " + method.ReturnType.Name;

        hintingObjectField = hinting_object;
        recentTextEditorFunctionHint = hint;
    }

    private void ExtractCurrentLineObjectHint(TextEditor te)
    {
        if (hintingObjectField == false)
        {
            recentTextEditorObjectHint = "";
            return;
        }

        var o = StringToObject(ExtractCurrentField(te));
        if (o == null)
        {
            recentTextEditorObjectHint = "";
            return;
        }


        recentTextEditorObjectHint = AssetDatabase.GetAssetPath(o) + ":" + o.ToString();
    }

    private void ExtractDefaultLineObjectHint(TextEditor te)
    {
        recentTextEditorObjectHint = "";
        if (EditingDefaultLineInjectible(te) == false) return;
        recentTextEditorObjectHint = ExtractDefaultLineInjectible(te);
    }

    void AutoCompleteEditingContents(TextEditor te, int cursor_index, string content)
    {
        //take explicit scenario out first
        if (cursor_index == 0)
        {
            editing_contents = editing_contents.Insert(cursor_index, content);
            te.text = editing_contents;
            te.cursorIndex += content.Length;
            return;
        }

        char starting_char = '?';
        int start_cursor_index = cursor_index;
        bool starting_char_break = false;

        do
        {
            start_cursor_index--;
            starting_char = start_cursor_index < 0 ? ' ' : editing_contents[start_cursor_index];
            starting_char_break = starting_char == ' ' || starting_char == '\n' || starting_char == '\t' || starting_char == ':' || starting_char == ',' || starting_char == '[' || starting_char == ']';
        }
        while (start_cursor_index >= 0 && starting_char_break == false);

        start_cursor_index++;
        var remove_len = cursor_index - start_cursor_index;
        editing_contents = editing_contents.Remove(start_cursor_index, remove_len);
        editing_contents = editing_contents.Insert(start_cursor_index, content);
        te.text = editing_contents;
        te.cursorIndex += content.Length - remove_len;
    }

    void CreateSuggestionPopup(string line_mod, string line_method, int line_index, string line_var)
    {
        //Debug.Log(string.Format("{0} - {1} - {2} - {3}", line_mod, line_method, line_index, line_var));
        hint_result_input = null;
        hint_result_input_object = null;

        ScriptboundObjectEditorHintPopup popup = null;

        if (line_index <= -1) popup = new ScriptboundObjectEditorHintPopup(methodReflections, line_var);
        if (line_index >= 0)
        {
            if (line_method == null) return;
            if (methodReflections.ContainsKey(line_method) == false) return;
            var method = methodReflections[line_method];
            var parameters = method.GetParameters();
            if (line_index >= parameters.Length) return;
            var param_type = parameters[line_index].ParameterType;
            if (param_type.IsPrimitive || param_type == typeof(string)) return;

            if (typeof(UnityEngine.Object).IsAssignableFrom(param_type) && StringToObject(line_var) != null)
                popup = new ScriptboundObjectEditorHintPopup(param_type, StringToObject(line_var));
            else popup = new ScriptboundObjectEditorHintPopup(param_type, line_var);
        }

        popup.callbackClose += () =>
        {
            hint_popup_completed = true;
        };
        popup.callbackInput += (result) =>
        {
            hint_result_input = result;
            hint_popup_completed = true;
        };
        popup.callbackInputObject += (str, result) =>
        {
            hint_result_input = str;
            hint_result_input_object = result;
            hint_popup_completed = true;
        };
        PopupWindow.Show(hint_rect_position, popup);
    }

    private void CreateDefaultLineObjectInjectibleSuggestion(TextEditor tEditor)
    {
        if (defaultStringInjectableTypes == null) return;

        hint_result_input = null;
        hint_result_input_object = null;

        ScriptboundObjectEditorHintPopup popup = null;

        var line_var = ExtractDefaultLineInjectibleBeforeCursor(tEditor);
        //TODO: ADD MORE SUPPORT FOR MULTIPLE TYPES
        popup = new ScriptboundObjectEditorHintPopup(new List<System.Type>(defaultStringInjectableTypes), line_var);

        popup.callbackClose += () =>
        {
            hint_popup_completed = true;
        };
        popup.callbackInput += (result) =>
        {
            hint_result_input = result;
            hint_popup_completed = true;
        };
        popup.callbackInputObject += (str, result) =>
        {
            hint_result_input = str;
            hint_result_input_object = result;
            hint_popup_completed = true;
        };
        PopupWindow.Show(hint_rect_position, popup);
    }

}