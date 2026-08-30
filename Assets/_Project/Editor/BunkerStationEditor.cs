using UnityEditor;

[CustomEditor(typeof(BunkerStation))]
[CanEditMultipleObjects]
public sealed class BunkerStationEditor : Editor
{
    private SerializedProperty script;
    private SerializedProperty stationType;
    private SerializedProperty interactionText;
    private SerializedProperty progressionEnabled;
    private SerializedProperty progressionStationId;
    private SerializedProperty runTransitionTarget;
    private SerializedProperty panelManagerFallback;
    private SerializedProperty animator;
    private SerializedProperty animationTrigger;
    private SerializedProperty onInteract;

    private void OnEnable()
    {
        script = serializedObject.FindProperty("m_Script");
        stationType = serializedObject.FindProperty("stationType");
        interactionText = serializedObject.FindProperty("interactionText");
        progressionEnabled = serializedObject.FindProperty("progressionEnabled");
        progressionStationId = serializedObject.FindProperty("progressionStationId");
        runTransitionTarget = serializedObject.FindProperty("runTransitionTarget");
        panelManagerFallback = serializedObject.FindProperty("panelManagerFallback");
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
        EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(panelManagerFallback);

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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Progression", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(progressionEnabled);

        if (progressionEnabled.boolValue)
            EditorGUILayout.PropertyField(progressionStationId);
    }
}
