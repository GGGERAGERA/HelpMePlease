#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(RewardCardButton))]
public sealed class RewardCardButtonEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visualPresets"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackPreset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("headerAccent"));
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
