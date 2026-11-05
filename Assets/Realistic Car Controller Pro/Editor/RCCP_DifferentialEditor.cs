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

[CustomEditor(typeof(RCCP_Differential))]
public class RCCP_DifferentialEditor : Editor {

    RCCP_Differential prop;
    List<string> errorMessages = new List<string>();
    GUISkin skin;
    private Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Differential)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox(
            "Transmits the received power from the engine → clutch → gearbox to the axle. " +
            "Open differential = RPM difference between both wheels will decide to which wheel needs more traction or not. " +
            "Limited = almost same with open with slip limitation. Higher percents = more close to the locked system. " +
            "Locked = both wheels will have the same traction.",
            MessageType.Info,
            true
        );

        if (BehaviorSelected())
            GUI.color = Color.red;

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("differentialType"),
            new GUIContent("Differential Type")
        );

        GUI.color = guiColor;

        RCCP_LiteEditorHelper.BeginLockedSection("Differential Settings");

        if (prop.differentialType == RCCP_Differential.DifferentialType.Limited)
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("limitedSlipRatio"),
                new GUIContent("Limited Slip Ratio")
            );

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("finalDriveRatio"),
            new GUIContent("Final Drive Ratio")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("connectedAxle"),
            new GUIContent("Connected Axle"),
            true
        );

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("receivedTorqueAsNM"),
            new GUIContent("Received Torque As NM")
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("producedTorqueAsNM"),
            new GUIContent("Produced Torque As NM")
        );

        DrawConnectionButtons();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

            if (prop.GetComponentInParent<RCCP_CarController>(true).checkComponents) {

                prop.GetComponentInParent<RCCP_CarController>(true).checkComponents = false;

                if (errorMessages.Count > 0) {

                    if (EditorUtility.DisplayDialog(
                        "Realistic Car Controller Pro | Errors found",
                        errorMessages.Count + " Errors found!",
                        "Cancel",
                        "Check"
                    ))
                        Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

                } else {

                    Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;
                    Debug.Log("No errors found");

                }

            }

            EditorGUILayout.EndVertical();

        }

        if (BehaviorSelected())
            EditorGUILayout.HelpBox(
                "Settings with red labels will be overridden by the selected behavior in RCCP_Settings",
                MessageType.None
            );

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        // --- Statistics Section ---
        DrawStatisticsSection();

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    /// <summary>
    /// Draws read-only differential calculation statistics in the inspector.
    /// </summary>
    private void DrawStatisticsSection() {

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

        GUI.enabled = false;

        EditorGUILayout.FloatField(
            new GUIContent("Left Wheel RPM"),
            prop.leftWheelRPM
        );
        EditorGUILayout.FloatField(
            new GUIContent("Right Wheel RPM"),
            prop.rightWheelRPM
        );
        EditorGUILayout.FloatField(
            new GUIContent("Wheel Slip Ratio"),
            prop.wheelSlipRatio
        );
        EditorGUILayout.FloatField(
            new GUIContent("Left Wheel Slip Ratio"),
            prop.leftWheelSlipRatio
        );
        EditorGUILayout.FloatField(
            new GUIContent("Right Wheel Slip Ratio"),
            prop.rightWheelSlipRatio
        );
        EditorGUILayout.FloatField(
            new GUIContent("Output Left (Nm)"),
            prop.outputLeft
        );
        EditorGUILayout.FloatField(
            new GUIContent("Output Right (Nm)"),
            prop.outputRight
        );
        EditorGUILayout.FloatField(
            new GUIContent("Produced Torque As NM"),
            prop.producedTorqueAsNM
        );

        GUI.enabled = true;
        EditorGUILayout.EndVertical();

    }

    private void DrawConnectionButtons() {

        if (prop.connectedAxle == null) {

            RCCP_Axle[] axle = prop.GetComponentInParent<RCCP_CarController>(true)
                .GetComponentsInChildren<RCCP_Axle>(true);

            if (axle != null && axle.Length > 0) {

                for (int i = 0; i < axle.Length; i++) {

                    if (GUILayout.Button("Connect to " + axle[i].gameObject.name)) {

                        prop.connectedAxle = axle[i];
                        EditorUtility.SetDirty(prop);

                    }

                }

            }

        } else {

            GUI.color = Color.red;

            if (GUILayout.Button(
                "Remove connection to " + prop.connectedAxle.gameObject.name
            )) {

                bool decision = EditorUtility.DisplayDialog(
                    "Realistic Car Controller Pro | Remove connection to " +
                    prop.connectedAxle.gameObject.name,
                    "Are you sure want to remove connection to the " +
                    prop.connectedAxle.gameObject.name + "?",
                    "Yes",
                    "No"
                );

                if (decision) {

                    prop.connectedAxle = null;
                    EditorUtility.SetDirty(prop);

                }

            }

            GUI.color = guiColor;

        }

    }

    private bool BehaviorSelected() {

        bool state = RCCP_Settings.Instance.overrideBehavior;

        if (prop.GetComponentInParent<RCCP_CarController>(true).ineffectiveBehavior)
            state = false;

        return state;

    }

}
#endif
