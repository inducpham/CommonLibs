using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//[CustomEditor(typeof(TARGET_MONO_BEHAVIOUR))]
public class SceneGUIEditorExample : SceneGUIEditor
{

    protected override void Enable()
    {
        // EDIT ON ENABLE HERE
        // Usually for setting up the editor
    }

    protected override void Disable()
    {
        // EDIT ON DISABLE HERE
        // Usually for saving the data from the editor state
    }

    public override void CustomSceneGUIDisplay()
    {
        // DISPLAY WITHOUT EDIT MODE
    }

    public override void CustomSceneGUI()
    {
        // DISPLAY ON EDIT MODE
    }

}
