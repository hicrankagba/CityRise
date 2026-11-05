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
using UnityEditor.SceneManagement;

[CustomEditor(typeof(RCCP_Engine))]
public class RCCP_EngineEditor : Editor {

    RCCP_Engine prop;
    List<string> errorMessages = new List<string>();
    GUISkin skin;
    private Color guiColor;
    private bool statsEnabled = true;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Engine)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("Main power generator of the vehicle. Produces and transmits the generated power to the clutch.", MessageType.Info, true);

        RCCP_LiteEditorHelper.BeginLockedSection("Override");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideEngineRPM"), new GUIContent("Override Engine RPM"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRunning"), new GUIContent("Engine Running"));
        RCCP_LiteEditorHelper.BeginLockedSection();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineStarting"), new GUIContent("Engine Starting"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumSpeed"), new GUIContent("Maximum Speed"));

        if (prop.maximumSpeed <= 0f)
            EditorGUILayout.HelpBox("Maximum Speed is 0 or negative. Differential ratio auto-calculation will be skipped and current ratio will be kept.", MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRPM"), new GUIContent("Engine RPM"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minEngineRPM"), new GUIContent("Min Engine RPM"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEngineRPM"), new GUIContent("Max Engine RPM"));

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Acceleration / Deceleration");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineAccelerationRate"), new GUIContent("Acceleration Rate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDynamicAcceleration"), new GUIContent("Dynamic Acceleration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineCouplingToWheelsRate"), new GUIContent("Coupling To Wheels Rate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineDecelerationRate"), new GUIContent("Deceleration Rate"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Torque Curve");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoCreateNMCurve"), new GUIContent("Auto Create Torque Curve"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumTorqueAsNM"), new GUIContent("Maximum Torque As NM"));
        RCCP_LiteEditorHelper.BeginLockedSection();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("peakRPM"), new GUIContent("Peak Torque RPM"));

        if (!prop.autoCreateNMCurve)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NMCurve"), new GUIContent("Torque Curve NM"));
        else
            prop.CheckAndCreateNMCurve();
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRevLimiter"), new GUIContent("Rev Limiter"));

        if (prop.engineRevLimiter) {

            //EditorGUILayout.PropertyField(serializedObject.FindProperty("revLimiterThreshold"), new GUIContent("Rev Limiter Threshold"));
            RCCP_LiteEditorHelper.BeginLockedSection("Rev Limiter");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("revLimiterCutFrequency"), new GUIContent("Rev Limiter Cut Frequency"));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        }

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Turbo");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("turboCharged"), new GUIContent("Turbo Charged"));

        if (prop.turboCharged) {

            EditorGUILayout.PropertyField(serializedObject.FindProperty("turboChargePsi"), new GUIContent("Turbo Charge PSI"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTurboChargePsi"), new GUIContent("Max Turbo Charge PSI"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("turboChargerCoEfficient"), new GUIContent("Turbo Charger Coefficient"));

        }
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Friction / Inertia");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineFriction"), new GUIContent("Engine Friction"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineInertia"), new GUIContent("Engine Inertia"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Temperature");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("simulateEngineTemperature"), new GUIContent("Simulate Engine Temperature"));

        if (prop.simulateEngineTemperature) {

            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineTemperature"), new GUIContent("Engine Temperature"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("optimalTemperature"), new GUIContent("Optimal Temperature"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientTemperature"), new GUIContent("Ambient Temperature"));

        }
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("VVT / Knock Detection");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableVVT"), new GUIContent("Enable VVT"));

        if (prop.enableVVT) {

            EditorGUILayout.PropertyField(serializedObject.FindProperty("vvtOptimalRange"), new GUIContent("VVT Optimal Range"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vvtTorqueMultiplier"), new GUIContent("VVT Torque Multiplier"));

        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableKnockDetection"), new GUIContent("Enable Knock Detection"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        statsEnabled = EditorGUILayout.BeginToggleGroup(new GUIContent("Realtime Statistics"), statsEnabled);

        if (statsEnabled) {

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Statistics will be updated at runtime", MessageType.Info);

            GUI.enabled = false;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRPM"), new GUIContent("Current Engine RPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("producedTorqueAsNM"), new GUIContent("Produced Torque NM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fuelInput"), new GUIContent("Current Fuel Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("idleInput"), new GUIContent("Current Idle Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineLoad"), new GUIContent("Current Engine Load"));

            if (prop.turboCharged) {

                EditorGUILayout.PropertyField(serializedObject.FindProperty("turboChargePsi"), new GUIContent("Current Turbo PSI"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("turboBlowOut"), new GUIContent("Turbo Blow Out"));

            }

            if (prop.simulateEngineTemperature)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("engineTemperature"), new GUIContent("Current Engine Temperature"));

            if (prop.enableKnockDetection)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("knockFactor"), new GUIContent("Current Knock Factor"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("cutFuel"), new GUIContent("Rev Limiter Active"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRunning"), new GUIContent("Engine Running"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineStarting"), new GUIContent("Engine Starting"));

            GUI.enabled = true;

        }

        EditorGUILayout.EndToggleGroup();

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Output Event");
        GUI.skin = null;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputEvent"), new GUIContent("Output Event"));
        GUI.skin = skin;
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            RCCP_LiteEditorHelper.BeginLockedSection();
            if (GUILayout.Button("Add Output To Clutch")) {

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

        }

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        if (!EditorApplication.isPlaying) {

            prop.UpdateMaximumSpeed();

            if (GUI.changed)
                EditorSceneManager.MarkSceneDirty(prop.gameObject.scene);

        }

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private void AddListener() {

        if (prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Clutch>(true) == null) {

            Debug.LogError("Clutch not found. Event is not added.");
            return;

        }

        prop.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Clutch>(true),
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Clutch>(true), targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(prop.outputEvent, methodDelegate);

    }

}
#endif
