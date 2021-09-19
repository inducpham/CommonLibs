﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Text.RegularExpressions;

public partial class ScriptboundObjectEditor : UnityEditor.Editor
{

    private string ExtractCurrentField(TextEditor te)
    {
        #region Extract the current line
        var text = te.text;
        var index = te.cursorIndex;

        var start_index = index;
        var end_index = index;

        while (start_index > 0)
        {
            start_index--;
            var c = text[start_index];
            if (c == '\n' || c == ',' || c == ':')
            {
                start_index++;
                break;
            }
        }

        end_index--;
        while (end_index < text.Length - 1)
        {
            end_index++;
            var c = text[end_index];
            if (c == '\n' || c == ',')
            {
                end_index--;
                break;
            }
        }
        var contents = text.Substring(start_index, end_index - start_index + 1);
        #endregion

        return contents.Trim();
    }

    private string TrimContentIndent(string contents, int indent)
    {
        var src = "\n";
        for (var i = 0; i < indent; i++) src += '\t';
        return contents.Replace(src, "\n");
    }

    /// <summary>
    /// Extract the current context for suggestion
    /// </summary>
    /// <param name="te">The context text editor</param>
    /// <returns>modification, method name, param index</returns>
    private (string, string, int, string) ExtractCurrentLineContext(TextEditor te)
    {
        #region Extract the current line
        var text = te.text;
        var index = te.cursorIndex;

        var start_index = index;
        var end_index = index;
        var start_new_line_count = 0;
        var end_new_line_count = 0;

        while (start_index > 0)
        {
            start_index--;
            var c = text[start_index];
            if (c == '\n') start_new_line_count++;
            else if (c != '\t' && c != ' ' && c != 13) start_new_line_count = 0;

            if (start_new_line_count >= 2)
            {
                start_index++;
                break;
            }            
        }
        if (start_index < 0) start_index = 0;

        end_index--;
        while (end_index < text.Length - 1)
        {
            end_index++;
            var c = text[end_index];
            if (c == '\n') end_new_line_count++;
            else if (c != '\t' && c != ' ' && c != 13) end_new_line_count = 0;
            if (end_new_line_count >= 2)
            {
                end_index--;
                break;
            }
        }
        if (end_index > text.Length - 1) end_index = text.Length - 1;
        index -= start_index;
        var contents = text.Substring(start_index, end_index - start_index + 1);
        #endregion

        #region Extract the indents while keeping the cursor index
        var indent_count = 0;
        var start_trim_index = 0;
        var start_white_space_index = 0;
        for (start_white_space_index = 0; start_white_space_index < contents.Length; start_white_space_index++)
        {
            var c = contents[start_white_space_index];
            if (c != '\n' && c != ' ' && c != '\t') break;
        }
        start_trim_index = start_white_space_index;
        for (; start_white_space_index > 0; start_white_space_index--)
        {
            var c = contents[start_white_space_index - 1];
            if (c == '\n') break;
        }
        //should be right after the new line and at the first tab
        for (; start_white_space_index < contents.Length; start_white_space_index++)
        {
            var c = contents[start_white_space_index];
            if (c != '\t') break;
            indent_count++;
        }
        contents = contents.Substring(start_trim_index);
        if (index >= start_trim_index) index -= start_trim_index;

        if (indent_count > 0) {
            var trim_contents = "\n";
            for (var i = 0; i < indent_count; i++) trim_contents += '\t';
            var trim_contents_index = contents.IndexOf(trim_contents);
            while (trim_contents_index > 0)
            {
                contents = contents.Remove(trim_contents_index + 1, trim_contents.Length - 1);
                if (trim_contents_index + trim_contents.Length <= index) index -= trim_contents.Length - 1;
                trim_contents_index = contents.IndexOf(trim_contents);
            }
        }
        #endregion

        var colon_index = contents.IndexOf(':');
        var before_colon = index <= colon_index || colon_index < 0;

        if (before_colon)
        {
            var (mod, method, control_index, word) = ExtractCurrentLineControlContext(colon_index < 0 ? contents : contents.Substring(0, colon_index), index);
            return (mod, method, control_index, word);
        } else
        {
            var (mod, method, control_index, word) = ExtractCurrentLineControlContext(colon_index < 0 ? contents : contents.Substring(0, colon_index), index);
            contents = contents.Substring(colon_index + 1);
            index -= colon_index + 1;
            (control_index, word) = ExtractCurrentLineParameterContext(contents, index);
            return (mod, method, control_index, word);
        }

        //Debug.Log(contents);
        //Debug.Log(contents[index]);

        return (null, null, 0, null);
    }

    private (string, string, int, string) ExtractCurrentLineControlContext(string content, int cursor)
    {
        var matches = Regex.Matches(content, @"([\S]+)");

        if (matches.Count <= 0) return (null, null, -1, null);
        if (matches.Count == 1)
        {
            var match = matches[0];
            var pos = -1;
            string res = null;
            if (match.Index <= cursor && match.Index + match.Length >= cursor) res = match.Value;
            return (null, match.Value, pos, res);
        }
        else
        {
            var pos = -1;
            string res = null;
            foreach (Match match in matches)
            {
                if (match.Index > cursor) break;
                pos ++;
                if (match.Index <= cursor && match.Index + match.Length >= cursor) res = match.Value;
            }
            return (matches[0].Value, matches[1].Value, pos - matches.Count, res);
        }
    }

    private (int, string) ExtractCurrentLineParameterContext(string content, int cursor)
    {
        var matches = Regex.Split(content, @"(?<!\\),");
        var index = 0;

        foreach (var match in matches)
        {
            if (match.Length >= cursor) return (index, match);
            index += 1;
            cursor -= match.Length + 1;
        }

        return (0, null);
    }

    private static Dictionary<int, Object> mapHashToObject = new Dictionary<int, Object>();

    private string ObjectToString(UnityEngine.Object obj)
    {
        if (obj == null) return "none";
        var hash = obj.GetHashCode();
        mapHashToObject[hash] = obj;
        return string.Format("{0}({1})", obj.name, hash);
    }

    private UnityEngine.Object StringToObject(string str)
    {
        var matches = Regex.Matches(str, @"\((\d+)\)");
        if (matches.Count <= 0) return null;
        var match = matches[matches.Count - 1];
        if (match.Success == false) return null;
        var hash = int.Parse(match.Value.Substring(1, match.Value.Length - 2));
        if (mapHashToObject.ContainsKey(hash) == false) return null;
        return mapHashToObject[hash];
    }
}