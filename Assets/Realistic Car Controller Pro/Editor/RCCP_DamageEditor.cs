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

[CustomEditor(typeof(RCCP_Damage))]
public class RCCP_DamageEditor : Editor {

    RCCP_Damage prop;
    GUISkin skin;
    GUISkin orgSkin;
    Color guiColor;

    private void OnEnable() {

        guiColor = GUI.color;
        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Damage)target;
        serializedObject.Update();

        if (orgSkin == null)
            orgSkin = GUI.skin;

        GUI.skin = skin;

        RCCP_LiteEditorHelper.DrawLiteBanner();

        EditorGUILayout.HelpBox("Damage system.", MessageType.Info, true);

        DamageTab();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        if (!EditorUtility.IsPersistent(prop)) {

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Back"))
                Selection.activeGameObject = prop.GetComponentInParent<RCCP_CarController>(true).gameObject;

            EditorGUILayout.EndVertical();

        }

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        RCCP_LiteEditorHelper.DrawLiteFooter();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private void DamageTab() {

        RCCP_LiteEditorHelper.BeginLockedSection("Damage Settings");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("saveName"), new GUIContent("Save Name", "This string will be used to save and load the damage data with json."));

        EditorGUILayout.HelpBox("Auto Install: All meshes, lights, parts, and wheels will be collected automatically at runtime. If you want to select specific objects, disable ''Auto Install'' and select specific objects. If you want to remove only few objects, you can use buttom buttons to get all.", MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("automaticInstallation"), new GUIContent("Auto Install", "Auto Install: All meshes, lights, parts, and wheels will be collected automatically at runtime. If you want to select specific objects, disable ''Auto Install'' and select specific objects. If you want to remove only few objects, you can use buttom buttons to get all."));

        GUI.skin = orgSkin;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("damageFilter"), new GUIContent("Damage Filter", "LayerMask filter. Damage will be taken from the objects with these layers."));
        GUI.skin = skin;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumDamage"), new GUIContent("Maximum Damage", "Maximum Vert Distance For Limiting Damage. 0 Value Will Disable The Limit."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("processInactiveGameobjects"), new GUIContent("Process Inactive Gameobjects", "Process inactive gameobjects too?"));
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("meshDeformation"), new GUIContent("Mesh Deformation", "Deforms selected meshes on collision."));

        if (prop.meshDeformation) {

            EditorGUI.indentLevel++;
            RCCP_LiteEditorHelper.BeginLockedSection("Mesh Deformation");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("deformationRadius"), new GUIContent("Deformation Radius", "Verticies in this radius will be effected on collisions."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("deformationMultiplier"), new GUIContent("Deformation Multiplier", "Damage multiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recalculateNormals"), new GUIContent("Recalculate Normals", "Recalculate normals while deforming / restoring the mesh."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recalculateBounds"), new GUIContent("Recalculate Bounds", "Recalculate bounds while deforming / restoring the mesh."));

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);
            EditorGUI.indentLevel--;

        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelDamage"), new GUIContent("Wheel Damage", "Use wheel damage."));

        if (prop.wheelDamage) {

            EditorGUI.indentLevel++;
            RCCP_LiteEditorHelper.BeginLockedSection("Wheel Damage");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelDamageRadius"), new GUIContent("Wheel Damage Radius", "Wheel damage radius."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelDamageMultiplier"), new GUIContent("Wheel Damage Multiplier", "Wheel damage multiplier."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelDetachment"), new GUIContent("Wheel Detachment", "Use wheel detachment."));

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);
            EditorGUI.indentLevel--;

        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDamage"), new GUIContent("Light Damage", "Use light damage."));

        if (prop.lightDamage) {

            EditorGUI.indentLevel++;
            RCCP_LiteEditorHelper.BeginLockedSection("Light Damage");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDamageRadius"), new GUIContent("Light Damage Radius", "Light damage radius."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDamageMultiplier"), new GUIContent("Light Damage Multiplier", "Light damage multiplier."));

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);
            EditorGUI.indentLevel--;

        }

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("partDamage"), new GUIContent("Part Damage", "Use part damage."));

        if (prop.partDamage) {

            EditorGUI.indentLevel++;
            RCCP_LiteEditorHelper.BeginLockedSection("Part Damage");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("partDamageRadius"), new GUIContent("Part Damage Radius", "Part damage radius."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("partDamageMultiplier"), new GUIContent("Part Damage Multiplier", "Part damage multiplier."));

            RCCP_LiteEditorHelper.EndLockedSection(guiColor);
            EditorGUI.indentLevel--;

        }

        EditorGUILayout.Space();

        RCCP_LiteEditorHelper.BeginLockedSection("Manual Installation");

        if (!prop.automaticInstallation) {

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Mesh Filters", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;

            if (prop.meshFilters != null) {

                for (int i = 0; i < prop.meshFilters.Length; i++) {

                    if (prop.meshFilters[i]) {

                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.ObjectField(prop.meshFilters[i], typeof(MeshFilter), false);

                        if (prop.meshFilters[i].sharedMesh == null) {

                            GUI.color = Color.red;
                            EditorGUILayout.HelpBox("Mesh is null!", MessageType.None);

                        }

                        if (prop.meshFilters[i].GetComponent<MeshRenderer>() == null) {

                            GUI.color = Color.red;
                            EditorGUILayout.HelpBox("No renderer found!", MessageType.None);

                        }

                        bool fixedRotation = 1 - Mathf.Abs(Quaternion.Dot(prop.meshFilters[i].transform.rotation, prop.transform.rotation)) < .01f;

                        if (!fixedRotation) {

                            GUI.color = Color.red;
                            EditorGUILayout.HelpBox("Axis is wrong!", MessageType.None);

                        }

                        GUI.color = guiColor;
                        GUI.color = Color.red;

                        GUILayout.Button("X", GUILayout.Width(25f));

                        GUI.color = guiColor;
                        EditorGUILayout.EndHorizontal();

                    }

                }

            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            //
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Wheels", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;

            if (prop.wheels != null) {

                for (int i = 0; i < prop.wheels.Length; i++) {

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(prop.wheels[i], typeof(RCCP_WheelCollider), false);
                    GUI.color = Color.red;

                    GUILayout.Button("X", GUILayout.Width(25f));

                    GUI.color = guiColor;
                    EditorGUILayout.EndHorizontal();

                }

            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            //
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Lights", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;

            if (prop.lights != null) {

                for (int i = 0; i < prop.lights.Length; i++) {

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(prop.lights[i], typeof(RCCP_Light), false);
                    GUI.color = Color.red;

                    GUILayout.Button("X", GUILayout.Width(25f));

                    GUI.color = guiColor;
                    EditorGUILayout.EndHorizontal();

                }

            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            //
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUI.indentLevel++;

            if (prop.parts != null) {

                for (int i = 0; i < prop.parts.Length; i++) {

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(prop.parts[i], typeof(RCCP_DetachablePart), false);
                    GUI.color = Color.red;

                    GUILayout.Button("X", GUILayout.Width(25f));

                    GUI.color = guiColor;
                    EditorGUILayout.EndHorizontal();

                }

            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            ///////////////////////

            EditorGUILayout.Space();

            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();

            GUILayout.Button("Get Meshes");
            GUILayout.Button("Get Lights");
            GUILayout.Button("Get Parts");
            GUILayout.Button("Get Wheels");

            EditorGUILayout.EndHorizontal();

            GUILayout.Button("Clean Empty Elements");

            EditorGUILayout.EndVertical();

        }

        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        GUILayout.Space(10f);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (!EditorApplication.isPlaying)
            GUI.enabled = false;

        if (prop.repaired) {

            GUILayout.Button("Repaired");

        } else {

            GUI.color = Color.green;

            if (GUILayout.Button("Repair Now"))
                prop.repairNow = true;

            GUI.color = guiColor;

        }

        GUI.enabled = true;

        RCCP_LiteEditorHelper.BeginLockedSection("Save / Load");
        EditorGUILayout.BeginHorizontal();

        GUILayout.Button("Save");
        GUILayout.Button("Load");
        GUILayout.Button("Delete");

        EditorGUILayout.EndHorizontal();
        RCCP_LiteEditorHelper.EndLockedSection(guiColor);

        EditorGUILayout.EndVertical();

    }

    private void CleanEmptyElements() {

        List<MeshFilter> meshFilterList = new List<MeshFilter>();

        for (int i = 0; i < prop.meshFilters.Length; i++) {

            if (prop.meshFilters[i] != null)
                meshFilterList.Add(prop.meshFilters[i]);

        }

        prop.meshFilters = meshFilterList.ToArray();

        List<RCCP_Light> lightList = new List<RCCP_Light>();

        for (int i = 0; i < prop.lights.Length; i++) {

            if (prop.lights[i] != null)
                lightList.Add(prop.lights[i]);

        }

        prop.lights = lightList.ToArray();

        List<RCCP_DetachablePart> partList = new List<RCCP_DetachablePart>();

        for (int i = 0; i < prop.parts.Length; i++) {

            if (prop.parts[i] != null)
                partList.Add(prop.parts[i]);

        }

        prop.parts = partList.ToArray();

        List<RCCP_WheelCollider> wheelsList = new List<RCCP_WheelCollider>();

        for (int i = 0; i < prop.wheels.Length; i++) {

            if (prop.wheels[i] != null)
                wheelsList.Add(prop.wheels[i]);

        }

        prop.wheels = wheelsList.ToArray();

    }

    public void GetMeshes() {

        RCCP_CarController carController = prop.GetComponentInParent<RCCP_CarController>(true);

        List<MeshFilter> properMeshFilters = new List<MeshFilter>(
            carController.GetComponentsInChildren<MeshFilter>(prop.processInactiveGameobjects)
        );

        List<MeshFilter> filteredMeshFilters = new List<MeshFilter>();

        List<RCCP_WheelCollider> wheelColliders = new List<RCCP_WheelCollider>(
            carController.GetComponentsInChildren<RCCP_WheelCollider>(true)
        );

        foreach (MeshFilter meshFilter in properMeshFilters) {

            if (meshFilter == null)
                continue;

            MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();

            if (renderer == null)
                continue;

            // Check if the mesh is readable - skip if not (can't deform non-readable meshes)
            if (!meshFilter.sharedMesh.isReadable) {
                Debug.LogWarning(
                    "Skipping non-readable mesh '" + meshFilter.transform.name
                    + "' for damage deformation. Enable 'Read/Write' in the mesh Import Settings to include this mesh."
                );
                continue;  // Skip this mesh - don't add to damage list
            }

            // We'll use a 'skip' flag to decide if we should exclude this MeshFilter
            bool skip = false;

            // If we do have wheelColliders, let's see if this mesh belongs to any wheel
            if (wheelColliders != null && wheelColliders.Count > 0) {

                foreach (RCCP_WheelCollider wc in wheelColliders) {

                    if (wc == null)
                        continue;

                    // If the wheelModel is null, decide what you want to do:
                    // The original code added the mesh automatically if wheelModel was null.
                    // If you want to skip, set skip = true; or if you want to add, do nothing here.
                    // For now, let's do nothing, so we only skip if it's actually the child
                    // of a real wheelModel that exists.
                    if (wc.wheelModel == null)
                        continue;

                    // If it's the same transform OR a child of the wheelModel, then skip
                    if (meshFilter.transform == wc.wheelModel ||
                        meshFilter.transform.IsChildOf(wc.wheelModel)) {

                        skip = true;
                        break;  // No need to check other wheels

                    }

                }

            }

            // If we haven't marked it 'skip', then add to filtered list
            if (!skip && !filteredMeshFilters.Contains(meshFilter))
                filteredMeshFilters.Add(meshFilter);

        }

        prop.meshFilters = filteredMeshFilters.ToArray();

    }

}
#endif
