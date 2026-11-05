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

[CustomEditor(typeof(RCCP_LiteSetupHelper))]
public class RCCP_LiteSetupHelperEditor : Editor {

    public override void OnInspectorGUI() {

        RCCP_LiteSetupHelper helper = (RCCP_LiteSetupHelper)target;
        RCCP_CarController car = helper.GetComponent<RCCP_CarController>();

        if (car == null) {

            EditorGUILayout.HelpBox("This component should be on a GameObject with RCCP_CarController.", MessageType.Error);
            return;

        }

        EditorGUILayout.HelpBox("RCCP Lite Setup Helper — Complete these steps to get your vehicle driving.", MessageType.Info);
        EditorGUILayout.Space();

        bool allPassed = true;

        // Check wheel models on each axle.
        RCCP_Axle[] axles = car.GetComponentsInChildren<RCCP_Axle>(true);

        if (axles.Length == 0) {

            EditorGUILayout.HelpBox("No axles found on this vehicle.", MessageType.Warning);
            allPassed = false;

        } else {

            for (int i = 0; i < axles.Length; i++) {

                RCCP_Axle axle = axles[i];
                string axleName = axle.gameObject.name;

                if (axle.leftWheelModel == null) {

                    EditorGUILayout.HelpBox(axleName + " — Left Wheel Model is not assigned. Select the axle and drag your wheel mesh into the slot.", MessageType.Warning);
                    allPassed = false;

                }

                if (axle.rightWheelModel == null) {

                    EditorGUILayout.HelpBox(axleName + " — Right Wheel Model is not assigned. Select the axle and drag your wheel mesh into the slot.", MessageType.Warning);
                    allPassed = false;

                }

            }

        }

        // Check for at least one non-WheelCollider body collider.
        Collider[] colliders = car.GetComponentsInChildren<Collider>(true);
        bool hasBodyCollider = false;

        for (int i = 0; i < colliders.Length; i++) {

            if (!(colliders[i] is WheelCollider)) {

                hasBodyCollider = true;
                break;

            }

        }

        if (!hasBodyCollider) {

            EditorGUILayout.HelpBox("No body collider found. Add a MeshCollider or BoxCollider to your vehicle's body mesh so it can collide with the environment.", MessageType.Warning);
            allPassed = false;

        }

        // All checks passed.
        if (allPassed) {

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("All checks passed! Your vehicle is ready to drive. You can remove this helper now.", MessageType.Info);

        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Remove This Helper")) {

            Undo.DestroyObjectImmediate(helper);
            return;

        }

    }

}
#endif
