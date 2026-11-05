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

/// <summary>
/// Centralized visual feedback helper for RCCP Lite editor scripts.
/// Provides goldenrod-tinted locked sections to indicate Pro-only parameters.
/// </summary>
public static class RCCP_LiteEditorHelper {

    private static readonly Color LiteGold = new Color(0.85f, 0.65f, 0.13f);

    private const string UpgradeURL = RCCP_AssetPaths.assetStorePath;

    private static bool _anyLockedSectionDrawn;

    /// <summary>
    /// Draws the Lite banner at the top of the inspector. Call after GUI.skin = skin.
    /// </summary>
    public static void DrawLiteBanner() {

        _anyLockedSectionDrawn = false;

        EditorGUILayout.HelpBox(
            "RCCP Lite \u2014 Some parameters are read-only. Full editing available in RCCP Pro.",
            MessageType.None);

        if (GUILayout.Button("Upgrade to RCCP Pro"))
            Application.OpenURL(UpgradeURL);

    }

    /// <summary>
    /// Begins a locked (Pro-only) section. Draws a gold inline label, tints fields gold, disables GUI.
    /// </summary>
    /// <param name="label">Optional section label (e.g. "Turbo Settings"). Pass null or empty for generic "Pro" tag.</param>
    public static void BeginLockedSection(string label = null) {

        _anyLockedSectionDrawn = true;

        string displayLabel = string.IsNullOrEmpty(label) ? "\u25C6 Pro" : "\u25C6 Pro \u2013 " + label;

        Color prevColor = GUI.color;
        GUI.color = LiteGold;
        EditorGUILayout.LabelField(displayLabel, EditorStyles.miniLabel);
        GUI.color = prevColor;

        GUI.color = LiteGold;
        GUI.enabled = false;

    }

    /// <summary>
    /// Ends a locked section, restoring GUI.enabled and GUI.color.
    /// </summary>
    /// <param name="originalColor">The original GUI color to restore. Pass the guiColor captured in OnEnable.</param>
    public static void EndLockedSection(Color originalColor) {

        GUI.enabled = true;
        GUI.color = originalColor;

    }

    /// <summary>
    /// Ends a locked section, restoring GUI.enabled and GUI.color to white.
    /// Use this overload when the editor does not store a guiColor field.
    /// </summary>
    public static void EndLockedSection() {

        GUI.enabled = true;
        GUI.color = Color.white;

    }

    /// <summary>
    /// Draws a footer message if any locked sections were drawn in this inspector pass.
    /// Call before serializedObject.ApplyModifiedProperties().
    /// </summary>
    public static void DrawLiteFooter() {

        if (!_anyLockedSectionDrawn)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Gold-labeled fields are available in RCCP Pro. All features work fully at runtime.",
            MessageType.None);

    }

}
#endif
