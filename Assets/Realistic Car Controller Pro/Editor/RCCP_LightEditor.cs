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
using UnityEngine.Rendering;

[CustomEditor(typeof(RCCP_Light))]
public class RCCP_LightEditor : Editor {

    RCCP_Light prop;
    GUISkin skin;
    private Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Light)target;
        serializedObject.Update();
        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (prop.useLensFlares) {

            EditorGUILayout.HelpBox("When using lensflares, be sure to have correct lensflare system. Builtin renderer pipeline is using 'LensFlare' component, other renderer pilelines are using 'LensFlare (SRP) component.'", MessageType.Info);
            EditorGUILayout.HelpBox("If you are using SRP lensflares, be sure camera on your scene has post process effects option enabled.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create LensFlare")) {

#if BCG_URP || BCG_HDRP

                LensFlareComponentSRP lensFlareComponentSRP = prop.GetComponent<LensFlareComponentSRP>();

                if (lensFlareComponentSRP)
                    return;

                RCCP_LightSetupData setupData = RCCP_Settings.Instance.lightsSetupData;

                LensFlareComponentSRP flareComp = prop.gameObject.AddComponent<LensFlareComponentSRP>();
                flareComp.lensFlareData = setupData.lensFlareSRP as LensFlareDataSRP;
                flareComp.attenuationByLightShape = false;
                flareComp.intensity = 0f;

#else

                LensFlare lensFlareComponent = prop.GetComponent<LensFlare>();

                if (lensFlareComponent)
                    return;

                RCCP_LightSetupData setupData = RCCP_Settings.Instance.lightsSetupData;

                LensFlare flareComp = prop.gameObject.AddComponent<LensFlare>();
                flareComp.flare = setupData.flare;
                flareComp.brightness = 0f;

#endif

            }

        }

        if (!EditorUtility.IsPersistent(prop) && prop.GetComponentInParent<RCCP_CarController>(true)) {

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Lights>(true).gameObject;

            if (prop.GetComponentInParent<RCCP_CarController>(true).checkComponents)
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

        }

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

}
#endif
