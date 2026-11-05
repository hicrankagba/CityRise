//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
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
[CustomEditor(typeof(RCCP_Settings))]
public class RCCP_SettingsEditor : Editor {

    RCCP_Settings RCCSettingsAsset;
    GUISkin skin;

    Color originalGUIColor;
    Color guiColor;
    Vector2 scrollPos;
    PhysicsMaterial[] physicMaterials;

    private enum DriftPreset { Subtle, Street, Pro, Arcade }

    bool foldGeneralSettings = false;
    bool foldBehaviorSettings = false;
    bool foldControllerSettings = false;
    bool foldUISettings = false;
    bool foldWheelPhysics = false;
    bool foldTagsAndLayers = false;
    bool foldResourcesSettings = false;
    bool foldOptimization = false;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

        foldGeneralSettings = RCCP_Settings.Instance.foldGeneralSettings;
        foldBehaviorSettings = RCCP_Settings.Instance.foldBehaviorSettings;
        foldControllerSettings = RCCP_Settings.Instance.foldControllerSettings;
        foldUISettings = RCCP_Settings.Instance.foldUISettings;
        foldWheelPhysics = RCCP_Settings.Instance.foldWheelPhysics;
        foldTagsAndLayers = RCCP_Settings.Instance.foldTagsAndLayers;
        foldResourcesSettings = RCCP_Settings.Instance.resourcesSettings;
        foldOptimization = RCCP_Settings.Instance.foldOptimization;

    }

    private void OnDestroy() {

        RCCP_Settings.Instance.foldGeneralSettings = foldGeneralSettings;
        RCCP_Settings.Instance.foldBehaviorSettings = foldBehaviorSettings;
        RCCP_Settings.Instance.foldControllerSettings = foldControllerSettings;
        RCCP_Settings.Instance.foldUISettings = foldUISettings;
        RCCP_Settings.Instance.foldWheelPhysics = foldWheelPhysics;
        RCCP_Settings.Instance.foldTagsAndLayers = foldTagsAndLayers;
        RCCP_Settings.Instance.resourcesSettings = foldResourcesSettings;
        RCCP_Settings.Instance.foldOptimization = foldOptimization;

    }

    public override void OnInspectorGUI() {

        RCCSettingsAsset = (RCCP_Settings)target;
        serializedObject.Update();
        GUI.skin = skin;
        RCCP_LiteEditorHelper.DrawLiteBanner();

        originalGUIColor = GUI.color;

        EditorGUIUtility.labelWidth = 250;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("RCCP Asset Settings Editor Window", EditorStyles.boldLabel);
        GUI.color = new Color(.75f, 1f, .75f);
        EditorGUILayout.LabelField("This editor will keep update necessary .asset files in your project for RCCP. Don't change directory of the ''Resources/RCCP Assets''.", EditorStyles.helpBox);
        GUI.color = originalGUIColor;
        EditorGUILayout.Space();

        EditorGUI.indentLevel++;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);

        EditorGUILayout.Space();

        foldGeneralSettings = EditorGUILayout.Foldout(foldGeneralSettings, "General Settings");

        if (foldGeneralSettings) {

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("General Settings", EditorStyles.boldLabel);

            RCCP_LiteEditorHelper.BeginLockedSection("Physics Tuning");

            SerializedProperty multithreadingProp = serializedObject.FindProperty("multithreading");
            if (multithreadingProp.boolValue) {
                multithreadingProp.boolValue = false;
            }
            EditorGUILayout.PropertyField(multithreadingProp, new GUIContent("Multithreading", "Use multithreading if current platform is supported. Fallback to false if platform doesn't support."));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideFixedTimeStep"), new GUIContent("Override FixedTimeStep", "Overrides the fixed timestep. More accurate physics in lower values, more performance on higher values. Default is 0.02, which means 50 frames per second. Not recommended to change this value."));

            if (RCCSettingsAsset.overrideFixedTimeStep)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedTimeStep"), new GUIContent("Fixed Timestep", "Overrides the fixed timestep. More accurate physics in lower values, more performance on higher values. Default is 0.02, which means 50 frames per second. Not recommended to change this value."));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideFPS"), new GUIContent("Override FPS", "Overrides maximum frames per second."));

            RCCP_LiteEditorHelper.BeginLockedSection("Physics Tuning");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAngularVelocity"), new GUIContent("Maximum Angular Velocity", "Maximum angular velocity limit of the vehicle. More stable angular movements on higher values, more speedy movements on lower values."));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            if (RCCSettingsAsset.overrideFPS)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxFPS"), new GUIContent("Maximum FPS", "Overrides maximum frames per second."));

            EditorGUILayout.HelpBox("You can find all references to any mode. Open up ''RCCP_Settings.cs'' and right click to any mode. Hit ''Find references'' to find all modifications.", MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("useFixedWheelColliders"), new GUIContent("Use Fixed WheelColliders", "Improves stability by increasing mass of the WheelColliders."));

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldBehaviorSettings = EditorGUILayout.Foldout(foldBehaviorSettings, "Behavior Settings");

        if (foldBehaviorSettings) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Behavior Settings", EditorStyles.boldLabel);

            GUI.color = new Color(.75f, 1f, .75f);
            EditorGUILayout.HelpBox("Using behavior preset will override wheelcollider settings, chassis joint, antirolls, and other stuff. Using ''Custom'' mode will not override anything.", MessageType.Info);
            GUI.color = originalGUIColor;

            RCCSettingsAsset.overrideBehavior = EditorGUILayout.BeginToggleGroup("Override Behavior", RCCSettingsAsset.overrideBehavior);

            EditorGUI.indentLevel++;

            RCCP_LiteEditorHelper.BeginLockedSection("Behavior Definitions");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("behaviorTypes"), new GUIContent("Behavior Types"), true);
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            List<string> behaviorTypeStrings = new List<string>();

            GUI.color = new Color(.5f, 1f, 1f, 1f);

            for (int i = 0; i < RCCSettingsAsset.behaviorTypes.Length; i++)
                behaviorTypeStrings.Add(RCCSettingsAsset.behaviorTypes[i].behaviorName);

            RCCSettingsAsset.behaviorSelectedIndex = GUILayout.Toolbar(RCCSettingsAsset.behaviorSelectedIndex, behaviorTypeStrings.ToArray());

            GUI.color = originalGUIColor;

            //if (RCCSettingsAsset.behaviorTypes.Length > 0 && RCCSettingsAsset.behaviorSelectedIndex < RCCSettingsAsset.behaviorTypes.Length) {

            //    RCCP_Settings.BehaviorType selectedBehavior = RCCSettingsAsset.behaviorTypes[RCCSettingsAsset.behaviorSelectedIndex];

            //    if (selectedBehavior.driftMode) {

            //        EditorGUILayout.Space();
            //        EditorGUILayout.LabelField("Drift Presets", EditorStyles.boldLabel);
            //        EditorGUILayout.HelpBox("Quick-apply drift parameter presets to the selected behavior type. You can fine-tune individual values after applying a preset.", MessageType.None);

            //        EditorGUILayout.BeginHorizontal();

            //        if (GUILayout.Button("Subtle\n(Beginner)", GUILayout.Height(36)))
            //            ApplyDriftPreset(selectedBehavior, DriftPreset.Subtle);

            //        if (GUILayout.Button("Street\n(Balanced)", GUILayout.Height(36)))
            //            ApplyDriftPreset(selectedBehavior, DriftPreset.Street);

            //        if (GUILayout.Button("Pro\n(Competition)", GUILayout.Height(36)))
            //            ApplyDriftPreset(selectedBehavior, DriftPreset.Pro);

            //        if (GUILayout.Button("Arcade\n(Forgiving)", GUILayout.Height(36)))
            //            ApplyDriftPreset(selectedBehavior, DriftPreset.Arcade);

            //        EditorGUILayout.EndHorizontal();

            //    }

            //}

            EditorGUI.indentLevel--;

            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldControllerSettings = EditorGUILayout.Foldout(foldControllerSettings, "Controller Settings");

        if (foldControllerSettings) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Main Controller Settings", EditorStyles.boldLabel);

            RCCP_LiteEditorHelper.BeginLockedSection();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSaveLoadInputRebind"), new GUIContent("Auto Save Load Input Rebind"));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            EditorGUILayout.Space();

            GUILayout.Label("Mobile Controller Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("mobileControllerEnabled"), new GUIContent("Mobile Controller Enabled"));

            if (RCCSettingsAsset.mobileControllerEnabled) {

                EditorGUILayout.Space();
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mobileController"), new GUIContent("Mobile Controller Type", "There are four controller types for mobile controller listed below."));
                EditorGUILayout.HelpBox("Mobile UI controller buttons will be used to receive player inputs through the RCCP_InputManager.", MessageType.Info);
                RCCP_LiteEditorHelper.BeginLockedSection("Input Tuning");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gyroSensitivity"), new GUIContent("Gyro Sensitivity", "Gyro sensitivity."));
                RCCP_LiteEditorHelper.EndLockedSection(guiColor);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();

            }

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldUISettings = EditorGUILayout.Foldout(foldUISettings, "UI Settings");

        if (foldUISettings) {

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("UI Dashboard Settings", EditorStyles.boldLabel);
            RCCP_LiteEditorHelper.BeginLockedSection();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useMPH"), new GUIContent("Use MPH", "Use KMH or MPH."));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useTelemetry"), new GUIContent("Use Telemetry", "Enables telemetry to display vehicle stats."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useInputDebugger"), new GUIContent("Use Input Debugger", "Displays informer texts on important inputs."));
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldWheelPhysics = EditorGUILayout.Foldout(foldWheelPhysics, "Wheel Physics Settings");

        if (foldWheelPhysics) {

            RCCP_LiteEditorHelper.BeginLockedSection("Wheel Physics");

            if (RCCP_GroundMaterials.Instance.frictions != null && RCCP_GroundMaterials.Instance.frictions.Length > 0) {

                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Ground Physic Materials", EditorStyles.boldLabel);

                physicMaterials = new PhysicsMaterial[RCCP_GroundMaterials.Instance.frictions.Length];

                for (int i = 0; i < physicMaterials.Length; i++) {

                    physicMaterials[i] = RCCP_GroundMaterials.Instance.frictions[i].groundMaterial;
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.ObjectField("Ground Physic Materials " + i, physicMaterials[i], typeof(PhysicsMaterial), false);
                    EditorGUILayout.EndVertical();

                }

                EditorGUILayout.Space();

            }

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            GUI.color = new Color(.5f, 1f, 1f, 1f);

            if (GUILayout.Button("Configure Ground Physic Materials"))
                Selection.activeObject = RCCP_GroundMaterials.Instance;

            GUI.color = originalGUIColor;

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldTagsAndLayers = EditorGUILayout.Foldout(foldTagsAndLayers, "Tags & Layers");

        if (foldTagsAndLayers) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Tags & Layers", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("setLayers"), new GUIContent("Set Layers Auto", "Sets layers of the vehicle automatically. Vehicle will be RCCP_Vehicle, wheels will be RCCP_WheelCollider, detachable parts will be RCCP_DetachablePart."), false);

            if (RCCSettingsAsset.setLayers) {

                RCCP_LiteEditorHelper.BeginLockedSection("Layer Config");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPLayer"), new GUIContent("Vehicle Layer"), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPWheelColliderLayer"), new GUIContent("WheelCollider Layer"), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPDetachablePartLayer"), new GUIContent("DetachablePart Layer"), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPPropLayer"), new GUIContent("Prop Layer"), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPObstacleLayer"), new GUIContent("Obstacle Layer"), false);
                RCCP_LiteEditorHelper.EndLockedSection(guiColor);
                GUI.color = new Color(.75f, 1f, .75f);
                EditorGUILayout.HelpBox("Be sure you have that tag and layer in your Tags & Layers", MessageType.Warning);
                EditorGUILayout.HelpBox("All vehicles powered by Realistic Car Controller Pro are using this layer. What does this layer do? It was used for masking wheel rays, light masks, and projector masks. Just create a new layer for vehicles from Edit --> Project Settings --> Tags & Layers, and select the layer here.", MessageType.Info);
                GUI.color = originalGUIColor;

            }

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldOptimization = EditorGUILayout.Foldout(foldOptimization, "Optimization");

        if (foldOptimization) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Optimization", EditorStyles.boldLabel);

            RCCP_LiteEditorHelper.BeginLockedSection("Optimization");
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useHeadLightsAsVertexLights"), new GUIContent("Head Lights As Vertex Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useBrakeLightsAsVertexLights"), new GUIContent("Brake Lights As Vertex Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useReverseLightsAsVertexLights"), new GUIContent("Reverse Lights As Vertex Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useIndicatorLightsAsVertexLights"), new GUIContent("Indicator Lights As Vertex Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useOtherLightsAsVertexLights"), new GUIContent("Other Lights As Vertex Lights"));
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            GUI.color = new Color(.75f, 1f, .75f);
            EditorGUILayout.HelpBox("Always use vertex lights for mobile platform. Even only one pixel light will drop your performance dramaticaly!", MessageType.Info);
            GUI.color = originalGUIColor;

            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        foldResourcesSettings = EditorGUILayout.Foldout(foldResourcesSettings, "Resources");

        if (foldResourcesSettings) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Resources", EditorStyles.boldLabel);

            GUI.color = new Color(.75f, 1f, .75f);
            EditorGUILayout.HelpBox("These are the initial resources for the initialization. RCCP will use these resources to setup things.", MessageType.Info);
            GUI.color = originalGUIColor;

            RCCP_LiteEditorHelper.BeginLockedSection("Resources");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineLowClipOn"), new GUIContent("Engine Low On"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineLowClipOff"), new GUIContent("Engine Low Off"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineMedClipOn"), new GUIContent("Engine Med On"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineMedClipOff"), new GUIContent("Engine Med Off"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineHighClipOn"), new GUIContent("Engine High On"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineHighClipOff"), new GUIContent("Engine High Off"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineIdleClipOn"), new GUIContent("Engine Idle On"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineIdleClipOff"), new GUIContent("Engine Idle Off"), false);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightsSetupData"), new GUIContent("Lights Setup Data"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightBox"), new GUIContent("Light Box"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("skidmarksManager"), new GUIContent("Skidmarks Manager"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exhaustGas"), new GUIContent("Exhaust Gas"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelBlur"), new GUIContent("Wheel Blur"), false);
#if BCG_URP || BCG_HDRP
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lensFlareData"), new GUIContent("LensFlare Data SRP"), false);
#endif
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPMainCamera"), new GUIContent("RCCP Main Camera"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPHoodCamera"), new GUIContent("RCCP Hood Camera"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPCinematicCamera"), new GUIContent("RCCP Cinematic Camera"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPWheelCamera"), new GUIContent("RCCP Wheel Camera"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPFixedCamera"), new GUIContent("RCCP Fixed Camera"), false);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPCanvas"), new GUIContent("RCCP UI Canvas"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RCCPTelemetry"), new GUIContent("RCCP Telemetry Canvas"), false);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("contactParticles"), new GUIContent("Contact Particles"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scratchParticles"), new GUIContent("Scratch Particles"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelSparkleParticles"), new GUIContent("Wheel Sparkle Particles"), false);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineStartClip"), new GUIContent("Engine Start Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("indicatorClip"), new GUIContent("Indicator Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bumpClip"), new GUIContent("Bump Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exhaustFlameClips"), new GUIContent("Exhaust Flame Clips"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gearClips"), new GUIContent("Gear Shifting Clips"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crashClips"), new GUIContent("Crash Clips"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NOSClip"), new GUIContent("NOS Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("turboClip"), new GUIContent("Turbo Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blowoutClip"), new GUIContent("Blowout Clips"), true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reversingClip"), new GUIContent("Reverse Transmission Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("windClip"), new GUIContent("Wind Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brakeClip"), new GUIContent("Brake Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelDeflateClip"), new GUIContent("Wheel Deflate Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelInflateClip"), new GUIContent("Wheel Inflate Clip"), false);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelFlatClip"), new GUIContent("Wheel Flat Clip"), false);

            EditorGUILayout.Space();

            if (GUILayout.Button("Configure Wheel Slip Sounds"))
                Selection.activeObject = RCCP_GroundMaterials.Instance;
            RCCP_LiteEditorHelper.EndLockedSection(guiColor);

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(GUI.skin.button);

        GUI.color = new Color(.5f, 1f, 1f, 1f);

        if (GUILayout.Button("Open PDF Documentation")) {

            UnityEngine.Object docAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RCCP_AssetPaths.documentationPath);
            if (docAsset != null)
                AssetDatabase.OpenAsset(docAsset);
            else
                EditorUtility.RevealInFinder(RCCP_AssetPaths.documentationFolder);

        }

        GUI.color = originalGUIColor;

        EditorGUILayout.LabelField("Realistic Car Controller Pro " + RCCP_Version.version + " \nBoneCracker Games", EditorStyles.centeredGreyMiniLabel, GUILayout.MaxHeight(50f));

        EditorGUILayout.LabelField("Developed by Ekrem Bugra Ozdoganlar", EditorStyles.centeredGreyMiniLabel, GUILayout.MaxHeight(50f));

        EditorGUILayout.EndVertical();

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(RCCSettingsAsset);

    }

    private void ApplyDriftPreset(RCCP_Settings.BehaviorType behavior, DriftPreset preset) {

        Undo.RecordObject(RCCSettingsAsset, "Apply Drift Preset: " + preset);

        switch (preset) {

            case DriftPreset.Subtle:

            // Light, controlled drifts. High grip minimums, fast recovery, low forces.
            behavior.driftAngleLimiter = true;
            behavior.driftAngleLimit = 25f;
            behavior.driftAngleCorrectionFactor = 5f;

            behavior.driftYawTorqueMultiplier = 0.4f;
            behavior.driftForwardForceMultiplier = 1000f;
            behavior.driftSidewaysForceMultiplier = 800f;
            behavior.driftMinSpeed = 25f;
            behavior.driftFullForceSpeed = 90f;
            behavior.driftThrottleYawFactor = 0.2f;

            behavior.driftRearSidewaysStiffnessMin = 0.65f;
            behavior.driftRearForwardStiffnessMin = 0.9f;
            behavior.driftFrontSidewaysStiffnessMin = 0.95f;
            behavior.driftFrictionResponseSpeed = 6f;
            behavior.driftFrictionRecoverySpeed = 8f;

            behavior.driftMaxAngularVelocity = 80f;
            behavior.driftCounterSteerRecoveryBoost = 3f;
            behavior.driftMomentumMaintenanceForce = 500f;
            behavior.driftForceSmoothing = 12f;
            break;

            case DriftPreset.Street:

            // Balanced for fun street-style drifting. Default values.
            behavior.driftAngleLimiter = true;
            behavior.driftAngleLimit = 35f;
            behavior.driftAngleCorrectionFactor = 3f;

            behavior.driftYawTorqueMultiplier = 0.7f;
            behavior.driftForwardForceMultiplier = 2000f;
            behavior.driftSidewaysForceMultiplier = 1500f;
            behavior.driftMinSpeed = 20f;
            behavior.driftFullForceSpeed = 80f;
            behavior.driftThrottleYawFactor = 0.3f;

            behavior.driftRearSidewaysStiffnessMin = 0.45f;
            behavior.driftRearForwardStiffnessMin = 0.8f;
            behavior.driftFrontSidewaysStiffnessMin = 0.9f;
            behavior.driftFrictionResponseSpeed = 8f;
            behavior.driftFrictionRecoverySpeed = 4f;

            behavior.driftMaxAngularVelocity = 120f;
            behavior.driftCounterSteerRecoveryBoost = 2f;
            behavior.driftMomentumMaintenanceForce = 800f;
            behavior.driftForceSmoothing = 8f;
            break;

            case DriftPreset.Pro:

            // Competition-style. High forces, low grip, wide angles allowed.
            behavior.driftAngleLimiter = false;
            behavior.driftAngleLimit = 50f;
            behavior.driftAngleCorrectionFactor = 2f;

            behavior.driftYawTorqueMultiplier = 1.2f;
            behavior.driftForwardForceMultiplier = 3000f;
            behavior.driftSidewaysForceMultiplier = 2500f;
            behavior.driftMinSpeed = 15f;
            behavior.driftFullForceSpeed = 70f;
            behavior.driftThrottleYawFactor = 0.5f;

            behavior.driftRearSidewaysStiffnessMin = 0.3f;
            behavior.driftRearForwardStiffnessMin = 0.7f;
            behavior.driftFrontSidewaysStiffnessMin = 0.85f;
            behavior.driftFrictionResponseSpeed = 12f;
            behavior.driftFrictionRecoverySpeed = 3f;

            behavior.driftMaxAngularVelocity = 180f;
            behavior.driftCounterSteerRecoveryBoost = 1.5f;
            behavior.driftMomentumMaintenanceForce = 1200f;
            behavior.driftForceSmoothing = 6f;
            break;

            case DriftPreset.Arcade:

            // Exaggerated, easy-to-control. High speed maintenance, forgiving recovery.
            behavior.driftAngleLimiter = true;
            behavior.driftAngleLimit = 40f;
            behavior.driftAngleCorrectionFactor = 2f;

            behavior.driftYawTorqueMultiplier = 1.0f;
            behavior.driftForwardForceMultiplier = 2500f;
            behavior.driftSidewaysForceMultiplier = 2000f;
            behavior.driftMinSpeed = 15f;
            behavior.driftFullForceSpeed = 60f;
            behavior.driftThrottleYawFactor = 0.4f;

            behavior.driftRearSidewaysStiffnessMin = 0.4f;
            behavior.driftRearForwardStiffnessMin = 0.85f;
            behavior.driftFrontSidewaysStiffnessMin = 0.95f;
            behavior.driftFrictionResponseSpeed = 10f;
            behavior.driftFrictionRecoverySpeed = 6f;

            behavior.driftMaxAngularVelocity = 100f;
            behavior.driftCounterSteerRecoveryBoost = 2.5f;
            behavior.driftMomentumMaintenanceForce = 1500f;
            behavior.driftForceSmoothing = 10f;
            break;

        }

        EditorUtility.SetDirty(RCCSettingsAsset);

    }

}
#endif
