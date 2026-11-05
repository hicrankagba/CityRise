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

[CustomEditor(typeof(RCCP_VehicleUpgrade_WheelManager))]
public class RCCP_VehicleUpgrade_WheelEditor : Editor {

    RCCP_VehicleUpgrade_WheelManager prop;

    public override void OnInspectorGUI() {

        prop = (RCCP_VehicleUpgrade_WheelManager)target;
        serializedObject.Update();

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("All wheels are stored in the configure wheels section", MessageType.None);

        RCCP_LiteEditorHelper.BeginLockedSection("Wheel Manager");

        DrawDefaultInspector();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

        RCCP_LiteEditorHelper.EndLockedSection();

        if (GUILayout.Button("Configure Wheels"))
            Selection.activeObject = RCCP_ChangableWheels.Instance;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

    }

}

#endif
