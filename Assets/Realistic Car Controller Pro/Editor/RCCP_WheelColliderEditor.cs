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
using System.Collections.Generic;

[CustomEditor(typeof(RCCP_WheelCollider))]
[CanEditMultipleObjects]
public class RCCP_WheelColliderEditor : Editor {

    // Reference to the target component
    private RCCP_WheelCollider prop;
    // Error message list for misconfiguration checks
    private List<string> errorMessages = new List<string>();
    // Custom GUI skin for styling
    private GUISkin skin;
    // Backup of original GUI color
    private Color guiColor;
    // Toggle for showing runtime statistics
    private bool showStatistics = true;

    // Serialized properties for inspector
    private SerializedProperty alignWheels;
    private SerializedProperty connectedAxle;
    private SerializedProperty wheelModel;
    private SerializedProperty offset;
    private SerializedProperty camber;
    private SerializedProperty caster;
    private SerializedProperty grip;
    private SerializedProperty width;
    private SerializedProperty drawSkid;
    private SerializedProperty deflated;
    private SerializedProperty deflatedRadiusMultiplier;
    private SerializedProperty deflatedStiffnessMultiplier;
    private SerializedProperty driftMode;
    private SerializedProperty wheelbase;
    private SerializedProperty trackWidth;

    // Runtime statistics properties
    private SerializedProperty isGrounded;
    private SerializedProperty isSkidding;
    private SerializedProperty groundIndex;
    private SerializedProperty motorTorque;
    private SerializedProperty brakeTorque;
    private SerializedProperty steerInput;
    private SerializedProperty handbrakeInput;
    private SerializedProperty negativeFeedbackIntensity;
    private SerializedProperty wheelRPM2Speed;
    private SerializedProperty wheelRPM;
    private SerializedProperty totalSlip;
    private SerializedProperty wheelSlipAmountForward;
    private SerializedProperty wheelSlipAmountSideways;
    private SerializedProperty totalWheelTemp;
    private SerializedProperty bumpForce;

    /// <summary>
    /// Called when the inspector is enabled. Finds all relevant serialized properties.
    /// </summary>
    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

        // Main settings
        alignWheels = serializedObject.FindProperty("alignWheels");
        connectedAxle = serializedObject.FindProperty("connectedAxle");
        wheelModel = serializedObject.FindProperty("wheelModel");

        // Wheel setup
        offset = serializedObject.FindProperty("offset");
        camber = serializedObject.FindProperty("camber");
        caster = serializedObject.FindProperty("caster");
        grip = serializedObject.FindProperty("grip");
        width = serializedObject.FindProperty("width");

        // Deflation settings
        deflated = serializedObject.FindProperty("deflated");
        deflatedRadiusMultiplier = serializedObject.FindProperty("deflatedRadiusMultiplier");
        deflatedStiffnessMultiplier = serializedObject.FindProperty("deflatedStiffnessMultiplier");

        // Additional options
        drawSkid = serializedObject.FindProperty("drawSkid");
        driftMode = serializedObject.FindProperty("driftMode");

        // Steering and dimensions
        wheelbase = serializedObject.FindProperty("wheelbase");
        trackWidth = serializedObject.FindProperty("trackWidth");

        // Runtime statistics
        isGrounded = serializedObject.FindProperty("isGrounded");
        isSkidding = serializedObject.FindProperty("isSkidding");
        groundIndex = serializedObject.FindProperty("groundIndex");
        motorTorque = serializedObject.FindProperty("motorTorque");
        brakeTorque = serializedObject.FindProperty("brakeTorque");
        steerInput = serializedObject.FindProperty("steerInput");
        handbrakeInput = serializedObject.FindProperty("handbrakeInput");
        negativeFeedbackIntensity = serializedObject.FindProperty("negativeFeedbackIntensity");
        wheelRPM2Speed = serializedObject.FindProperty("wheelRPM2Speed");
        totalWheelTemp = serializedObject.FindProperty("totalWheelTemp");
        bumpForce = serializedObject.FindProperty("bumpForce");

    }

    /// <summary>
    /// Draws the custom inspector GUI for RCCP_WheelCollider.
    /// </summary>
    public override void OnInspectorGUI() {

        prop = (RCCP_WheelCollider)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        RCCP_LiteEditorHelper.BeginLockedSection("Wheel Settings");

        float wheelRPM = prop.WheelRPM;
        float totalSlip = prop.TotalSlip;
        float wheelSlipAmountForward = prop.ForwardSlip;
        float wheelSlipAmountSideways = prop.SidewaysSlip;

        // Main properties
        EditorGUILayout.PropertyField(
            connectedAxle,
            new GUIContent("Connected Axle")
        );
        EditorGUILayout.PropertyField(
            wheelModel,
            new GUIContent("Wheel Model")
        );
        EditorGUILayout.PropertyField(
            alignWheels,
            new GUIContent("Align Wheels")
        );

        EditorGUILayout.Space();

        // Wheel Setup
        EditorGUILayout.LabelField("Wheel Setup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            offset,
            new GUIContent("Wheel Offset")
        );
        EditorGUILayout.PropertyField(
            camber,
            new GUIContent("Camber Angle")
        );
        EditorGUILayout.PropertyField(
            caster,
            new GUIContent("Caster Angle")
        );
        EditorGUILayout.PropertyField(
            width,
            new GUIContent("Wheel Width")
        );

        EditorGUILayout.Space();

        // Grip Settings
        EditorGUILayout.LabelField("Grip Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            grip,
            new GUIContent("Grip Multiplier", "Multiplies ground material friction stiffness. 1.0 = normal grip, 0.5 = half grip (slippery), 2.0 = double grip (sticky).")
        );
        EditorGUILayout.HelpBox(
            "Grip multiplies ground material stiffness at runtime:\n" +
            "- 0.0 = No grip (ice/oil)\n" +
            "- 0.5 = Low grip (wet surface)\n" +
            "- 1.0 = Normal (ground material defines grip)\n" +
            "- 1.5 = High grip (racing slicks)\n" +
            "- 2.0 = Maximum grip (arcade mode)",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // Deflation Settings
        EditorGUILayout.LabelField("Deflation Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            deflated,
            new GUIContent("Deflated")
        );
        EditorGUILayout.PropertyField(
            deflatedRadiusMultiplier,
            new GUIContent("Radius Multiplier")
        );
        EditorGUILayout.PropertyField(
            deflatedStiffnessMultiplier,
            new GUIContent("Stiffness Multiplier")
        );

        EditorGUILayout.Space();

        // Additional Options
        EditorGUILayout.LabelField("Additional Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            drawSkid,
            new GUIContent("Draw Skidmarks")
        );
        EditorGUILayout.PropertyField(
            driftMode,
            new GUIContent("Drift Mode")
        );

        EditorGUILayout.Space();

        // Steering / Dimensions
        EditorGUILayout.LabelField("Steering / Dimensions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            wheelbase,
            new GUIContent("Wheelbase")
        );
        EditorGUILayout.PropertyField(
         trackWidth,
         new GUIContent("Track Width")
     );

        EditorGUILayout.Space();

        // Runtime Statistics Foldout
        showStatistics = EditorGUILayout.Foldout(showStatistics, "Runtime Statistics");
        if (showStatistics) {
            if (Application.isPlaying) {
                EditorGUI.indentLevel++;

                // Ground Status
                EditorGUILayout.LabelField("Ground Status", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    isGrounded,
                    new GUIContent("Is Grounded")
                );
                EditorGUILayout.PropertyField(
                    isSkidding,
                    new GUIContent("Is Skidding")
                );
                EditorGUILayout.PropertyField(
                 groundIndex,
                 new GUIContent("Ground Index")
             );

                EditorGUILayout.Space();

                // Input / Forces
                EditorGUILayout.LabelField("Input / Forces", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    motorTorque,
                    new GUIContent("Motor Torque")
                );
                EditorGUILayout.PropertyField(
                    brakeTorque,
                    new GUIContent("Brake Torque")
                );
                EditorGUILayout.PropertyField(
                    steerInput,
                    new GUIContent("Steer Input")
                );
                EditorGUILayout.PropertyField(
                    handbrakeInput,
                    new GUIContent("Handbrake Input")
                );
                EditorGUILayout.PropertyField(
                    negativeFeedbackIntensity,
                    new GUIContent("Negative Feedback")
                );

                EditorGUILayout.Space();

                // Slip & Speed
                EditorGUILayout.LabelField("Slip & Speed", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                 wheelRPM2Speed,
                 new GUIContent("Wheel Speed")
             );

                EditorGUILayout.LabelField("Wheel RPM", wheelRPM.ToString("F2"));
                EditorGUILayout.LabelField("Total Slip", totalSlip.ToString("F2"));
                EditorGUILayout.LabelField("Forward Slip", wheelSlipAmountForward.ToString("F2"));
                EditorGUILayout.LabelField("Sideways Slip", wheelSlipAmountSideways.ToString("F2"));

                EditorGUILayout.Space();

                // Temperature & Bump
                EditorGUILayout.LabelField("Temperature & Bump", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    totalWheelTemp,
                    new GUIContent("Wheel Temperature")
                );
                EditorGUILayout.PropertyField(
                    bumpForce,
                    new GUIContent("Bump Force")
                );

                EditorGUI.indentLevel--;
            } else {
                EditorGUILayout.HelpBox("Runtime statistics are available in Play Mode only.", MessageType.Info);
            }
        }

        // Reset GUI color
        GUI.color = guiColor;

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        // Show Back button and auto-align in Editor
        if (!EditorUtility.IsPersistent(prop)) {
            if (GUILayout.Button("Back")) {
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;
            }
            if (!EditorApplication.isPlaying && prop.connectedAxle != null && prop.connectedAxle.autoAlignWheelColliders) {
                prop.AlignWheel();
            }
        }

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed) {
            EditorUtility.SetDirty(prop);
        }

    }

}
#endif
