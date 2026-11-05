//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;
using System;
using UnityEditor.Events;

/// <summary>
/// Creates a new RCCP vehicle with all required components and event wiring.
/// Lite version — creates a fully functional vehicle with sensible defaults.
/// For advanced vehicle setup with visual wizard, wheel assignment, drivetrain presets,
/// and handling configuration, upgrade to RCCP Pro.
/// </summary>
public class RCCP_CreateNewVehicle {

    private const string ProUpgradeURL = RCCP_AssetPaths.assetStorePath;

    public static RCCP_CarController NewVehicle(GameObject vehicle) {

        if (vehicle == null)
            return null;

        if (vehicle.GetComponentInParent<RCCP_CarController>(true) != null) {

            EditorUtility.DisplayDialog("Realistic Car Controller Pro | Already Has RCCP_CarController", "Selected vehicle already has RCCP_CarController. Are you sure you didn't pick the wrong house, oh vehicle?", "Close");
            return null;

        }

        if (EditorUtility.IsPersistent(vehicle)) {

            EditorUtility.DisplayDialog("Realistic Car Controller Pro | Please select a vehicle in the scene", "Please select a vehicle in the scene, not in the project. Drag and drop the vehicle model to the scene, and try again.", "Close");
            return null;

        }

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(vehicle);

        if (isPrefab) {

            bool isModelPrefab = PrefabUtility.IsPartOfModelPrefab(vehicle);
            bool unpackPrefab = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpack Prefab", "This gameobject is connected to a " + (isModelPrefab ? "model" : "") + " prefab. Would you like to unpack the prefab completely? If you don't unpack it, you won't be able to move, reorder, or delete any children instance of the prefab.", "Unpack", "Don't Unpack");

            if (unpackPrefab)
                PrefabUtility.UnpackPrefabInstance(vehicle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        bool foundRigids = false;

        if (vehicle.GetComponentInChildren<Rigidbody>(true))
            foundRigids = true;

        if (foundRigids) {

            bool removeRigids = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Rigidbodies Found", "Additional rigidbodies found in your vehicle. Additional rigidbodies will affect vehicle behavior directly.", "Remove Them", "Leave Them");

            if (removeRigids) {

                foreach (Rigidbody rigidbody in vehicle.GetComponentsInChildren<Rigidbody>(true))
                    UnityEngine.Object.DestroyImmediate(rigidbody);

            }

        }

        bool foundWheelColliders = false;

        if (vehicle.GetComponentInChildren<WheelCollider>(true))
            foundWheelColliders = true;

        if (foundWheelColliders) {

            bool removeWheelColliders = EditorUtility.DisplayDialog("Realistic Car Controller Pro | WheelColliders Found", "Additional wheelcolliders found in your vehicle.", "Remove Them", "Leave Them");

            if (removeWheelColliders) {

                foreach (WheelCollider wc in vehicle.GetComponentsInChildren<WheelCollider>(true))
                    UnityEngine.Object.DestroyImmediate(wc);

            }

        }

        bool fixPivot = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Fix Pivot Position Of The Vehicle", "Would you like to fix pivot position of the vehicle? If your vehicle has correct pivot position, select no.", "Fix", "No");

        if (fixPivot) {

            GameObject pivot = new GameObject(vehicle.name);
            pivot.transform.position = RCCP_GetBounds.GetBoundsCenter(vehicle.transform);
            pivot.transform.rotation = vehicle.transform.rotation;

            pivot.AddComponent<RCCP_CarController>();

            vehicle.transform.SetParent(pivot.transform);
            Selection.activeGameObject = pivot;
            vehicle = pivot;

        } else {

            GameObject selectedVehicle = vehicle;

            selectedVehicle.AddComponent<RCCP_CarController>();
            Selection.activeGameObject = selectedVehicle;
            vehicle = selectedVehicle;

        }

        Rigidbody rigid = vehicle.GetComponent<Rigidbody>();
        rigid.mass = 1350f;
        rigid.linearDamping = .0025f;
        rigid.angularDamping = .35f;
        rigid.interpolation = RigidbodyInterpolation.Interpolate;
        rigid.collisionDetectionMode = CollisionDetectionMode.Discrete;

        RCCP_CarController newVehicle = vehicle.GetComponent<RCCP_CarController>();

        // Add all drivetrain components and wire events.
        AddAllComponents(newVehicle);

        // Add all addon components.
        AddAllAddonComponents(newVehicle);

        EditorUtility.SetDirty(newVehicle);

        Undo.RegisterCreatedObjectUndo(vehicle, "RCCP Create New Vehicle");

        // Attach the setup helper so new users see validation warnings.
        if (!vehicle.GetComponent<RCCP_LiteSetupHelper>())
            vehicle.AddComponent<RCCP_LiteSetupHelper>();

        Debug.Log("RCCP Lite: Vehicle created successfully! Complete the two required steps shown in the Setup Helper inspector.");

        // Promote RCCP Pro's fully featured Setup Wizard.
        bool openProPage = EditorUtility.DisplayDialog(
            "RCCP Lite | Vehicle Created Successfully",
            "Your vehicle has been created with default settings.\n\n" +
            "Two required steps to finish setup:\n\n" +
            "1. Assign wheel models — Select each axle (front & rear) and drag your wheel meshes into the Left/Right Wheel Model slots.\n\n" +
            "2. Add a body collider — Add a MeshCollider or BoxCollider to your vehicle's body mesh so it can collide with the environment.\n\n" +
            "A Setup Helper component has been added to your vehicle to guide you through these steps.\n\n" +
            "Want more control? RCCP Pro includes a fully featured Setup Wizard with automatic wheel detection, drivetrain presets, and one-click setup.",
            "Get RCCP Pro", "Continue with Lite");

        if (openProPage)
            Application.OpenURL(ProUpgradeURL);

        return newVehicle;

    }

    public static void AddAllComponents(RCCP_CarController prop) {

        AddEngine(prop);
        AddClutch(prop);
        AddGearbox(prop);
        AddAxles(prop);
        AddDifferential(prop);
        AddDifferentialToAxle(prop);
        AddEngineToClutchListener(prop);
        AddClutchToGearboxListener(prop);
        AddGearboxToDifferentialListener(prop);

    }

    public static void AddAllAddonComponents(RCCP_CarController prop) {

        AddInputs(prop);
        AddAero(prop);
        AddStability(prop);
        AddAudio(prop);
        AddCustomizer(prop);
        AddDamage(prop);
        AddLights(prop);
        AddParticles(prop);
        AddLOD(prop);
        AddOtherAddons(prop);

    }

    public static void AddEngine(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Engine>(true))
            return;

        GameObject subject = new GameObject("RCCP_Engine");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(0);
        subject.AddComponent<RCCP_Engine>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddClutch(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Clutch>(true))
            return;

        GameObject subject = new GameObject("RCCP_Clutch");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(1);
        subject.gameObject.AddComponent<RCCP_Clutch>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddGearbox(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Gearbox>(true))
            return;

        GameObject subject = new GameObject("RCCP_Gearbox");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(2);
        subject.gameObject.AddComponent<RCCP_Gearbox>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddDifferential(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Differential");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(3);
        subject.gameObject.AddComponent<RCCP_Differential>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddAxles(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Axles>(true))
            return;

        GameObject subject = new GameObject("RCCP_Axles");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(4);
        subject.gameObject.AddComponent<RCCP_Axles>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddInputs(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Input>(true))
            return;

        GameObject subject = new GameObject("RCCP_Inputs");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(5);
        subject.gameObject.AddComponent<RCCP_Input>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddAero(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_AeroDynamics>(true))
            return;

        GameObject subject = new GameObject("RCCP_Aero");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(6);
        subject.gameObject.AddComponent<RCCP_AeroDynamics>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddAudio(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Audio>(true))
            return;

        GameObject subject = new GameObject("RCCP_Audio");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(7);
        subject.gameObject.AddComponent<RCCP_Audio>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddCustomizer(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Customizer>(true))
            return;

        GameObject subject = new GameObject("RCCP_Customizer");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(8);
        subject.gameObject.AddComponent<RCCP_Customizer>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddStability(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Stability>(true))
            return;

        GameObject subject = new GameObject("RCCP_Stability");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(9);
        subject.gameObject.AddComponent<RCCP_Stability>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddLights(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Lights>(true))
            return;

        GameObject subject = new GameObject("RCCP_Lights");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(10);
        subject.gameObject.AddComponent<RCCP_Lights>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddDamage(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Damage>(true))
            return;

        GameObject subject = new GameObject("RCCP_Damage");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(11);
        subject.gameObject.AddComponent<RCCP_Damage>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddParticles(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Particles>(true))
            return;

        GameObject subject = new GameObject("RCCP_Particles");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(12);
        subject.gameObject.AddComponent<RCCP_Particles>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddLOD(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_Lod>(true))
            return;

        GameObject subject = new GameObject("RCCP_LOD");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(13);
        subject.gameObject.AddComponent<RCCP_Lod>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddOtherAddons(RCCP_CarController prop) {

        if (EditorUtility.IsPersistent(prop))
            return;

        if (prop.gameObject.GetComponentInChildren<RCCP_OtherAddons>(true))
            return;

        GameObject subject = new GameObject("RCCP_OtherAddons");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(13);
        subject.gameObject.AddComponent<RCCP_OtherAddons>();

        EditorUtility.SetDirty(prop);

    }

    public static void AddEngineToClutchListener(RCCP_CarController prop) {

        RCCP_Engine engine = prop.GetComponentInChildren<RCCP_Engine>(true);
        RCCP_Clutch clutch = prop.GetComponentInChildren<RCCP_Clutch>(true);

        engine.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(clutch,
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), clutch, targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(engine.outputEvent, methodDelegate);

    }

    public static void AddClutchToGearboxListener(RCCP_CarController prop) {

        RCCP_Gearbox gearbox = prop.GetComponentInChildren<RCCP_Gearbox>(true);
        RCCP_Clutch clutch = prop.GetComponentInChildren<RCCP_Clutch>(true);

        clutch.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(gearbox,
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), gearbox, targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(clutch.outputEvent, methodDelegate);

    }

    public static void AddGearboxToDifferentialListener(RCCP_CarController prop) {

        RCCP_Gearbox gearbox = prop.GetComponentInChildren<RCCP_Gearbox>(true);
        RCCP_Differential[] differentials = prop.GetComponentsInChildren<RCCP_Differential>(true);

        gearbox.outputEvent = new RCCP_Event_Output();

        foreach (RCCP_Differential diff in differentials) {

            var methodInfo = UnityEvent.GetValidMethodInfo(
                diff,
                "ReceiveOutput",
                new Type[] { typeof(RCCP_Output) }
            );

            var action = Delegate.CreateDelegate(
                typeof(UnityAction<RCCP_Output>),
                diff,
                methodInfo
            ) as UnityAction<RCCP_Output>;

            UnityEventTools.AddPersistentListener(gearbox.outputEvent, action);

        }

    }

    public static void AddDifferentialToAxle(RCCP_CarController prop) {

        RCCP_Axles axles = prop.GetComponentInChildren<RCCP_Axles>(true);
        RCCP_Differential differential = prop.GetComponentInChildren<RCCP_Differential>(true);

        if (!axles)
            return;

        float[] indexes = new float[axles.GetComponentsInChildren<RCCP_Axle>(true).Length];

        if (indexes.Length < 1)
            return;

        for (int i = 0; i < indexes.Length; i++)
            indexes[i] = axles.GetComponentsInChildren<RCCP_Axle>(true)[i].leftWheelCollider.transform.localPosition.z;

        int biggestIndex = 0;
        int lowestIndex = 0;

        for (int i = 0; i < indexes.Length; i++) {

            if (indexes[i] >= biggestIndex)
                biggestIndex = i;

            if (indexes[i] <= lowestIndex)
                lowestIndex = i;

        }

        RCCP_Axle rearAxle = axles.GetComponentsInChildren<RCCP_Axle>(true)[lowestIndex];

        if (rearAxle)
            differential.connectedAxle = rearAxle;

    }

}
#endif
