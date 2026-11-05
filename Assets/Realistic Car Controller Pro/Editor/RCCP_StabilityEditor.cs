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

[CustomEditor(typeof(RCCP_Stability))]
public class RCCP_StabilityEditor : Editor {

    RCCP_Stability prop;
    List<string> errorMessages = new List<string>();
    GUISkin skin;
    private Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Stability)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("ABS = Anti-skid braking system, ESP = Detects vehicle skidding movements, and actively counteracts them., TCS = Detects if a loss of traction occurs among the vehicle's wheels.", MessageType.Info, true);

        if (BehaviorSelected())
            GUI.color = Color.red;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("ABS"), new GUIContent("ABS"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ESP"), new GUIContent("ESP"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TCS"), new GUIContent("TCS"));

        GUI.color = guiColor;

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("ABS / ESP / TCS Thresholds");

        if (prop.ABS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engageABSThreshold"), new GUIContent("Engage ABS Threshold"));
        if (prop.ESP)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engageESPThreshold"), new GUIContent("Engage ESP Threshold"));
        if (prop.TCS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engageTCSThreshold"), new GUIContent("Engage TCS Threshold"));

        EditorGUILayout.Space();

        if (prop.ABS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ABSIntensity"), new GUIContent("ABSIntensity"));
        if (prop.ESP)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ESPIntensity"), new GUIContent("ESPIntensity"));
        if (prop.TCS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TCSIntensity"), new GUIContent("TCSIntensity"));

        EditorGUILayout.Space();

        if (prop.ABS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ABSEngaged"), new GUIContent("ABS Engaged"));
        if (prop.ESP)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ESPEngaged"), new GUIContent("ESP Engaged"));
        if (prop.TCS)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TCSEngaged"), new GUIContent("TCS Engaged"));

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.Space();

        if (BehaviorSelected())
            GUI.color = Color.red;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("steeringHelper"), new GUIContent("Steering Helper"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tractionHelper"), new GUIContent("Traction Helper"));

        RCCP_LiteEditorHelper.BeginLockedSection("Helpers / Drift Angle");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("angularDragHelper"), new GUIContent("Angular Drag Helper"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("driftAngleLimiter"), new GUIContent("Drift Angle Limiter"));

        if (prop.driftAngleLimiter) {

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxDriftAngle"), new GUIContent("Max Drift Angle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftAngleCorrectionFactor"), new GUIContent("Drift Angle Correction Factor"));
            EditorGUI.indentLevel--;

        }

        EditorGUILayout.Space();

        if (prop.steeringHelper)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steerHelperStrength"), new GUIContent("Steering Helper Strength"));
        if (prop.tractionHelper)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tractionHelperStrength"), new GUIContent("Traction Helper Strength"));
        if (prop.angularDragHelper)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("angularDragHelperStrength"), new GUIContent("Angular Drag Helper Strength"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("driftForceSmoothing"), new GUIContent("Drift Force Smoothing", "Smoothing speed for drift force transitions. Higher values mean faster response, lower values mean smoother transitions."));

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);
        GUI.color = guiColor;

        EditorGUILayout.Space();

        // Drift system parameters section.
        bool driftModeActive = RCCP_Settings.Instance.SelectedBehaviorType != null && RCCP_Settings.Instance.SelectedBehaviorType.driftMode;

        if (driftModeActive || Application.isPlaying) {

            GUI.enabled = false;

            EditorGUILayout.LabelField("Drift System", EditorStyles.boldLabel);

            if (Application.isPlaying) {

                EditorGUILayout.Slider(new GUIContent("Drift Intensity", "Current drift intensity (0-1). Computed from rear wheel slip magnitude."), prop.driftIntensity, 0f, 1f);

            }

            if (BehaviorSelected())
                GUI.color = Color.red;

            EditorGUILayout.LabelField("Forces", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftYawTorqueMultiplier"), new GUIContent("Yaw Torque Multiplier", "Multiplier for yaw torque applied during drift. Higher values make the car rotate faster when drifting."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftForwardForceMultiplier"), new GUIContent("Forward Force Multiplier", "Forward push force during drift to maintain speed. Higher values reduce speed loss while sliding."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftSidewaysForceMultiplier"), new GUIContent("Sideways Force Multiplier", "Lateral push force during drift. Higher values push the car further sideways for wider drifts."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftMinSpeed"), new GUIContent("Min Speed", "Minimum speed (km/h) required for drift forces to activate. Below this speed, no drift assistance is applied."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftFullForceSpeed"), new GUIContent("Full Force Speed", "Speed (km/h) at which drift forces reach full strength. Forces scale linearly between min speed and this value."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftThrottleYawFactor"), new GUIContent("Throttle Yaw Factor", "How much throttle input alone contributes to yaw rotation. Higher values allow initiating drift with throttle without steering."));
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Friction", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftRearSidewaysStiffnessMin"), new GUIContent("Rear Sideways Stiffness Min", "Minimum rear tire sideways grip during full drift. Lower values allow more lateral sliding. 1.0 = no grip reduction."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftRearForwardStiffnessMin"), new GUIContent("Rear Forward Stiffness Min", "Minimum rear tire forward grip during full drift. Lower values cause more speed loss. 1.0 = no grip reduction."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftFrontSidewaysStiffnessMin"), new GUIContent("Front Sideways Stiffness Min", "Minimum front tire sideways grip during drift. Higher values keep front-end responsive for steering control."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftFrictionResponseSpeed"), new GUIContent("Friction Response Speed", "How quickly tire grip reduces when entering a drift. Higher values make grip loss more immediate."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftFrictionRecoverySpeed"), new GUIContent("Friction Recovery Speed", "How quickly tire grip recovers when exiting a drift. Higher values make grip recovery faster."));
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Recovery", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftMaxAngularVelocity"), new GUIContent("Max Angular Velocity", "Maximum angular velocity (deg/s) allowed during drift. Prevents uncontrollable spins. 0 = no limit."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftCounterSteerRecoveryBoost"), new GUIContent("Counter Steer Recovery Boost", "Multiplier for recovery force when counter-steering during drift. Higher values make recovery from drifts easier."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("driftMomentumMaintenanceForce"), new GUIContent("Momentum Maintenance Force", "Constant forward force applied during drift to maintain momentum. Higher values prevent speed loss while drifting."));
            EditorGUI.indentLevel--;

            GUI.enabled = true;
            GUI.color = guiColor;

        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

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

        if (BehaviorSelected())
            EditorGUILayout.HelpBox("Settings with red labels will be overridden by the selected behavior in RCCP_Settings", MessageType.None);

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private bool BehaviorSelected() {

        bool state = RCCP_Settings.Instance.overrideBehavior;

        if (prop.GetComponentInParent<RCCP_CarController>(true).ineffectiveBehavior)
            state = false;

        return state;

    }

}
#endif
