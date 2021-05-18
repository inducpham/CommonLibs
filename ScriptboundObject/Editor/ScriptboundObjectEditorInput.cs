using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

public partial class ScriptboundObjectEditor : UnityEditor.Editor
{
    void InputNewLineWithIndents(TextEditor te)
    {
        var index = te.cursorIndex;
        var text = this.editing_contents;

        if (text.Length <= 0)
        {
            te.Insert('\n');
            return;
        }

        var tabcount = 0;
        index--;
        if (index >= text.Length) index = text.Length - 1;
        while (index >= 0 && text[index] != '\n')
            index--;
        index++;
        while (index < text.Length && text[index] == '\t')
        {
            tabcount++;
            index++;
        }

        te.Insert('\n');
        while (tabcount > 0)
        {
            te.Insert('\t');
            tabcount--;
        }
    }
}