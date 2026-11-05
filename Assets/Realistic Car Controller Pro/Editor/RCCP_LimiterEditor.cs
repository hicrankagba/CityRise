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

[CustomEditor(typeof(RCCP_Limiter))]
public class RCCP_LimiterEditor : Editor {

    RCCP_Limiter prop;
    GUISkin skin;

    private void OnEnable() {

        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Limiter)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("Limits the maximum speed of the vehicle per each gear. Be sure length of the float array is same with the length of the gearbox gears.", MessageType.Info, true);

        RCCP_LiteEditorHelper.BeginLockedSection("Speed Limiter");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("limitSpeedAtGear"), new GUIContent("Limit Speed At Gear", "Limits the speed of the vehicle at this gear."), true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("limitingNow"), new GUIContent("Limiting Now", "Speed of the vehicle exceeds the limit now?"), true);
        RCCP_LiteEditorHelper.EndLockedSection();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_OtherAddons>(true).gameObject;

            EditorGUILayout.EndVertical();

        }

        Undo.RecordObject(prop.transform, "Reset Limiter Transform");
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

}
#endif
