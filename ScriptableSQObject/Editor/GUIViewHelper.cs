using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SSQObject
{
    public static class GUIViewHelper
    {
        public static EditorWindow GetCurrentWindow()
        {
            try
            {
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GUIView");
                var current = type.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.HostView");
                var actualView = type.GetProperty("actualView", BindingFlags.NonPublic | BindingFlags.Instance);
                var window = (EditorWindow)actualView.GetValue(current.GetValue(null, null), null);
                return window;
            }
            catch
            {
                return null;
            }
        }

        public static float GetEditorWindowScrollPosition(EditorWindow window)
        {
            System.Type T = System.Type.GetType("UnityEditor.InspectorWindow,UnityEditor");
            if (T == null) return 0;

            var field = T.GetField("m_ScrollView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return 0;

            ScrollView sv = (ScrollView)field.GetValue(window);
            var scrollPosition = sv.scrollOffset.y;
            var windowHeight = sv.contentViewport.layout.height;

            if (window.GetType().Name == "InspectorWindow")
            {
                scrollPosition -= 100;
            }

            return scrollPosition;
        }

        public static Rect GetCurrentEditorWindowRect()
        {
            var window = GetCurrentWindow();
            if (window == null) return new Rect(0, 0, 0, 0);

            var scroll = GetEditorWindowScrollPosition(window);
            var rect = window.position;
            rect.x = 0;
            rect.y = scroll;
            return rect;
        }
    }
}