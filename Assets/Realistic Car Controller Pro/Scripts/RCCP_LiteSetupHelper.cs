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

/// <summary>
/// Editor-only helper component attached to newly created RCCP vehicles.
/// Shows validation warnings for missing wheel models and body colliders.
/// Stripped from builds automatically via #if UNITY_EDITOR.
/// </summary>
[DisallowMultipleComponent]
public class RCCP_LiteSetupHelper : MonoBehaviour { }
#endif
