using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Text.RegularExpressions;
using System;

public partial class ScriptboundObjectEditor : UnityEditor.Editor
{
    public class ParsingException : Exception {
        public ParsingException(string reason = null) : base(reason) { }
    }

    private string GetPreviewContents()
    {
        return ScriptboundObjectToString(Target, true);
    }

    private string GetEditContents()
    {
        return ScriptboundObjectToString(Target, false);
    }

    private string ScriptboundObjectToString(ScriptboundObject target, bool highlight = false)
    {
        if (target.scriptInstructions == null) target.scriptInstructions = new List<ScriptboundObject.Instruction>();
        var results = "";
        var instructions = target.scriptInstructions;
        var instructionParsedCount = 0;

        foreach (var instruction in instructions)
        {

            if (instructionParsedCount > 0) results += "\n\n";
            instructionParsedCount++;

            var indent_replacement = "\n";
            for (var i = 0; i < instruction.indent; i++)
            {
                results += '\t';
                indent_replacement += '\t';
            }

            //singular control

            if (instruction.controlElse)
            {
                results += string.Format(highlight ? "<b>{0}: </b>" : "{0}: ", "else");
                continue;
            }


            //prefix controls
            var control = "";
            if (instruction.controlIf) control += instruction.negative? "ifnot " : "if ";
            control += instruction.instructionName;

            if (methodReflections.ContainsKey(instruction.instructionName) == false) continue;
            var method = methodReflections[instruction.instructionName];
            if (method == null) continue;

            if (method.Name == this.defaultStringMethod) { }
            else {
                if (method.GetParameters().Length <= 0) results += string.Format(highlight ? "<b>{0}</b>" : "{0}", control);
                else results += string.Format(highlight ? "<b>{0}: </b>" : "{0}: ", control);
            }

            if (method.GetParameters().Length > 0)
            {
                var parameters = Target.ExtractParameters(method, instruction);

                string str_params = "";

                if (instruction.instructionName == this.defaultStringMethod)
                    str_params = ParseDefaultStringInstruction(instruction, parameters, highlight);
                else if (parameters.Length == 1 && instruction.parameters[0].type == ScriptboundObject.Instruction.ParamType.STRING && parameters[0] != null)
                {
                    str_params += ParseDefaultStringInstruction(instruction, parameters, highlight);
                    //str_params += parameters[0].ToString(); //TODO: CHANGE THIS TO SUPPORT INJECTIBLE AS WELL
                }
                else
                    for (var i = 0; i < instruction.parameters.Count; i++)
                    {
                        var param = parameters[i];
                        if (i > 0) str_params += ", ";
                        if (instruction.parameters[i].type == ScriptboundObject.Instruction.ParamType.OBJECT)
                            str_params += ObjectToString((UnityEngine.Object)param);
                        else if (param != null)
                        {
                            var param_str = param.ToString().Replace("\\,", ",");
                            param_str = param_str.Replace(",", "\\,");
                            str_params += param_str;
                        }
                    }
                str_params = str_params.Replace("\n", indent_replacement);
                results += str_params;
            }
        }

        return results;
    }

    string ParseDefaultStringInstruction(ScriptboundObject.Instruction instruction, object[] parameters, bool highlight)
    {
        if (instruction.parameters[0].type != ScriptboundObject.Instruction.ParamType.STRING) return "";
        var result = parameters[0].ToString();

        if (instruction.injectibles == null || instruction.injectibles.Count <= 0) return result;
        var count = instruction.injectibles.Count;
        for (var i = count - 1; i >= 0; i--)
        {
            var injectible = instruction.injectibles[i];
            var index = injectible.index;
            var obj = injectible.obj;
            var obj_str = "[[" + ObjectToString(obj) + "]]";
            if (highlight) obj_str = "<b>" + obj_str + "</b>";
            result = result.Insert(index, obj_str);
        }

        return result;
    }

    private void ParseInstructions(ScriptboundObject target, string fullString)
    {
        fullString = fullString.Replace("" + ((char)13), "");

        var methods = target.ExtractMethodReflections();
        var clone = target.Duplicate();

        clone.scriptBoolValues.Clear();
        clone.scriptFloatValues.Clear();
        clone.scriptIntValues.Clear();
        clone.scriptObjectValues.Clear();
        clone.scriptStringValues.Clear();

        clone.scriptInstructions.Clear();

        var instructions = Regex.Split(fullString, @"\n[ \t\n]*\n");
        foreach (var instruction in instructions) ParseInstruction(instruction, clone, methods);

        target.scriptBoolValues = clone.scriptBoolValues;
        target.scriptFloatValues = clone.scriptFloatValues;
        target.scriptIntValues = clone.scriptIntValues;
        target.scriptObjectValues = clone.scriptObjectValues;
        target.scriptStringValues = clone.scriptStringValues;

        target.scriptInstructions = clone.scriptInstructions;
    }

    static List<string> SINGULAR_CONTROLS = new List<string> { "else" };
    private void ParseInstruction(string instruction_str, ScriptboundObject clone, Dictionary<string, MethodInfo> methods)
    {
        if (instruction_str.Length <= 0) return; //if line is empty then no need to do anything

        instruction_str = instruction_str.Trim(' ');
        instruction_str = instruction_str.Trim((char) 13);

        //calculate tabCount
        var tabCount = 0;
        for (var i = 0; i < instruction_str.Length; i++) if (instruction_str[i] != '\t') { tabCount = i; break; }

        //trim instruction from tabs
        instruction_str = instruction_str.Trim('\t');

        //break the instruction into two parts
        var (controls, contents) = BreakInstruction(instruction_str);

        ScriptboundObject.Instruction instructionObj = new ScriptboundObject.Instruction();
        instructionObj.indent = tabCount;


        if (string.IsNullOrEmpty(contents) && SINGULAR_CONTROLS.Contains(controls))
        {
            if (controls == "else") instructionObj.controlElse = true;
        }
        else
        {
            try
            {
                var method = BreakInstructionControl(controls, instructionObj, clone, methods);
                if (contents != null) contents = TrimContentIndent(contents, tabCount);
                BreakInstructionContents(contents, instructionObj, clone, method);
            }
            catch (ParsingException e)
            {
                if (defaultStringMethod != null)
                {
                    var attr_injectible = methodReflections[defaultStringMethod].GetCustomAttribute<ScriptboundObject.StringInjectible>();
                    instructionObj.instructionName = defaultStringMethod;
                    instructionObj.parameters = new List<ScriptboundObject.Instruction.Parameter>();
                    instructionObj.injectibles = new List<ScriptboundObject.Instruction.Injectible>();
                    ExstractSingleStringInstruction(TrimContentIndent(instruction_str, tabCount), instructionObj, clone, attr_injectible);
                }
                else throw e;
            }
        }
        clone.scriptInstructions.Add(instructionObj);
    }

    (string, string) BreakInstruction(string instruction)
    {
        var colonIndex = instruction.IndexOf(':');
        if (colonIndex < 0) return (instruction, null);
        return (instruction.Substring(0, colonIndex).Trim(), instruction.Substring(colonIndex + 1).Trim());
    }

    static List<string> AVAILABLE_CONTROLS = new List<string> { "if", "ifnot" };
    MethodInfo BreakInstructionControl(string control_str, ScriptboundObject.Instruction instruction, ScriptboundObject clone, Dictionary<string, MethodInfo> methods)
    {
        var controls = control_str.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
        if (controls.Length <= 0) throw new ParsingException("Instruction name missing");
        if (controls.Length > 2) throw new ParsingException("Unexpected instruction value: " + controls[2]);

        var control = controls.Length > 1 ? controls[0] : null;
        var funcname = controls.Length == 1 ? controls[0] : controls[1];

        if (control != null && AVAILABLE_CONTROLS.Contains(control) == false) throw new ParsingException("Instruction control not found: " + control);
        if (methods.ContainsKey(funcname) == false)
            throw new ParsingException("Instruction name not found: " + funcname);
        var method = methods[funcname];

        if (control != null)
        {
            if (control.StartsWith("if") && method.ReturnType != typeof(bool)) throw new ParsingException("Instruction control `if` can only apply for instructions that return bool. Current instruction: " + method.Name);
            instruction.controlIf = control.StartsWith("if");
            instruction.negative = control.EndsWith("not");
        }

        instruction.instructionName = method.Name;

        return method;
    }

    void BreakInstructionContents(string contents, ScriptboundObject.Instruction instruction, ScriptboundObject clone, MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length <= 0) return; //if param length is zero then no need to do anything

        var attr_injectible = method.GetCustomAttribute<ScriptboundObject.StringInjectible>();

        //auto parse as string if this is the only field required
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
        {
            ExstractSingleStringInstruction(contents, instruction, clone, attr_injectible);
            return;
        }

        if (contents == null) throw new ParsingException(string.Format("Parameter count for {0} mismatched. Expected {1}, received {2}", method.Name, parameters.Length, 0));

        var tokens = contents.Split(',');
        var token_len = tokens.Length;

        for (var i = 0; i < token_len - 1; i++)
        {
            var next_index = i + 1;
            var combine_offset = 0;
            while (tokens[i].EndsWith("\\") && next_index < token_len)
            {
                tokens[i] = tokens[i].Substring(0, tokens[i].Length - 1) + ',' + tokens[next_index];
                combine_offset++;
                next_index++;
            }

            for (var k = i + 1; k < token_len - combine_offset; k++)
                tokens[k] = tokens[k + combine_offset];

            token_len -= combine_offset;
        }


        if (token_len != parameters.Length)
            throw new ParsingException(string.Format("Parameter count for {0} mismatched. Expected {1}, received {2}", method.Name, parameters.Length, token_len));

        for (var i = 0; i < token_len; i++)
            ValidateInstructionParam(tokens[i].Trim(), parameters[i].ParameterType);

        for (var i = 0; i < token_len; i++)
            ExtractInstructionParam(tokens[i].Trim(), instruction, clone, parameters[i].ParameterType);
    }

    void ValidateInstructionParam(string param_str, System.Type type)
    {
        ScriptboundObject.Instruction.Parameter param = new ScriptboundObject.Instruction.Parameter();

        if (type == typeof(string))
        {
            param.type = ScriptboundObject.Instruction.ParamType.STRING;
            return;
        }

        if (type == typeof(int))
        {
            int param_val;
            if (int.TryParse(param_str, out param_val) == false)
                throw new ParsingException("Instruction param int type parse failure: " + param_str);
            return;
        }

        if (type == typeof(float))
        {
            float param_val;
            if (float.TryParse(param_str, out param_val) == false) throw new ParsingException("Instruction param float type parse failure: " + param_str);
            return;
        }

        if (type.IsEnum)
        {
            int param_val = (int)System.Enum.Parse(type, param_str, true);
            if (param_val < 0) throw new ParsingException("Instruction param Enum type parse failure: " + param_str);
            return;
        }

        if (type == typeof(bool))
        {
            bool param_val;
            if (bool.TryParse(param_str, out param_val) == false) throw new ParsingException("Instruction bool type parse failure: " + param_str);
            return;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            if (param_str.Trim().Length == 0 || param_str.Trim().ToLower() == "none") return;
            var val = StringToObjectWithType(param_str, type);
            if (val == null && param_str.Trim().Length > 0) throw new ParsingException("Instruction object type parse failure: " + param_str);
            return;
        }

        throw new ParsingException("Instruction param type not supported: " + type.Name);
    }

    void ExtractInstructionParam(string param_str, ScriptboundObject.Instruction instruction, ScriptboundObject clone, System.Type type)
    {
        ScriptboundObject.Instruction.Parameter param = new ScriptboundObject.Instruction.Parameter();
        instruction.parameters.Add(param);

        if (type == typeof(string))
        {
            param.type = ScriptboundObject.Instruction.ParamType.STRING;
            param.valueIndex = clone.scriptStringValues.Count;
            clone.scriptStringValues.Add(param_str);
            return;
        }

        if (type == typeof(int))
        {
            int param_val;
            if (int.TryParse(param_str, out param_val) == false)
                throw new ParsingException("Instruction param int type parse failure: " + param_str);
            param.type = ScriptboundObject.Instruction.ParamType.INT;
            param.valueIndex = clone.scriptIntValues.Count;
            clone.scriptIntValues.Add(param_val);
            return;
        }

        if (type == typeof(float))
        {
            float param_val;
            if (float.TryParse(param_str, out param_val) == false) throw new ParsingException("Instruction param float type parse failure: " + param_str);
            param.type = ScriptboundObject.Instruction.ParamType.FLOAT;
            param.valueIndex = clone.scriptFloatValues.Count;
            clone.scriptFloatValues.Add(param_val);
            return;
        }

        if (type.IsEnum)
        {
            int param_val = (int) System.Enum.Parse(type, param_str, true);
            if (param_val < 0) throw new ParsingException("Instruction param Enum type parse failure: " + param_str);
            param.type = ScriptboundObject.Instruction.ParamType.ENUM;
            param.valueIndex = clone.scriptIntValues.Count;
            clone.scriptIntValues.Add(param_val);
            return;
        }

        if (type == typeof(bool))
        {
            bool param_val;
            if (bool.TryParse(param_str, out param_val) == false) throw new ParsingException("Instruction bool type parse failure: " + param_str);
            param.type = ScriptboundObject.Instruction.ParamType.BOOL;
            param.valueIndex = clone.scriptBoolValues.Count;
            clone.scriptBoolValues.Add(param_val);
            return;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            //params_str: name(21412)
            param.type = ScriptboundObject.Instruction.ParamType.OBJECT;
            param.valueIndex = clone.scriptObjectValues.Count;
            var val = StringToObjectWithType(param_str, type);
            clone.scriptObjectValues.Add(val);
            return;
        }

        throw new ParsingException("Instruction param type not supported: " + type.Name);
    }

    static Regex regexExtractInjectible = new Regex(@"(\[\[.*?\]\])");

    private void ExstractSingleStringInstruction(string param_str, ScriptboundObject.Instruction instruction, ScriptboundObject clone, ScriptboundObject.StringInjectible attr_injectible = null)
    {
        if (attr_injectible == null)
        {
            ScriptboundObject.Instruction.Parameter param = new ScriptboundObject.Instruction.Parameter();
            instruction.parameters.Add(param);
            param.type = ScriptboundObject.Instruction.ParamType.STRING;
            param.valueIndex = clone.scriptStringValues.Count;
            clone.scriptStringValues.Add(param_str);
            return;
        }

        var matches = regexExtractInjectible.Matches(param_str);

        List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        List<int> indices = new List<int>();
        List<int> lens = new List<int>();
        int overhead = 0;
        foreach (Match match in matches) {
            var str = match.Value.Substring(2, match.Value.Length - 4);
            var obj = this.StringToObject(str);
            if (obj == null) continue;

            objects.Add(obj);
            indices.Add(match.Index - overhead);
            lens.Add(match.Value.Length);
            overhead += match.Value.Length;
        }

        //If the amount of matches not matching the number of objects detected, make this an invalid string instead
        //This is a bit too extreme so we will just ignore it
        //if (matches.Count == 0 || objects.Count != matches.Count)
        //{
        //    ScriptboundObject.Instruction.Parameter param = new ScriptboundObject.Instruction.Parameter();
        //    instruction.parameters.Add(param);
        //    param.type = ScriptboundObject.Instruction.ParamType.STRING;
        //    param.valueIndex = clone.scriptStringValues.Count;
        //    clone.scriptStringValues.Add(param_str);
        //    return;
        //}

        for (var i = 0; i < objects.Count; i++)
            param_str = param_str.Remove(indices[i], lens[i]);

        {
            ScriptboundObject.Instruction.Parameter param = new ScriptboundObject.Instruction.Parameter();
            instruction.parameters.Add(param);
            param.type = ScriptboundObject.Instruction.ParamType.STRING;
            param.valueIndex = clone.scriptStringValues.Count;
            clone.scriptStringValues.Add(param_str);
        }

        instruction.injectibles = new List<ScriptboundObject.Instruction.Injectible>();
        for (var i = 0; i < objects.Count; i++)
        {
            instruction.injectibles.Add(new ScriptboundObject.Instruction.Injectible()
            {
                index = indices[i],
                obj = objects[i]
            });
        }
    }
}