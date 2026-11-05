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
using UnityEngine.Rendering;
public class RCCP_InitLoad {

    [InitializeOnLoadMethod]
    public static void InitOnLoad() {

        EditorApplication.delayCall += EditorDelayedUpdate;

    }

    public static void EditorDelayedUpdate() {

        RCCP_Installation.CheckProjectLayers();

        CheckSymbols();

        RCCP_Installation.CheckMissingWheelSlipParticles();

        // In Lite, GetPaths() is inside the runtime DLL where UNITY_EDITOR was not defined at compile time,
        // so it always returns "" and overwrites the correctly-serialized paths. Skip it for Lite.
#if !BCG_RCCP_LITE
        RCCP_DemoScenes.Instance.GetPaths();
#endif

        RCCP_SceneUpdater.Check();

#if RCCP_PHOTON
        RCCP_DemoScenes_Photon.Instance.GetPaths();
#endif

#if BCG_ENTEREXIT
        BCG_DemoScenes.Instance.GetPaths();
#endif

#if RCCP_MIRROR
        RCCP_DemoScenes_Mirror.Instance.GetPaths();
#endif

        CheckRP();

    }

    public static void CheckSymbols() {

        bool hasKey = false;

#if BCG_RCCP && !RCCP_DEMO && !BCG_RCCP_LITE

        if (!RCCP_DemoContent.Instance.dontAskDemoContent) {

            RCCP_DemoContent.Instance.dontAskDemoContent = true;

            bool importDemoAssets = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Demo Assets", "Do you want to import demo assets such as vehicles, city, environment, scenes, etc...? You can import them later from the welcome window (Tools --> BCG --> RCCP --> Welcome Window).", "Import Demo Assets", "No");

            if (importDemoAssets)
                AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.demoPackage), true);

            EditorUtility.SetDirty(RCCP_DemoContent.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

#endif

#if BCG_RCCP
        hasKey = true;
#endif

        if (!hasKey) {

            RCCP_SetScriptingSymbol.SetEnabled("BCG_RCCP", true);
            RCCP_SetScriptingSymbol.SetEnabled("BCG_RCCP_LITE", true);

            EditorUtility.DisplayDialog("RCCP Lite | Welcome", "Welcome to Realistic Car Controller Pro — Lite Edition!\n\nThis is a free evaluation version with full runtime functionality. Editor inspectors have some fields locked to encourage upgrading to RCCP Pro.\n\nPlease read the documentation before use. Have fun!", "Let's get started!");
            EditorUtility.DisplayDialog("RCCP Lite | Input System", "RCCP Lite uses the new Input System by default. Make sure your project has Input System installed through the Package Manager. It should be installed if you have installed dependencies while importing the package. If not, you can install it from the Package Manager (Window --> Package Manager). More info can be found in the documentation.", "Ok");

            RCCP_WelcomeWindow.OpenWindow();

            EditorApplication.delayCall += () => {

                RCCP_Installation.CheckAllLayers();
                RCCP_SceneUpdater.CheckAllScenes();

            };

        }

    }

    public static void CheckRP() {

        RenderPipelineAsset activePipeline;

        activePipeline = GraphicsSettings.currentRenderPipeline;

        if (activePipeline == null) {

            RCCP_SetScriptingSymbol.SetEnabled("BCG_URP", false);
            RCCP_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);

        } else if (activePipeline.GetType().ToString().Contains("Universal")) {

#if !BCG_URP
            RCCP_SetScriptingSymbol.SetEnabled("BCG_URP", true);
            RCCP_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);
#endif

        } else if (activePipeline.GetType().ToString().Contains("HD")) {

#if !BCG_HDRP
            RCCP_SetScriptingSymbol.SetEnabled("BCG_HDRP", true);
            RCCP_SetScriptingSymbol.SetEnabled("BCG_URP", false);
#endif

        } else {

            RCCP_SetScriptingSymbol.SetEnabled("BCG_URP", false);
            RCCP_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);

        }

    }

}

#endif
