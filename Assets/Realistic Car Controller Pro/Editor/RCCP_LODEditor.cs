//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(RCCP_Lod))]
public class RCCP_LODEditor : Editor {

    RCCP_Lod prop;
    GUISkin skin;

    private void OnEnable() {

        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Lod)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("LOD applier, but not visually. Enables / disables the most heavier components of the vehicle controller relative to the main camera distance.\nHigh performance on lower LOD Factor values, but 0.8 would work fine. Otherwise some bugs would be noticable.", MessageType.Info, true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("forceToFirstLevel"), new GUIContent("Force To First Level"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("forceToLatestLevel"), new GUIContent("Force To Latest Level"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("lodFactor"), new GUIContent("LOD Factor"));

        RCCP_LiteEditorHelper.BeginLockedSection("LOD Level");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentLODLevel"), new GUIContent("Current LOD Level"));
        RCCP_LiteEditorHelper.EndLockedSection();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

            if (prop.GetComponentInParent<RCCP_CarController>(true).checkComponents)
                prop.GetComponentInParent<RCCP_CarController>(true).checkComponents = false;

            EditorGUILayout.EndVertical();

        }

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

}
#endif
