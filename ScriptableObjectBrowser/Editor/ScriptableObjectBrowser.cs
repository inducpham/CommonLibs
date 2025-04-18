﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Callbacks;


////////////////////////////////////////////////////////////////////////
/// 
/// The editor goes here
/// 
////////////////////////////////////////////////////////////////////////

namespace ScriptableObjectBrowser
{
    public class ScriptableObjectBrowser : EditorWindow
    {
        static int BROWSE_AREA_WIDTH = 320;
        static int ENTRY_LINE_HEIGHT = 22;

        private static LinkedList<ScriptableObject> EditorHistory = new LinkedList<ScriptableObject>();
        private static int EDITOR_HISTORY_MAX = 60;

        private static Dictionary<System.Type, ScritpableObjectBrowserEditor> editors = null;
        private static List<System.Type> browsable_types = new List<System.Type>();
        private static string[] browsable_type_names;

        static void ReloadScriptableObjectBrowserEditors()
        {
            if (editors != null) return;
            editors = new Dictionary<System.Type, ScritpableObjectBrowserEditor>();
            var browsable_type_names = new List<string>();

            var types = System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(t =>
                t.BaseType != null && t.BaseType.IsGenericType && t.BaseType.GetGenericTypeDefinition() == typeof(ScriptableObjectBrowserEditor<>));
            foreach (var type in types)
            {
                var t = type.BaseType.GetGenericArguments()[0];
                editors[t] = (ScritpableObjectBrowserEditor)System.Activator.CreateInstance(type);
                browsable_types.Add(t);
                browsable_type_names.Add(t.Name);
            }
            ScriptableObjectBrowser.browsable_type_names = browsable_type_names.ToArray();
        }

        private void OnEnable()
        {
            ReloadScriptableObjectBrowserEditors();
            SetupEditorAssets();

            if (currentEditor == null && currentObject != null)
                OpenObject(currentObject);
            else
                SwitchToEditorType(browsable_types[0]);
        }

        [MenuItem("Window/Scriptable Object Browser %#o")]
        public static ScriptableObjectBrowser ShowWindow()
        {
            ReloadScriptableObjectBrowserEditors();


            ScriptableObjectBrowser[] windows = Resources.FindObjectsOfTypeAll<ScriptableObjectBrowser>();
            if (windows != null && windows.Length > 0) return windows[0];

            var w = EditorWindow.GetWindow<ScriptableObjectBrowser>();
            w.ShowTab();
            return w;
        }

        static void OpenObject(UnityEngine.Object obj)
        {
            var w = ShowWindow();
            var type = obj.GetType();
            w.SwitchToEditorType(type);
            ScritpableObjectBrowserEditor editor = w.currentEditor;
            editor.SetTargetObjects(new UnityEngine.Object[] { obj });
            w.SelectionSingle(obj);
        }

        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (editors == null || Selection.activeObject == null) return false;
            ReloadScriptableObjectBrowserEditors();
            if (editors.ContainsKey(Selection.activeObject.GetType()))
            {
                OpenObject(EditorUtility.InstanceIDToObject(instanceID));
                return true;
            }

            return false; // let unity open the file
        }
        
        void SwitchToEditorType(System.Type type)
        {
            RecordCurrentSelection();
            this.currentSelectionEntry = this.startSelectionEntry = null;
            this.selections.Clear();

            while (type != null && editors.ContainsKey(type) == false) type = type.BaseType;
            if (type == null) return;

            currentEditorTypeIndex = browsable_types.IndexOf(type);
            this.currentEditor = editors[type];
            this.currentEditor.browser = this;
            this.currentType = type;
            ResetAssetList(type);
            SelectionChanged();
        }

        int currentEditorTypeIndex = 0;
        System.Type currentType = null;
        ScritpableObjectBrowserEditor currentEditor = null;
        UnityEngine.Object currentObject;
        List<AssetEntry> asset_list = new List<AssetEntry>();
        List<AssetEntry> sorted_asset_list = new List<AssetEntry>();

        void ResetAssetList(System.Type type)
        {
            var assets = AssetDatabase.FindAssets($"t:{type.Name}");
            var found_assets = new HashSet<UnityEngine.Object>();
            this.filter_text = "";

            List<AssetEntry> asset_list = new List<AssetEntry>();

            string last_path = null;
            foreach (var asset_guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(asset_guid);
                if (path == last_path) continue;
                last_path = path;

                var loaded_assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in loaded_assets)
                    if (type.IsAssignableFrom(asset.GetType())) found_assets.Add(asset);
            }

            foreach (var asset in found_assets)
                asset_list.Add(CreateAssetEntry(asset));

            this.asset_list = asset_list;
            this.sorted_asset_list = new List<AssetEntry>(asset_list);
        }

        void ResetAssetList()
        {
            ResetAssetList(this.currentType);
        }

        AssetEntry CreateAssetEntry(UnityEngine.Object asset)
        {
            var name = asset.name;

            var path = AssetDatabase.GetAssetPath(asset) + "." + name;

            var entry = new AssetEntry()
            {
                path = path,
                rpath = ReverseString(path),
                name = name,
                asset = asset
            };

            return entry;
        }

        void SyncAssetEntry(AssetEntry entry)
        {
            var asset = entry.asset;
            var name = asset.name;
            var path = AssetDatabase.GetAssetPath(asset) + "." + name;

            entry.path = path;
            entry.rpath = ReverseString(path);
            entry.name = name;
        }

        void AddAssetEntry(UnityEngine.Object asset)
        {
            var entry = CreateAssetEntry(asset);
            this.asset_list.Add(entry);
            this.ResortEntries(this.filter_text);
        }

        void AddAssetEntries(List<UnityEngine.Object> assets)
        {
            foreach (var asset in assets)
            {
                var entry = CreateAssetEntry(asset);
                this.asset_list.Add(entry);
            }
            this.ResortEntries(this.filter_text);
        }

        class AssetEntry
        {
            public string path;
            public string rpath;
            public string name;
            public UnityEngine.Object asset;
            public int match_amount;
            public bool visible = true;
            public bool selected = false;
        }

        private void OnGUI()
        {
            Rect pos = position;
            pos.x -= pos.xMin;
            pos.y -= pos.yMin;

            Rect rect_browse = pos;
            Rect rect_inspect = pos;
            rect_browse.width = BROWSE_AREA_WIDTH;
            rect_inspect.x += BROWSE_AREA_WIDTH;

            rect_inspect.width -= BROWSE_AREA_WIDTH;

            GUILayout.BeginArea(rect_browse, EditorStyles.helpBox);
            OnBrowse(rect_browse);
            GUILayout.EndArea();

            GUILayout.BeginArea(rect_inspect, EditorStyles.helpBox);
            OnInspect(rect_inspect);
            GUILayout.EndArea();
        }

        void SetupEditorAssets()
        {
            text_scriptable_object = EditorGUIUtility.FindTexture("UnityEditor.ConsoleWindow");
            selected_style = new GUIStyle();
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, new Color(62 / 255f, 125 / 255f, 231 / 255f));
            t.Apply();
            selected_style.normal.background = t;
            selected_style.normal.textColor = Color.white;
            selected_style.fixedHeight = 18;
        }

        static Texture2D text_scriptable_object = null;
        static GUIStyle selected_style = null;

        Vector2 browse_scroll = new Vector2();
        string filter_text = "";

        void ResortEntries(string filter_text)
        {
            var r_filter_text = ReverseString(filter_text);
            this.filter_text = filter_text;
            filter_text = filter_text.ToLower();
            foreach (var entry in asset_list)
            {
                if (filter_text.Length == 0)
                {
                    entry.visible = true;
                    continue;
                }
                if (filter_text.Length > entry.path.Length) continue;

                var last_index = 0;
                var path = entry.path.ToLower();
                for (var i = 0; i < filter_text.Length; i++)
                {
                    last_index = path.IndexOf(filter_text[i], last_index);
                    if (last_index < 0) break;
                    last_index++;
                }
                entry.visible = last_index >= 0;
                if (entry.visible)
                {
                    int match_amount, r_match_amount;
                    FuzzyFinder.FuzzyMatcher.FuzzyMatch(entry.rpath, r_filter_text, out match_amount);
                    FuzzyFinder.FuzzyMatcher.FuzzyMatch(entry.path, filter_text, out r_match_amount);
                    entry.match_amount = Mathf.Max(match_amount, r_match_amount);
                }
            }

            sorted_asset_list = new List<AssetEntry>(asset_list);
            sorted_asset_list.RemoveAll((a) => a.visible == false);
            if (filter_text.Length > 0) sorted_asset_list.Sort((e2, e1) => e1.match_amount.CompareTo(e2.match_amount));
        }

        bool browse_control_focused = false;

        void OnBrowse(Rect area)
        {
            area.x = area.y = 0;
            var dummy_control_name = this.GetHashCode() + "FILTER_DUMMY";
            var filter_control_name = this.GetHashCode() + "FILTER";
            var focus_control_name = this.GetHashCode() + "browse_focus_id";

            GUI.color = Color.clear;
            var dummy_control_rect = EditorGUILayout.GetControlRect(false, 0); dummy_control_rect.width = 0; GUI.SetNextControlName(dummy_control_name); EditorGUI.TextField(dummy_control_rect, "");
            GUI.color = Color.white;

            var new_editor_type_index = EditorGUILayout.Popup(this.currentEditorTypeIndex, browsable_type_names);
            if (new_editor_type_index != this.currentEditorTypeIndex) this.SwitchToEditorType(browsable_types[new_editor_type_index]);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = currentEditor.DefaultStoragePath != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.miniButton, GUILayout.Width(24), GUILayout.Height(18)))
                this.CreateNewEntry();
            GUI.enabled = true;

            GUI.enabled = currentEditor.ContextMenu != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("SettingsIcon"), EditorStyles.miniButton, GUILayout.Width(24), GUILayout.Height(18)))
                currentEditor.ContextMenu.ShowAsContext();
            GUI.enabled = true;

            GUI.SetNextControlName(filter_control_name);
            var filter_text = EditorGUILayout.TextField(this.filter_text, (GUIStyle)"SearchTextField");
            if (filter_text.Length != this.filter_text.Length) ResortEntries(filter_text);

            if (GUILayout.Button(" ", (GUIStyle)"SearchCancelButton"))
            {
                ResortEntries("");
                GUIUtility.keyboardControl = 0;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            var editing_text = EditorGUIUtility.editingTextField;
            if (!editing_text && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.LeftArrow && (Event.current.control || Event.current.command))
            {
                Event.current.Use();
                SelectPrevious();
            }

            if (GUI.GetNameOfFocusedControl() == dummy_control_name)
                GUI.FocusControl(filter_control_name);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F && Event.current.control)
            {
                GUI.FocusControl(dummy_control_name);
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.N && (Event.current.control || Event.current.command))
            {
                Event.current.Use();
                this.CreateNewEntry();
            }

            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.F2 ||
                ((Event.current.control || Event.current.command) && Event.current.keyCode == KeyCode.R)))
            {
                Event.current.Use();
                this.RenameCurrentEntry();
            }

            if (GUI.GetNameOfFocusedControl() == filter_control_name && Event.current.type == EventType.Layout)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    ResortEntries("");
                    GUI.FocusControl(dummy_control_name);
                }
                if (Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.PageUp || Event.current.keyCode == KeyCode.PageDown)
                {
                    GUI.FocusControl(focus_control_name);
                }
            }


            browse_scroll = GUILayout.BeginScrollView(browse_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var rect_entry = new Rect(0, 0, area.width, ENTRY_LINE_HEIGHT);
            foreach (var asset in sorted_asset_list)
                RenderAssetEntry(asset, ref rect_entry);
            EditorGUILayout.EndScrollView();

            var scroll_rect = GUILayoutUtility.GetLastRect();

            GUI.SetNextControlName(focus_control_name);
            GUI.color = Color.clear; EditorGUI.Toggle(scroll_rect, true); GUI.color = Color.white;
            this.browse_control_focused = GUI.GetNameOfFocusedControl() == focus_control_name;

            if (this.browse_control_focused && sorted_asset_list.Count > 0 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                ResortEntries("");
                GUI.FocusControl(filter_control_name);
            }

            if (this.browse_control_focused && sorted_asset_list.Count > 0 && Event.current.type == EventType.KeyDown)
            {
                var start_selection_index = sorted_asset_list.IndexOf(startSelectionEntry);
                var current_selection_index = sorted_asset_list.IndexOf(currentSelectionEntry);
                var min_index = 0;
                var max_index = sorted_asset_list.Count - 1;
                var page_index_count = Mathf.Max(1, Mathf.FloorToInt(scroll_rect.height / ENTRY_LINE_HEIGHT) - 1);

                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[max_index] :
                        sorted_asset_list[Mathf.Max(min_index, current_selection_index - 1)];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[min_index] :
                        sorted_asset_list[Mathf.Min(max_index, current_selection_index + 1)];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if (Event.current.keyCode == KeyCode.PageUp)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[max_index] :
                        sorted_asset_list[Mathf.Max(min_index, current_selection_index - page_index_count)];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if (Event.current.keyCode == KeyCode.PageDown)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[min_index] :
                        sorted_asset_list[Mathf.Min(max_index, current_selection_index + page_index_count)];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if (Event.current.keyCode == KeyCode.Home)
                {
                    var target_asset = sorted_asset_list[min_index];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if (Event.current.keyCode == KeyCode.End)
                {
                    var target_asset = sorted_asset_list[max_index];
                    if (Event.current.shift) SelectionToRange(target_asset);
                    else SelectionSingle(target_asset);
                }
                else if ((Event.current.command || Event.current.control) && Event.current.keyCode == KeyCode.A)
                    SelectionSetAll();

                current_selection_index = sorted_asset_list.IndexOf(currentSelectionEntry);
                var selection_y = current_selection_index * ENTRY_LINE_HEIGHT;
                if (selection_y < browse_scroll.y) browse_scroll.y = Mathf.Max(0, selection_y - ENTRY_LINE_HEIGHT / 2);
                if (selection_y > browse_scroll.y + scroll_rect.height - ENTRY_LINE_HEIGHT * 2)
                {
                    var max = ENTRY_LINE_HEIGHT * sorted_asset_list.Count - scroll_rect.height + 14;
                    browse_scroll.y = Mathf.Min(max, selection_y - scroll_rect.height + 50);
                }
            }

            #region SHIFT MOVE SELECTION WHILE EDITING
            if (!this.browse_control_focused && sorted_asset_list.Count > 0 && Event.current.type == EventType.KeyDown && (Event.current.control || Event.current.command))
            {
                var start_selection_index = sorted_asset_list.IndexOf(startSelectionEntry);
                var current_selection_index = sorted_asset_list.IndexOf(currentSelectionEntry);
                var min_index = 0;
                var max_index = sorted_asset_list.Count - 1;
                var page_index_count = Mathf.Max(1, Mathf.FloorToInt(scroll_rect.height / ENTRY_LINE_HEIGHT) - 1);

                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[max_index] :
                        sorted_asset_list[Mathf.Max(min_index, current_selection_index - 1)];
                    SelectionSingle(target_asset);
                    GUIUtility.keyboardControl = 0; ;
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[min_index] :
                        sorted_asset_list[Mathf.Min(max_index, current_selection_index + 1)];
                    SelectionSingle(target_asset);
                    GUIUtility.keyboardControl = 0;
                }
                else if (Event.current.keyCode == KeyCode.PageUp)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[max_index] :
                        sorted_asset_list[Mathf.Max(min_index, current_selection_index - page_index_count)];
                    SelectionSingle(target_asset);
                    GUIUtility.keyboardControl = 0;
                }
                else if (Event.current.keyCode == KeyCode.PageDown)
                {
                    var target_asset = current_selection_index < 0 ?
                        currentSelectionEntry = sorted_asset_list[min_index] :
                        sorted_asset_list[Mathf.Min(max_index, current_selection_index + page_index_count)];
                    SelectionSingle(target_asset);
                    GUIUtility.keyboardControl = 0;
                }
            }

            #endregion
        }

        HashSet<UnityEngine.Object> selections = new HashSet<Object>();
        AssetEntry startSelectionEntry, currentSelectionEntry;

        void RenderAssetEntry(AssetEntry asset, ref Rect rect_entry)
        {
            if (asset.visible == false) return;
            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(rect_entry.width));

            var selected_color = this.browse_control_focused ? new Color(62 / 255f, 125 / 255f, 231 / 255f) : new Color(0.6f, 0.6f, 0.6f);
            if (selections.Contains(asset.asset)) EditorGUI.DrawRect(rect_entry, selected_color);

            var content = new GUIContent(asset.name, text_scriptable_object);
            EditorGUILayout.LabelField(content, EditorStyles.boldLabel, GUILayout.Width(EditorStyles.boldLabel.CalcSize(content).x));
            EditorGUILayout.LabelField(asset.path, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            rect_entry.y += rect_entry.height;

            var r = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
            {
                if (Event.current.control)
                    SelectionSingleToogle(asset);
                else if (Event.current.shift && currentSelectionEntry != null)
                    SelectionToRange(asset);
                else
                    SelectionSingle(asset);

                Repaint();
            }
        }

        void SelectionSingle(UnityEngine.Object obj)
        {
            var asset = this.sorted_asset_list.Find((a) => a.asset == obj);
            if (asset != null) SelectionSingle(asset);
        }

        void SelectionSingle(AssetEntry asset)
        {
            RecordCurrentSelection(asset.asset);
            bool same_obj = currentSelectionEntry == asset;
            currentSelectionEntry = startSelectionEntry = asset;
            selections.Clear();
            selections.Add(asset.asset);
            SelectionChanged();

            if (same_obj) EditorGUIUtility.PingObject(asset.asset);
        }

        void SelectionSingleToogle(AssetEntry asset)
        {
            if (selections.Contains(asset.asset) && selections.Count <= 1) return;
            currentSelectionEntry = startSelectionEntry = asset;
            if (selections.Contains(asset.asset)) selections.Remove(asset.asset); else selections.Add(asset.asset);
            SelectionChanged();
        }

        void SelectionSetAll()
        {
            selections.Clear();
            foreach (var asset in sorted_asset_list) selections.Add(asset.asset);
            SelectionChanged();
        }

        void SelectionToRange(AssetEntry asset)
        {
            if (startSelectionEntry == null)
            {
                SelectionSingle(asset);
                return;
            }

            selections.Clear();
            var index = sorted_asset_list.IndexOf(startSelectionEntry);
            var target_index = sorted_asset_list.IndexOf(asset);

            for (; index != target_index; index += index > target_index ? -1 : 1)
                selections.Add(sorted_asset_list[index].asset);
            selections.Add(asset.asset);

            currentSelectionEntry = asset;
            SelectionChanged();
        }

        void SelectionChanged()
        {
            this.currentObject = null;
            foreach (var selection in selections) { this.currentObject = selection; break; }
            this.currentEditor.SetTargetObjects(selections.ToArray());
            Repaint();
        }

        void RecordCurrentSelection(Object next_selection = null)
        {
            if (currentSelectionEntry != null && currentSelectionEntry.asset == next_selection) return;

            if (selecting_previous)
            {
                selecting_previous = false;
                return;
            }
            if (currentSelectionEntry != null)
            {
                var entry = (ScriptableObject)currentSelectionEntry.asset;
                if (EditorHistory.Count > 0 && entry == EditorHistory.Last())
                {
                    return;
                }
                EditorHistory.AddLast(entry);
                while (EditorHistory.Count > EDITOR_HISTORY_MAX) EditorHistory.RemoveFirst();
            }
        }

        static bool selecting_previous = false;
        void SelectPrevious()
        {
            while (EditorHistory.Count > 0 && EditorHistory.Last() == null) EditorHistory.RemoveLast();
            if (EditorHistory.Count <= 0) return;

            var last = EditorHistory.Last();
            EditorHistory.RemoveLast();
            selecting_previous = true;

            OpenObject(last);
        }

        Vector2 inspectScroll = new Vector2(0, 0);
        void OnInspect(Rect area)
        {
            area.x = area.y = 0;
            if (currentEditor == null) return;
            inspectScroll = EditorGUILayout.BeginScrollView(inspectScroll);
            currentEditor.RenderInspector();
            EditorGUILayout.EndScrollView();
        }

        static string ReverseString(string s)
        {
            char[] arr = s.ToCharArray();
            System.Array.Reverse(arr);
            return new string(arr);
        }

        void RenameCurrentEntry()
        {
            if (currentObject == null) return;
            var r = new Rect();
            r.position = this.position.position;
            r.y += 42;
            r.x += 32;
            r.width = BROWSE_AREA_WIDTH - 34;
            r.height = 18;
            PopupWindow.Show(r, new CreateNewEntryPopup(r, currentObject.name, FinishRenameCurrentEntry));
        }

        void FinishRenameCurrentEntry(string newName)
        {
            if (currentObject == null) return;
            var path = AssetDatabase.GetAssetPath(currentObject);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != currentObject) return;
            var folderPath = path.Substring(0, path.LastIndexOf('/') + 1);

            var new_path = folderPath + newName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(new_path) != null) return;

            currentObject.name = newName;
            EditorUtility.SetDirty(currentObject);
            AssetDatabase.RenameAsset(path, newName + ".asset");
            AssetDatabase.SaveAssets();
            SyncAssetEntry(currentSelectionEntry);
        }

        void CreateNewEntry()
        #region CreateNewEntry
        {            
            var types = currentType.Assembly.GetTypes().Where(t => t.IsSubclassOf(currentType)).ToList();
            types.Insert(0, currentType);
            types.RemoveAll((t) => t.IsAbstract);

            if (types.Count == 0) return;

            if (types.Count == 1)
            {
                CreateNewEntry(types[0]);
                return;
            } 

            if (types.Count > 1)
            {
                var menu = new GenericMenu();
                foreach (var type in types)
                {
                    var name = type.Name;
                    menu.AddItem(new GUIContent(name), false, () => CreateNewEntry(type));
                }
                menu.ShowAsContext();
            }
        }

        #region Create new entry subclass popup
        #endregion

        void CreateNewEntry(System.Type entryType)
        {
            var r = new Rect();
            r.position = this.position.position;
            r.y += 42;
            r.x += 32;
            r.width = BROWSE_AREA_WIDTH - 34;
            r.height = 18;
            PopupWindow.Show(r, new CreateNewEntryPopup(r, "", (name) => FinishCreateNewEntry(name, entryType)));
        }

        #region Create new entry popup
        class CreateNewEntryPopup : PopupWindowContent
        {
            Rect position;
            string entryValue = "";
            bool err = false;
            System.Action<string> callback;

            public CreateNewEntryPopup(Rect r, string currentEntryValue, System.Action<string> callback)
            {
                entryValue = currentEntryValue;
                this.position = r;
                this.callback = callback;
            }

            public override Vector2 GetWindowSize()
            {
                var size = this.position.size;
                if (err) size.y += size.y + 4;
                return size;
            }

            bool autoFocus = true;

            public override void OnGUI(Rect rect)
            {
                this.editorWindow.position = position;
                if (Event.current.keyCode == KeyCode.Escape) this.editorWindow.Close();

                rect.x = rect.y = 0;
                rect.height = 18;
                GUI.SetNextControlName("Name");
                entryValue = EditorGUI.TextField(rect, entryValue);
                GUI.FocusControl("Name");
                if (autoFocus)
                {
                    if (entryValue.Length > 0) EditorGUI.FocusTextInControl("Name");
                    autoFocus = false;
                }

                if (err)
                {
                    rect.y += rect.height + 2;
                    rect.x += 2; rect.width -= 4;
                    EditorGUI.HelpBox(rect, "Object with the same name already exist", MessageType.Error);
                }
                if (Event.current.keyCode == KeyCode.Return) this.ConfirmNewEntry();
            }

            void ConfirmNewEntry()
            {
                if (this.err) return;
                this.editorWindow.Close();
                this.callback?.Invoke(this.entryValue);
            }
        }
        #endregion

        void FinishCreateNewEntry(string name, System.Type entryType)
        {
            var e = this.currentEditor;
            CreateNewEntry(name, entryType);
        }
        
        public UnityEngine.Object CreateNewEntry(string name, System.Type entryType = null)
        {
            if (entryType == null) entryType = this.currentType;
            if (entryType != currentType && entryType.IsSubclassOf(currentType) == false) return null;

            var e = this.currentEditor;
            string path;

            if (this.currentEditor.CreateDataFolder)
            {
                AssetDatabase.CreateFolder(this.currentEditor.DefaultStoragePath, name);
                path = this.currentEditor.DefaultStoragePath + "/" + name + "/" + name + ".asset";
            }
            else
                path = this.currentEditor.DefaultStoragePath + "/" + name + ".asset";

            ScriptableObject instance = (ScriptableObject)System.Activator.CreateInstance(entryType);
            instance.name = name;

            AssetDatabase.CreateAsset(instance, path);
            this.AddAssetEntry(instance);

            return instance;
        }

        public List<UnityEngine.Object> CreateNewEntries(IEnumerable<string> names)
        {
            var e = this.currentEditor;
            List<UnityEngine.Object> entries = new List<UnityEngine.Object>();

            foreach (var name in names)
            {
                string path;
                if (this.currentEditor.CreateDataFolder)
                {
                    AssetDatabase.CreateFolder(this.currentEditor.DefaultStoragePath, name);
                    path = this.currentEditor.DefaultStoragePath + "/" + name + "/" + name + ".asset";
                }
                else
                    path = this.currentEditor.DefaultStoragePath + "/" + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object)) != null) continue;

                ScriptableObject instance = (ScriptableObject)System.Activator.CreateInstance(this.currentType);
                instance.name = name;

                AssetDatabase.CreateAsset(instance, path);
                entries.Add(instance);
            }

            AddAssetEntries(entries);
            return entries;
        }
        #endregion
    }
}