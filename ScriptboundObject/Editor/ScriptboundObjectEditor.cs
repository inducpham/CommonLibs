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
    private string previewContent;
    private string previewAPIs;
    private string controlName;
    private const int MIN_HEIGHT = 480;

    ScriptboundObject Target => (ScriptboundObject)target;

    private void OnEnable()
    {
        this.methodReflections = Target.ExtractMethodReflections();
        CachePreviewAPIs();
        CachePreviewContents();
        controlName = Target.GetHashCode().ToString();
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
        previewAPIs = "";
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

            previewAPIs += methodInfoStr + "\n";
        }
        previewAPIs = previewAPIs.Substring(0, previewAPIs.Length - 1);
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
            GUILayout.Label(previewAPIs, api_style);
            EditorGUILayout.EndVertical();
        }
    }
    #endregion

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW PREVIEW
    bool preview_showing = true;
    static GUIStyle preview_style = null;
    Vector2 previewScrollPosition;
    void DrawPreview()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preview contents", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        bool editPressed = GUILayout.Button("Edit", EditorStyles.miniButton);
        EditorGUILayout.EndHorizontal();

        previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(MIN_HEIGHT));
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

    private Vector2 scrollPosition;

    ///////////////////////////////////////////////////////////////
    /// SECTION DRAW EDITOR
    string editing_contents = "";
    bool hint_popup = false;
    int hint_cursor_position = -1;
    bool hint_popup_completed = false;
    string hint_result_input = null;
    int hint_popup_completed_frameskip = 2;
    Rect recent_text_editor_rect = new Rect();
    Rect hint_rect_position = new Rect();
    Vector2 recent_hint_cursor_position;
    private FieldInfo cachedSelectAllField;
    int editorControlID = -1;
    private int recentControlID;
    static GUIStyle editor_style = null;

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
                #pragma warning disable
                Event.current.Use();
                #pragma warning restore
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
            GUI.backgroundColor = Color.clear;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(MIN_HEIGHT));
            GUI.SetNextControlName(controlName);
            try {
                editing_contents = EditorGUILayout.TextArea(editing_contents, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUILayout.FlexibleSpace();
            } catch { }
            EditorGUILayout.EndScrollView();
            GUI.backgroundColor = bgc;
        }
        tEditor = typeof(EditorGUI)
            .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as TextEditor;

        var keyboardControlID = EditorGUIUtility.GetControlID(FocusType.Keyboard);
        if (tEditor != null && tEditor.controlID == keyboardControlID - 1)
        {
            tEditor.scrollOffset = Vector2.zero; //no scroll offset
            //sync in case of deselect
            if (tEditor.text != editing_contents) editing_contents = tEditor.text;
        }

        if (Event.current.type == EventType.Repaint) recent_text_editor_rect = GUILayoutUtility.GetLastRect();
        #endregion

        //enable hints here
        hint_popup = false;
        #region Create suggestion popup here
        if (focusing && hinting)
        {
            hint_popup = true;
            hint_popup_completed = false;
            hint_cursor_position = tEditor.cursorIndex;
            hint_rect_position = recent_text_editor_rect;
            hint_rect_position.y = 0;
            hint_rect_position.min += tEditor.graphicalCursorPos - tEditor.scrollOffset;
            hint_rect_position.width = 100;
            hint_rect_position.height = 100;
            recent_hint_cursor_position = tEditor.graphicalCursorPos;

            // Get the context here, depends on the context to create suggestion
            var (line_mod, line_method, line_index, line_var) = ExtractCurrentLineContext(tEditor);
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
                    editing_contents = editing_contents.Insert(hint_cursor_position, hint_result);
                    tEditor.text = editing_contents;
                    tEditor.cursorIndex += hint_result.Length;
                }
                tEditor.selectIndex = tEditor.cursorIndex;
                Repaint();
            }
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

    void CreateSuggestionPopup(string line_mod, string line_method, int line_index, string line_var)
    {
        //Debug.Log(string.Format("{0} - {1} - {2} - {3}", line_mod, line_method, line_index, line_var));
        hint_result_input = null;

        ScriptboundObjectEditorHintPopup popup = null;

        if (line_index == -1) popup = new ScriptboundObjectEditorHintPopup(methodReflections, line_var);
        if (line_index >= 0)
        {
            if (line_method == null) return;
            if (methodReflections.ContainsKey(line_method) == false) return;
            var method = methodReflections[line_method];
            var parameters = method.GetParameters();
            if (line_index >= parameters.Length) return;
            var param_type = parameters[line_index].ParameterType;
            if (param_type.IsPrimitive || param_type == typeof(string)) return;

            popup = new ScriptboundObjectEditorHintPopup(parameters[line_index].ParameterType, line_var);
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
        PopupWindow.Show(hint_rect_position, popup);
    }

}