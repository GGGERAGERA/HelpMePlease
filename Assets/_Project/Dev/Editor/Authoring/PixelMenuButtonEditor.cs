#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(PixelMenuButton))]
public sealed class PixelMenuButtonEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("face"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("label"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("labelColors"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressedOffset"));
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
