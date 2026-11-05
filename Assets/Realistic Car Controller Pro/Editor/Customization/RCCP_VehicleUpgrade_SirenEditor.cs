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

[CustomEditor(typeof(RCCP_VehicleUpgrade_SirenManager))]
public class RCCP_VehicleUpgrade_SirenEditor : Editor {

    RCCP_VehicleUpgrade_SirenManager prop;

    public override void OnInspectorGUI() {

        prop = (RCCP_VehicleUpgrade_SirenManager)target;
        serializedObject.Update();

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("All sirens can be used under this manager. Each siren has target index. Click 'Get All Sirens' after editing sirens.", MessageType.None);

        RCCP_LiteEditorHelper.BeginLockedSection("Siren Manager");

        DrawDefaultInspector();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

        if (GUILayout.Button("Get All Sirens"))
            prop.GetAllSirens();

        RCCP_LiteEditorHelper.EndLockedSection();

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

    }

}

#endif
