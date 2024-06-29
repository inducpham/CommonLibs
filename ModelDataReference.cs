using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ModelDataHelper
{
    public abstract class ModelDataObject : ScriptableObject
    {
        public abstract string Code { get; }
        public abstract ModelDataObject LookUpReference(string code);

    }

    //[Beebyte.Obfuscator.Skip]
    [System.Serializable]
    public class ModelDataReference<T> : ISerializationCallbackReceiver where T : ModelDataObject
    {
        public string Code => code;

        [SerializeField] string code;
#if UNITY_EDITOR
        [SerializeField] T value;
#endif

        private static T instance;

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            code = value?.Code?? "";
#endif
        }

        public void OnAfterDeserialize()
        {
        }

        public T Reference
        {
            get
            {
                if (instance == null) instance = System.Activator.CreateInstance<T>();
                return (T)instance.LookUpReference(code);
            }
        }

        public static bool operator ==(ModelDataReference<T> a, ModelDataReference<T> b)
        {
            if (((object)a) == null && ((object)b) == null) return true;
            if (((object)a) == null || ((object)b) == null) return false;
            if (a.GetType() != b.GetType()) return false;
            return a.code == b.code;
        }

        public static bool operator !=(ModelDataReference<T> a, ModelDataReference<T> b)
        {
            if (((object)a) == null && ((object)b) == null) return false;
            if (((object)a) == null || ((object)b) == null) return true;
            if (a.GetType() != b.GetType()) return true;
            return a.code != b.code;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ModelDataReference<>))]
    public class ModelDataPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var value = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, value, label);
            EditorGUI.EndProperty();
        }
    }
#endif
}