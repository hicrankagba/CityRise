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

[CustomEditor(typeof(RCCP_Gearbox))]
public class RCCP_GearboxEditor : Editor {

    RCCP_Gearbox prop;
    List<string> errorMessages = new List<string>();
    GUISkin skin;
    private Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Gearbox)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("Multiplies the received power from the engine --> clutch by x ratio, and transmits it to the differential. Higher ratios = faster accelerations, lower top speeds, lower ratios = slower accelerations, higher top speeds.", MessageType.Info, true);

        RCCP_LiteEditorHelper.BeginLockedSection("Gear Ratios");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gearRatios"), new GUIContent("Gear Ratios"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gearRPMs"), new GUIContent("Gear RPMs"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Gear State / Shifting");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentGear"), new GUIContent("Current Gear"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gearInput"), new GUIContent("Input"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentGearState"), new GUIContent("Current Gear State"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGearState"), new GUIContent("Default Gear State"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("forceToNGear"), new GUIContent("Force To N Gear"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("forceToRGear"), new GUIContent("Force To R Gear"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftingTime"), new GUIContent("Shifting Delay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftingNow"), new GUIContent("Shifting Now"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("dontShiftTimer"), new GUIContent("Dont Shift Timer"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("transmissionType"), new GUIContent("Transmission Type"));

        RCCP_LiteEditorHelper.BeginLockedSection("Automatic / Output");

        if (prop.transmissionType == RCCP_Gearbox.TransmissionType.Automatic || prop.transmissionType == RCCP_Gearbox.TransmissionType.Automatic_DNRP) {

            if (prop.transmissionType == RCCP_Gearbox.TransmissionType.Automatic_DNRP)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("automaticGearSelector"), new GUIContent("Automatic Gear Selector"));

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftThreshold"), new GUIContent("Shift Threshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftUpRPM"), new GUIContent("Shift Up RPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftDownRPM"), new GUIContent("Shift Down RPM"));
            EditorGUI.indentLevel--;

        }

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

            GUILayout.Button("Add Output To Differential");

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

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Gear Presets");

        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Button("1 Gear Preset");
        GUILayout.Button("2 Gears Preset");
        GUILayout.Button("3 Gears Preset");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Button("4 Gears Preset");
        GUILayout.Button("5 Gears Preset");
        GUILayout.Button("6 Gears Preset");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Button("7 Gears Preset");
        GUILayout.Button("8 Gears Preset");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        if (BehaviorSelected())
            EditorGUILayout.HelpBox("Settings with red labels will be overridden by the selected behavior in RCCP_Settings", MessageType.None);

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private void AddListener() {

        if (prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Differential>(true) == null) {

            Debug.LogError("Differential not found. Event is not added.");
            return;

        }

        prop.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Differential>(true),
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Differential>(true), targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(prop.outputEvent, methodDelegate);

    }

    private bool BehaviorSelected() {

        bool state = RCCP_Settings.Instance.overrideBehavior;

        if (prop.GetComponentInParent<RCCP_CarController>(true).ineffectiveBehavior)
            state = false;

        return state;

    }

}
#endif
