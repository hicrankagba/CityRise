using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Oyun yalnızca yatay oynanır. Proje ayarlarında dikey yönleri kapatır ve editördeki
    /// Game görünümünü yatay bir çözünürlüğe (2400×1080, 1920×1080) ayarlar.
    /// Editör her açıldığında bir kez kendiliğinden çalışır; menüden de çağrılabilir.
    /// </summary>
    [InitializeOnLoad]
    public static class LandscapeSetup
    {
        static LandscapeSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool("CityRise.Landscape", false)) return;
            SessionState.SetBool("CityRise.Landscape", true);
            EditorApplication.delayCall += () => Apply(selectGameView: true);
        }

        [MenuItem("CityRise/Yatay Ekranı Ayarla", priority = 40)]
        public static void ApplyFromMenu() => Apply(selectGameView: true);

        public static void Apply(bool selectGameView)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            if (selectGameView) SelectLandscapeGameView();
        }

        /// <summary>
        /// Game görünümüne "CityRise 2400×1080" ve "CityRise 1920×1080" boyutlarını ekleyip ilkini seçer.
        /// Unity bunun için açık bir arayüz sunmadığından iç sınıflara yansıma ile erişilir;
        /// başarısız olursa yalnızca uyarı yazar (Game görünümünün üstündeki menüden elle seçilebilir).
        /// </summary>
        static void SelectLandscapeGameView()
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance").GetValue(null);
                var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(instance);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)groupType });
                var groupClass = group.GetType();
                var getTexts = groupClass.GetMethod("GetDisplayTexts");
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                var fixedRes = Enum.Parse(sizeKind, "FixedResolution");

                int Find(string label)
                {
                    var texts = (string[])getTexts.Invoke(group, null);
                    for (int i = 0; i < texts.Length; i++) if (texts[i].StartsWith(label)) return i;
                    return -1;
                }
                foreach (var (w, h) in new[] { (2400, 1080), (1920, 1080) })
                {
                    string label = $"CityRise {w}x{h}";
                    if (Find(label) >= 0) continue;
                    var size = ctor.Invoke(new object[] { fixedRes, w, h, label });
                    groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                }
                sizesType.GetMethod("SaveToHDD")?.Invoke(instance, null);

                int index = Find("CityRise 2400x1080");
                var gameViewType = asm.GetType("UnityEditor.GameView");
                var windows = Resources.FindObjectsOfTypeAll(gameViewType);
                foreach (var w in windows)
                {
                    var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (select != null) select.Invoke(w, new object[] { index, null });
                    else gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(w, index);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("CityRise: Game görünümü otomatik yatay yapılamadı; Game penceresinin üstündeki çözünürlük menüsünden 1920x1080 seçin. (" + e.Message + ")");
            }
        }
    }
}
