using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TextEditable
{
    [System.Serializable]
    public class Text
    {
        public enum Mood
        {
            none, smile, sad
        }

        public string name;
        public bool appear;
        public Sprite sprite;
        public SpriteRenderer prefab;

        [TextEditable.HideDefault]
        public Mood mood;

        [TextEditable.DefaultStringField]
        public string content;
    }

    [CreateAssetMenu]
    public class TextInputExample : ScriptableObject
    {
        [TextEditable.PropertyEditable]
        public Text text, text2;
    }

}