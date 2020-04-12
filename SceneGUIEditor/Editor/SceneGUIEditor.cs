using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class SceneGUIEditor : UnityEditor.Editor
{

    protected bool leftClicked, leftReleased, rightClicked, rightReleased, doubleClicked, clicked, mouseInside, keyPressed, dragging, draggingRight, contextClicked;
    protected Vector2 clickWorldPosition;
    protected Vector2 leftClickDelta;
    private Vector2 leftClickPosition;
    protected Rect rect;
    protected KeyCode keyPressedCode;
    private bool sceneGUIEditorEnabled = false;
    protected float handleSize;

    #region CheckRightClick
    static bool CheckContextClicked()
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            start_right_click_pos = Event.current.mousePosition;
            start_right_click_time = EditorApplication.timeSinceStartup;
        }

        if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
        {
            var diff = (Event.current.mousePosition - start_right_click_pos).sqrMagnitude;
            var dt_diff = EditorApplication.timeSinceStartup - start_right_click_time;
            if (diff <= 0)
                return true;
            if (dt_diff < 0.2f && diff < 10)
                return true;
        }

        return false;
    }
    static Vector2 start_right_click_pos;
    static double start_right_click_time;
    #endregion

    void OnEnable()
    {
        if (SelectingFile) return;
        Enable();
    }

    protected virtual void Enable()
    {

    }

    private void OnDisable()
    {
        if (SelectingFile) return;
        Disable();
    }

    protected virtual void Disable()
    {
    }

    #region Check double click
    static bool CheckDoubleClick()
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            var diff = (Event.current.mousePosition - start_left_click_pos).sqrMagnitude;
            var dt_diff = EditorApplication.timeSinceStartup - start_left_click_time;
            if (dt_diff < 0.4f && diff <= 1)
            {
                start_left_click_time = 0;
                return true;
            }

            start_left_click_pos = Event.current.mousePosition;
            start_left_click_time = EditorApplication.timeSinceStartup;
        }
        return false;
    }
    static Vector2 start_left_click_pos;
    static double start_left_click_time;
    #endregion

    public void OnSceneGUI()
    {
        if (SelectingFile)
            return;

        if (((Component)target).GetComponent<RectTransform>() == null)
            ((Component)target).gameObject.AddComponent<RectTransform>();

        this.handleSize = HandleUtility.GetHandleSize(Vector3.zero);
        this.rect = ((Component)target).GetComponent<RectTransform>().rect;

        // TOGGLE EDIT HERE
        var toggled_edit = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
        if (toggled_edit)
        {
            sceneGUIEditorEnabled = !sceneGUIEditorEnabled;
            EditorUtility.SetDirty(target);
            if (sceneGUIEditorEnabled)
                this.OnEnable();
            else
                this.OnDisable();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.S) { // && (Event.current.command || Event.current.control)) {
            EditorUtility.SetDirty(target);
            this.OnDisable();
        }

        if (sceneGUIEditorEnabled)
        {

            this.contextClicked = CheckContextClicked();
            this.doubleClicked = CheckDoubleClick();
            this.clickWorldPosition = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;
            this.mouseInside = Event.current.isMouse && rect.Contains(clickWorldPosition);

            this.leftClicked = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            this.leftReleased = Event.current.type == EventType.MouseUp && Event.current.button == 0;
            this.rightClicked = Event.current.type == EventType.MouseDown && Event.current.button == 1;
            this.rightReleased = Event.current.type == EventType.MouseUp && Event.current.button == 1;

            if (leftClicked) this.leftClickPosition = Input.mousePosition;
            this.leftClickDelta = (Vector2)Input.mousePosition - this.leftClickPosition;

            this.dragging = Event.current.type == EventType.MouseDrag && Event.current.button == 0;
            this.draggingRight = Event.current.type == EventType.MouseDrag && Event.current.button == 1;
            this.clicked = leftClicked;

            this.keyPressed = Event.current.type == EventType.KeyDown;
            this.keyPressedCode = Event.current.keyCode;

            this.CustomSceneGUI();
            if (clicked) Event.current.Use();
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (dragging || draggingRight || leftReleased || rightReleased || keyPressed)
            {
                EditorWindow view = EditorWindow.GetWindow<SceneView>();
                if (InternalEditorUtility.isApplicationActive && view == EditorWindow.focusedWindow)
                {
                    EditorUtility.SetDirty(target);
                    view.Repaint();
                }
            }

            if (keyPressed && keyPressedCode == KeyCode.Delete)
                Event.current.Use();
        }
        else
            CustomSceneGUIDisplay();

        Handles.BeginGUI();
        string info = "Scene gui editor available. Press Esc to toggle.";
        string info_enabled = "Scene gui editor available. Press Esc to toggle.\nScene GUI Editor: enabled.";
        EditorGUI.HelpBox(new Rect(4, 4, 280, 28), sceneGUIEditorEnabled? info_enabled : info, MessageType.Info);
        Handles.EndGUI();
    }

    public virtual void CustomSceneGUI()
    {

    }

    public virtual void CustomSceneGUIDisplay()
    {

    }
    
    protected bool SelectingFile { get
        {
            try
            {
                return AssetDatabase.Contains(Selection.activeObject);
            } catch
            {
                return false;
            }

        } }
}