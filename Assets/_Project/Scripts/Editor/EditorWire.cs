using UnityEditor;
using UnityEngine;

namespace HollowTide.EditorTools
{
    /// <summary>
    /// Atribui campos [SerializeField] privados de componentes via SerializedObject, para os
    /// builders de cena/prefab não precisarem deixar campos públicos só por causa do editor.
    /// </summary>
    static class EditorWire
    {
        static SerializedProperty Prop(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            if (p == null) Debug.LogError($"[Build Demo] Campo '{field}' não existe em {so.targetObject.GetType().Name}.");
            return p;
        }

        public static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetObjectArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
