using CityRise.Game;
using UnityEditor;
using UnityEngine;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Play'e basıldığında açık kasaba sahnesi eski bir kurulumla yapılmışsa (yeni binalar, özellikler
    /// sahnede yoksa) önce kasabayı yeniden kurar, sonra oyunu başlatır. Böylece güncellemeler için
    /// menüden "Başlangıç Kasabasını Oluştur" çalıştırmayı unutmak sorun olmaz.
    /// </summary>
    [InitializeOnLoad]
    static class KasabaGuncelligi
    {
        static KasabaGuncelligi()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || Application.isBatchMode) return;
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null || gm.builtVersion >= TownBuilder.BuildVersion) return;
            // Oyunu durdur, kasabayı yeni sürümle kur, sonra oyunu başlat.
            EditorApplication.isPlaying = false;
            bool night = RenderSettings.sun != null && RenderSettings.sun.name == "Ay";
            EditorApplication.delayCall += () =>
            {
                Debug.Log("CityRise: Kasaba eski sürümle kurulmuş; yeni sürümle yeniden kuruluyor...");
                TownBuilder.Build(night);
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            };
        }
    }
}
