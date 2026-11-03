using UnityEditor;

[CustomEditor(typeof(BunkerStation))]
[CanEditMultipleObjects]
public sealed class BunkerStationEditor : Editor
{
    private SerializedProperty script;
    private SerializedProperty stationType;
    private SerializedProperty interactionText;
    private SerializedProperty runTransitionTarget;
    private SerializedProperty panelManager;
    private SerializedProperty animator;
    private SerializedProperty animationTrigger;
    private SerializedProperty onInteract;

    private void OnEnable()
    {
        script = serializedObject.FindProperty("m_Script");
        stationType = serializedObject.FindProperty("stationType");
        interactionText = serializedObject.FindProperty("interactionText");
        runTransitionTarget = serializedObject.FindProperty("runTransitionTarget");
        panelManager = serializedObject.FindProperty("panelManager");
        animator = serializedObject.FindProperty("animator");
        animationTrigger = serializedObject.FindProperty("animationTrigger");
        onInteract = serializedObject.FindProperty("onInteract");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(script);

        EditorGUILayout.LabelField("Station", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stationType);
        EditorGUILayout.PropertyField(interactionText);

        if (!stationType.hasMultipleDifferentValues)
        {
            BunkerStationType type =
                (BunkerStationType)stationType.enumValueIndex;

            DrawTypeSpecificFields(type);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(panelManager);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTypeSpecificFields(BunkerStationType type)
    {
        switch (type)
        {
            case BunkerStationType.StartRun:
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Start Run", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(runTransitionTarget);
                return;

            case BunkerStationType.Animation:
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(animator);
                EditorGUILayout.PropertyField(animationTrigger);
                return;

            case BunkerStationType.CustomEvent:
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Custom", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(onInteract);
                return;

            case BunkerStationType.None:
                return;
        }
    }
}
