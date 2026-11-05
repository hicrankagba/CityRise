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
using UnityEditor.Events;
using UnityEngine.Events;

[CustomEditor(typeof(RCCP_Clutch))]
public class RCCP_ClutchEditor : Editor {

    RCCP_Clutch prop;
    List<string> errorMessages = new List<string>();
    GUISkin skin;
    private Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Clutch)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("Connecter between engine and the gearbox. Transmits the received power from the engine to the gearbox or not.", MessageType.Info, true);

        RCCP_LiteEditorHelper.BeginLockedSection("Clutch Settings");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clutchInput"), new GUIContent("Input"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clutchInertia"), new GUIContent("Inertia"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("automaticClutch"), new GUIContent("Automatic Clutch"));

        RCCP_LiteEditorHelper.BeginLockedSection("Clutch Behavior");

        if (prop.automaticClutch)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engageRPM"), new GUIContent("Engage RPM"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("forceToPressClutch"), new GUIContent("Force To Press Clutch"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressClutchWhileShiftingGears"), new GUIContent("Press Clutch While Shifting Gears"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressClutchWhileHandbraking"), new GUIContent("Press Clutch While Handbraking"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("receivedTorqueAsNM"), new GUIContent("Received Torque As NM"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("producedTorqueAsNM"), new GUIContent("Produced Torque As NM"));

        GUI.skin = null;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputEvent"), new GUIContent("Output Event"));
        GUI.skin = skin;

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Add Output To Gearbox")) {

                AddListener();
                EditorUtility.SetDirty(prop);

            }

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

            if (prop.GetComponentInParent<RCCP_CarController>(true).checkComponents) {

                prop.GetComponentInParent<RCCP_CarController>(true).checkComponents = false;

                if (errorMessages.Count > 0) {

                    if (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Errors found", errorMessages.Count + " Errors found!", "Cancel", "Check"))
                        Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

                } else {

                    Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;
                    Debug.Log("No errors found");

                }

            }

            EditorGUILayout.EndVertical();

        } else {

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        }

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private void AddListener() {

        if (prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Gearbox>(true) == null) {

            Debug.LogError("Gearbox not found. Event is not added.");
            return;

        }

        prop.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Gearbox>(true),
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Gearbox>(true), targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(prop.outputEvent, methodDelegate);

    }

}
#endif
