using UnityEngine;
using UnityEditor;

public class BattalionConfigWindow : EditorWindow
{
    private BattalionConfig config;
    private Vector2 scroll;

    [MenuItem("Conquest/Battalion Config")]
    public static void ShowWindow()
    {
        var w = GetWindow<BattalionConfigWindow>("Battalion Config");
        w.minSize = new Vector2(300, 400);
    }

    void OnEnable()
    {
        LoadConfig();
    }

    void LoadConfig()
    {
        var guids = AssetDatabase.FindAssets("t:BattalionConfig");
        if (guids.Length > 0)
        {
            config = AssetDatabase.LoadAssetAtPath<BattalionConfig>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (config == null)
        {
            // Create default
            if (!AssetDatabase.IsValidFolder("Assets/Config"))
                AssetDatabase.CreateFolder("Assets", "Config");
            config = CreateInstance<BattalionConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Config/BattalionConfig.asset");
            AssetDatabase.SaveAssets();
        }
    }

    void OnGUI()
    {
        if (config == null) { LoadConfig(); if (config == null) return; }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        var so = new SerializedObject(config);
        so.Update();

        EditorGUILayout.LabelField("Soldier", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("attackRange"));
        EditorGUILayout.PropertyField(so.FindProperty("attackCooldown"));
        EditorGUILayout.PropertyField(so.FindProperty("dashSpeed"));
        EditorGUILayout.PropertyField(so.FindProperty("dashHeight"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Battalion", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("moveSpeed"));
        EditorGUILayout.PropertyField(so.FindProperty("detectionRange"));
        EditorGUILayout.PropertyField(so.FindProperty("bobHeight"));
        EditorGUILayout.PropertyField(so.FindProperty("bobFrequency"));
        EditorGUILayout.PropertyField(so.FindProperty("formationSpacing"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Enemy AI", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("miningDuration"));
        EditorGUILayout.PropertyField(so.FindProperty("attackDuration"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("NavMeshAgent", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("agentRadius"));
        EditorGUILayout.PropertyField(so.FindProperty("agentHeight"));

        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Save & Apply", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
