using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ScriptableObjectBrowser
{

    public class PromptForText : PopupWindowContent
    {

        public static void Show(string prompt, Action<string> callback)
        {
            PopupWindow.Show(GUILayoutUtility.GetLastRect(), new PromptForText(prompt, callback));
        }

        private string prompt;
        private Action<string> callback;
        private string content = "";

        public PromptForText(string prompt, Action<string> callback)
        {
            this.prompt = prompt;
            this.callback = callback;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(480, 40);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField(this.prompt);

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape) this.editorWindow.Close();
                if (Event.current.keyCode == KeyCode.Return)
                {
                    callback(content);
                    this.editorWindow.Close();
                }
            }

            GUI.SetNextControlName(this.GetHashCode().ToString());
            content = EditorGUILayout.TextField(content);
            GUI.FocusControl(this.GetHashCode().ToString());


            GUILayout.EndArea();
        }
    }


}