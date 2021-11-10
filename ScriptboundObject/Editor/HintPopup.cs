using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FuzzyFinder;
using UnityEditor.IMGUI.Controls;
using System.Reflection;

public class ScriptboundObjectEditorHintPopup : PopupWindowContent
{
    struct SearchMatch
    {
        public string searchPath;
        public UnityEngine.Object searchValue;
        public int score;
    }

    public System.Action callbackClose;
    public System.Action<string> callbackInput;
    public System.Action<string, UnityEngine.Object> callbackInputObject;

    string textInput = "";
    private AutocompleteSearchField autocompleteSearchField;
    List<string> searchPaths;
    Dictionary<string, UnityEngine.Object> mapSearchObject = new Dictionary<string, UnityEngine.Object>();
    List<SearchMatch> matches;

    public ScriptboundObjectEditorHintPopup(List<string> available_options, string existing_value = null)
    {
        Setup(available_options, existing_value);
    }

    public ScriptboundObjectEditorHintPopup(Dictionary<string, MethodInfo> methodReflections, string existing_value = null)
    {
        Setup(new List<string>(methodReflections.Keys), existing_value);
    }

    public ScriptboundObjectEditorHintPopup(System.Type hintType, UnityEngine.Object obj = null)
    {
        if (obj == null) HintTypeConstructor(new List<System.Type>() { hintType });
        else
        {
            var existing_value = AssetDatabase.GetAssetPath(obj) + ":" + obj.name;
            HintTypeConstructor(new List<System.Type>() { hintType }, existing_value);
        }
    }

    public ScriptboundObjectEditorHintPopup(System.Type hintType, string linevar = "")
    {
        HintTypeConstructor(new List<System.Type>() { hintType }, linevar);
    }

    public ScriptboundObjectEditorHintPopup(List<System.Type> hintTypes, string linevar = "")
    {
        HintTypeConstructor(hintTypes, linevar);
    }

    void HintTypeConstructor(List<System.Type> hintTypes, string existing_value = null)
    {
        var options = new List<string>();

        foreach (var hintType in hintTypes)
        {
            var type = hintType;

            if (type.IsEnum)
            {
                var names = System.Enum.GetNames(type);
                options = new List<string>(names);
            }
            else if (typeof(MonoBehaviour).IsAssignableFrom(hintType))
            {
                var uids = AssetDatabase.FindAssets("t: GameObject");
                foreach (var uid in uids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(uid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var co = go.GetComponent(hintType);
                    if (co == null) continue;

                    var p = path + ":" + go.name;
                    options.Add(p);
                    mapSearchObject[p] = co;
                }
            }
            else
            {
                var uids = AssetDatabase.FindAssets("t:" + hintType.Name);
                foreach (var uid in uids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(uid);
                    var objs = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var obj in objs)
                    {
                        if (hintType.IsAssignableFrom(obj.GetType()))
                        {
                            var p = path + ":" + obj.name;
                            options.Add(p);
                            mapSearchObject[p] = obj;
                        }
                    }
                }
                //options = new List<string>(AssetDatabase.GetAllAssetPaths());
                options.RemoveAll((s) => s.StartsWith("Assets") == false);
            }
        }

        Setup(options, existing_value);
    }

    #region Main components
    void Setup(List<string> available_options, string existing_value = null)
    {
        if (existing_value != null)
        {
            existing_value = existing_value.Trim();
            textInput = existing_value;
        }

        autocompleteSearchField = new AutocompleteSearchField();
        autocompleteSearchField.searchString = textInput;
        autocompleteSearchField.onInputChanged = OnInputChanged;
        autocompleteSearchField.onConfirm = OnConfirm;

        searchPaths = new List<string>(available_options);

        searchPaths.Sort((s1, s2) => s1.CompareTo(s2));
        matches = new List<SearchMatch>(searchPaths.Count);

        OnInputChanged(autocompleteSearchField.searchString);
    }

    void OnInputChanged(string searchString)
    {
        autocompleteSearchField.ClearResults();
        matches.Clear();
        if (!string.IsNullOrEmpty(searchString.Trim(' ', '\t', '\n')))
        {
            int score = 0;
            int count = 0;

            foreach (var searchPath in searchPaths)
            {
                if (FuzzyMatcher.FuzzyMatch(searchPath, searchString, out score))
                {
                    matches.Add(new SearchMatch()
                    {
                        searchPath = searchPath,
                        searchValue = mapSearchObject.ContainsKey(searchPath) ? mapSearchObject[searchPath] : null,
                        score = score
                    });
                    count++;
                    if (count > 90) break;
                }
            }

            matches.Sort((m1, m2) => m2.score.CompareTo(m1.score));
            for (var i = 0; i < Mathf.Min(matches.Count, autocompleteSearchField.maxResults); i++)
                autocompleteSearchField.AddResult(matches[i].searchPath, null);
        } else
        {
            int count = 0;
            foreach (var searchPath in searchPaths)
            {
                autocompleteSearchField.AddResult(searchPath, null);
                count++;
                if (count > 20) break;
            }
        }
    }

    void OnConfirm(string searchResult, string altResult)
    {
        var objResult = mapSearchObject.ContainsKey(searchResult) ? mapSearchObject[searchResult] : null;

        if (objResult != null) this.callbackInputObject?.Invoke(searchResult, objResult);
        else this.callbackInput?.Invoke(searchResult);

        //if (altResult == null) altResult = searchResult;
        //this.callbackInput?.Invoke(altResult);
        this.editorWindow.Close();
    }
    #endregion

    #region OnGUI
    public override Vector2 GetWindowSize()
    {
        return new Vector2(640, 560);
    }

    public override void OnGUI(Rect rect)
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            callbackClose?.Invoke();
            this.editorWindow.Close();
            return;
        }


        rect.width -= 12;
        rect.x += 6;
        rect.y += 4;
        GUILayout.BeginArea(rect);
        autocompleteSearchField.OnGUI();
        GUILayout.EndArea();
    }
    #endregion
        
    #region Autodcomplete Searchfield
    [Serializable]
    public class AutocompleteSearchField
    {
        static class Styles
        {
            public const float resultHeight = 20f;
            public const float resultsBorderWidth = 2f;
            public const float resultsMargin = 15f;
            public const float resultsLabelOffset = 2f;

            public static readonly GUIStyle entryEven;
            public static readonly GUIStyle entryOdd;
            public static readonly GUIStyle labelStyle;
            public static readonly GUIStyle resultsBorderStyle;

            public static readonly string inputControlName = null;

            static Styles()
            {
                entryOdd = new GUIStyle("CN EntryBackOdd");
                entryEven = new GUIStyle("CN EntryBackEven");
                resultsBorderStyle = new GUIStyle("hostview");

                labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    richText = true
                };
            }
        }

        public Action<string> onInputChanged;
        public Action<string, string> onConfirm;
        Dictionary<string, string> altValues = new Dictionary<string, string>();
        public string searchString = null;
        public int maxResults = 15;

        [SerializeField]
        List<string> results = new List<string>();

        [SerializeField]
        int selectedIndex = 0;

        SearchField searchField;

        Vector2 previousMousePosition;
        bool selectedIndexByMouse;

        bool showResults = true;

        public void AddResult(string result, string value = null)
        {
            results.Add(result);
            if (value != null) altValues[result] = value;
        }

        public void ClearResults()
        {
            results.Clear();
        }

        public void OnToolbarGUI()
        {
            Draw(asToolbar: true);
        }

        public void OnGUI()
        {
            Draw(asToolbar: false);
        }

        void Draw(bool asToolbar)
        {
            var rect = GUILayoutUtility.GetRect(1, 1, 18, 18, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            DoSearchField(rect, asToolbar);
            GUILayout.EndHorizontal();
            rect.y += 18;
            DoResults(rect);
        }

        void DoSearchField(Rect rect, bool asToolbar)
        {
            if (searchField == null)
            {
                searchField = new SearchField();
                searchField.SetFocus();
                searchField.downOrUpArrowKeyPressed += OnDownOrUpArrowKeyPressed;
            }

            var result = asToolbar
                ? searchField.OnToolbarGUI(rect, searchString)
                : searchField.OnGUI(rect, searchString);

            if (result != searchString && onInputChanged != null)
            {
                onInputChanged(result);
                selectedIndex = 0;
                showResults = true;
            }

            searchString = result;

            if (HasSearchbarFocused())
            {
                RepaintFocusedWindow();
            }
        }

        void OnDownOrUpArrowKeyPressed()
        {
            var current = Event.current;

            if (current.keyCode == KeyCode.UpArrow)
            {
                current.Use();
                selectedIndex--;
                selectedIndexByMouse = false;
            }
            else
            {
                current.Use();
                selectedIndex++;
                selectedIndexByMouse = false;
            }

            if (selectedIndex >= results.Count) selectedIndex = results.Count - 1;
            else if (selectedIndex < 0) selectedIndex = -1;
        }

        internal float GetExtendedHeight()
        {
            if (results.Count <= 0 || !showResults) return 0;
            return Styles.resultHeight * Mathf.Min(maxResults, results.Count);
        }

        void DoResults(Rect rect)
        {
            if (results.Count <= 0 || !showResults) return;

            var current = Event.current;
            rect.height = Styles.resultHeight * Mathf.Min(maxResults, results.Count);
            rect.x = Styles.resultsMargin;
            rect.width -= Styles.resultsMargin * 2;

            var elementRect = rect;

            rect.height += Styles.resultsBorderWidth;
            GUI.Label(rect, "", Styles.resultsBorderStyle);

            var mouseIsInResultsRect = rect.Contains(current.mousePosition);

            if (mouseIsInResultsRect)
            {
                RepaintFocusedWindow();
            }

            var movedMouseInRect = previousMousePosition != current.mousePosition;

            elementRect.x += Styles.resultsBorderWidth;
            elementRect.width -= Styles.resultsBorderWidth * 2;
            elementRect.height = Styles.resultHeight;

            var didJustSelectIndex = false;

            for (var i = 0; i < results.Count && i < maxResults; i++)
            {
                if (current.type == EventType.Repaint)
                {
                    var style = i % 2 == 0 ? Styles.entryOdd : Styles.entryEven;

                    style.Draw(elementRect, false, false, i == selectedIndex, false);

                    var labelRect = elementRect;
                    labelRect.x += Styles.resultsLabelOffset;
                    GUI.Label(labelRect, results[i], Styles.labelStyle);
                }
                if (elementRect.Contains(current.mousePosition))
                {
                    if (movedMouseInRect)
                    {
                        selectedIndex = i;
                        selectedIndexByMouse = true;
                        didJustSelectIndex = true;
                    }
                    if (current.type == EventType.MouseDown)
                    {
                        OnConfirm(results[i]);
                    }
                }
                elementRect.y += Styles.resultHeight;
            }

            if (current.type == EventType.Repaint && !didJustSelectIndex && !mouseIsInResultsRect && selectedIndexByMouse)
            {
                selectedIndex = -1;
            }

            if ((GUIUtility.hotControl != searchField.searchFieldControlID && GUIUtility.hotControl > 0)
                || (current.rawType == EventType.MouseDown && !mouseIsInResultsRect))
            {
                showResults = false;
            }

            if (current.type == EventType.KeyUp && current.keyCode == KeyCode.Return && selectedIndex >= 0)
            {
                var result = results[selectedIndex];
                OnConfirm(results[selectedIndex]);
            }

            if (current.type == EventType.Repaint)
            {
                previousMousePosition = current.mousePosition;
            }
        }

        void OnConfirm(string result)
        {
            searchString = result;
            var altValue = altValues.ContainsKey(result) ? altValues[result] : null;

            if (onConfirm != null) onConfirm(result, altValue);
            if (onInputChanged != null) onInputChanged(result);
            RepaintFocusedWindow();
            GUIUtility.keyboardControl = 0; // To avoid Unity sometimes not updating the search field text
        }

        bool HasSearchbarFocused()
        {
            return GUIUtility.keyboardControl == searchField.searchFieldControlID;
        }

        static void RepaintFocusedWindow()
        {
            if (EditorWindow.focusedWindow != null)
            {
                EditorWindow.focusedWindow.Repaint();
            }
        }
    }
#endregion
}
