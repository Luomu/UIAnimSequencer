using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UIAnimSequencePlayer))]
[CanEditMultipleObjects]
public class UIAnimSequencePlayerEditor : Editor
{
    private UIAnimSequencePlayer player;

    private void OnEnable()
    {
        player = (UIAnimSequencePlayer)target;
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty defaultAnimSequence = serializedObject.FindProperty("m_defaultAnimSequence");
        SerializedProperty animSequences = serializedObject.FindProperty("m_animSequences");
        SerializedProperty playOnAwake = serializedObject.FindProperty("PlayOnAwake");
        SerializedProperty setToInitialStateOnAwake = serializedObject.FindProperty("SetToInitialStateOnAwake");
        SerializedProperty resetAfterPlay = serializedObject.FindProperty("ResetAfterPlay");

        EditorGUILayout.PropertyField(defaultAnimSequence);
        EditorGUILayout.PropertyField(playOnAwake);
        EditorGUILayout.PropertyField(setToInitialStateOnAwake);
        EditorGUILayout.PropertyField(resetAfterPlay);
        EditorGUILayout.PropertyField(animSequences, true);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Editing");
        if (GUILayout.Button("Edit Sequence") && player.CurrentAnimSequence != null)
        {
            player.CurrentAnimSequence.AnimPlayer = player;
            Selection.activeObject = player.CurrentAnimSequence;
        }

        EditorGUILayout.Space();
        DrawAnimSequences(player);
        DrawPreviewTools(player);
    }

    public static void DrawAnimSequences(UIAnimSequencePlayer player)
    {
        bool prevState = GUI.enabled;
        GUI.enabled = !player.Animating;
        Color selectedColor = Color.green * 0.85f;
        if (player.CurrentAnimSequence == player.DefaultAnimSequence) GUI.backgroundColor = selectedColor;
        if (GUILayout.Button("Default Sequence"))
        {
            if (player.CurrentAnimSequence != null && player.CurrentAnimSequence.AnimPlayer != null)
            {
                player.CurrentAnimSequence.AnimPlayer = null;
                player.DefaultAnimSequence.AnimPlayer = player;
                Selection.activeObject = player.DefaultAnimSequence;
            }
            player.CurrentAnimSequence = null;
        }

        foreach (var animSequence in player.AnimSequences)
        {
            GUI.backgroundColor = player.CurrentAnimSequence == animSequence ? selectedColor : Color.white;
            if (animSequence != null && GUILayout.Button(animSequence.name))
            {
                if (player.CurrentAnimSequence != null && player.CurrentAnimSequence.AnimPlayer != null)
                {
                    animSequence.AnimPlayer = player;
                    Selection.activeObject = animSequence;
                }
                player.CurrentAnimSequence.AnimPlayer = null;
                player.CurrentAnimSequence = animSequence;
            }
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = prevState;
    }

    public static void DrawPreviewTools(UIAnimSequencePlayer player)
    {
        if (player == null) return;
        string state = string.Empty;
        if (player.Animating)
        {
            //GUILayout.Label("Running...");
            state = "Playing...";
            GUI.enabled = false;
        }
        else
        {
            //GUILayout.Label("Idle");
            state = "Idling...";
            GUI.enabled = true;
        }
        EditorGUILayout.LabelField("Animation State", state);

        if (GUILayout.Button("Preview"))
        {
            player.EditorPreview();
        }


        bool wasEnabled = GUI.enabled;
        GUI.enabled = true;
        if (GUILayout.Button("Stop preview"))
        {
            player.ResetAfterPreview();
        }
        GUI.enabled = wasEnabled;
    }

    void EditorUpdate()
    {
        var player = (UIAnimSequencePlayer)target;
        if (player.Animating)
        {
            player.EditorForceUpdate();
            EditorUtility.SetDirty(player);
            //SceneView.RepaintAll();
        }
    }
}
