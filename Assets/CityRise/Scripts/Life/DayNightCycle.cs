using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityRise.Life
{
    /// <summary>
    /// Canlı gün-gece döngüsü. Güneş doğudan doğar batıdan batar; akşam gökyüzü kararır,
    /// pencereler ve sokak lambaları yanar, gece mehtap ve gece gökyüzü gelir, sabah her şey söner.
    /// Oyun saati gerçek saate bağlıdır (oyun kapalıyken de akar); bir oyun günü dayMinutes gerçek dakikadır.
    /// Gündüz ve gece için gereken her şey (malzemeler, ışıklar, gökyüzü, renk ayarı) kurulumda hazırlanır.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Tooltip("Bir oyun gününün gerçek dakika cinsinden süresi.")]
        public float dayMinutes = 24f;
        [Tooltip("Güneşin doğduğu ve battığı saat.")]
        public float sunriseHour = 6f, sunsetHour = 20f;

        [Header("Işıklar")]
        public Light sun;
        public Light moon;
        public List<Light> nightLights = new List<Light>();

        [Header("Gökyüzü ve renk")]
        public Material daySky, nightSky;
        public Volume dayVolume, nightVolume;

        [Header("Gece parlayan malzemeler (gündüz → gece)")]
        public List<Material> dayMaterials = new List<Material>();
        public List<Material> nightMaterials = new List<Material>();

        /// <summary>Test için saati sabitler (0-24); negatifse gerçek akış.</summary>
        public static float ForceHour = -1f;

        /// <summary>0 = gündüz, 1 = tam gece.</summary>
        public float Night { get; private set; }
        public bool IsNight => Night > 0.5f;
        public float Hour { get; private set; }
        public bool InteriorOpen;

        readonly List<Color> nightEmission = new List<Color>();
        readonly Dictionary<Material, Material> toNight = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Material> toDay = new Dictionary<Material, Material>();
        bool nightMatsOn;
        bool nightSkyOn;
        float lastEnvUpdate = -10f;
        float baseSunIntensity, baseMoonIntensity;
        Color daySunColor;
        readonly List<float> lightIntensity = new List<float>();

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < nightMaterials.Count; i++)
            {
                var n = nightMaterials[i];
                if (n != null)
                {
                    // Sahneden yüklenince parlama anahtar kelimesi kapalı gelebiliyor; açık olsun.
                    n.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    n.EnableKeyword("_EMISSION");
                }
                nightEmission.Add(n != null && n.HasProperty("_EmissionColor") ? n.GetColor("_EmissionColor") : Color.black);
                if (i < dayMaterials.Count && dayMaterials[i] != null && n != null)
                {
                    toNight[dayMaterials[i]] = n;
                    toDay[n] = dayMaterials[i];
                }
            }
            // Gökyüzü malzemelerinin kopyası değiştirilir (dosyadaki malzeme bozulmasın).
            if (daySky != null) daySky = new Material(daySky);
            if (nightSky != null) nightSky = new Material(nightSky);
            if (sun != null) { baseSunIntensity = sun.intensity; daySunColor = sun.color; }
            if (moon != null) baseMoonIntensity = moon.intensity;
            foreach (var l in nightLights) lightIntensity.Add(l != null ? l.intensity : 0f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start() => Apply(true);

        void Update() => Apply(false);

        /// <summary>Gerçek saatten oyun saati (0-24).</summary>
        float CurrentHour()
        {
            if (ForceHour >= 0) return ForceHour;
            double minutes = (DateTime.Now - DateTime.Today).TotalMinutes + DateTime.Today.DayOfYear * 1440.0;
            double day = minutes / Math.Max(1.0, dayMinutes);
            // Gün 09:00'da başlasın diye kaydırılır (oyun açılınca çoğunlukla aydınlık).
            return (float)((day - Math.Floor(day)) * 24.0 + 9.0) % 24f;
        }

        /// <summary>Güneşin ufka göre yüksekliği (derece): gündüz 0..55, gece 0..-30.</summary>
        float SunElevation(float h)
        {
            float dayLen = sunsetHour - sunriseHour;
            if (h >= sunriseHour && h <= sunsetHour) return Mathf.Sin(Mathf.PI * (h - sunriseHour) / dayLen) * 55f;
            float nightLen = 24f - dayLen;
            float k = h > sunsetHour ? h - sunsetHour : h + 24f - sunsetHour;
            return -Mathf.Sin(Mathf.PI * k / nightLen) * 30f;
        }

        void Apply(bool force)
        {
            Hour = CurrentHour();
            float elev = SunElevation(Hour);
            // Güneş ufkun 8° üstündeyken gündüz, 6° altındayken tam gece.
            Night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 8f, elev));

            if (sun != null)
            {
                float dayFrac = Mathf.InverseLerp(sunriseHour, sunsetHour, Hour);
                float azimuth = Mathf.Lerp(-110f, 110f, dayFrac);
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(elev, -5f), azimuth - 25f, 0);
                float low = Mathf.InverseLerp(25f, 2f, elev);
                sun.color = Color.Lerp(daySunColor, new Color(1f, 0.55f, 0.3f), low);
                sun.intensity = baseSunIntensity * Mathf.Clamp01(Mathf.InverseLerp(-4f, 12f, elev));
                sun.enabled = sun.intensity > 0.01f;
            }
            if (moon != null)
            {
                moon.intensity = baseMoonIntensity * Night;
                moon.enabled = Night > 0.02f;
            }
            if (dayVolume != null) dayVolume.weight = 1f - Night;
            if (nightVolume != null) nightVolume.weight = Night;

            for (int i = 0; i < nightLights.Count; i++)
            {
                var l = nightLights[i];
                if (l == null) continue;
                float k = Mathf.InverseLerp(0.25f, 0.8f, Night);
                l.intensity = lightIntensity[i] * k;
                l.enabled = k > 0.01f;
            }

            // Pencereler ve lambalar: gece malzemesine geç, parlaklık alacakaranlıkta artar.
            bool wantNightMats = Night > 0.05f;
            if (wantNightMats != nightMatsOn || force) SwapMaterials(wantNightMats);
            if (nightMatsOn)
            {
                float glow = Mathf.InverseLerp(0.05f, 0.85f, Night);
                for (int i = 0; i < nightMaterials.Count; i++)
                    if (nightMaterials[i] != null) nightMaterials[i].SetColor("_EmissionColor", nightEmission[i] * glow);
            }

            if (InteriorOpen) return;
            // Gökyüzü: gündüz gökyüzü kararır, en karanlık anda gece gökyüzüne geçilir.
            bool wantNightSky = Night > 0.55f;
            if (wantNightSky != nightSkyOn || force)
            {
                nightSkyOn = wantNightSky;
                var sky = nightSkyOn ? nightSky : daySky;
                if (sky != null) RenderSettings.skybox = sky;
                lastEnvUpdate = -10f;
            }
            if (daySky != null) daySky.SetFloat("_Exposure", Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(0f, 0.55f, Night)));
            if (nightSky != null) nightSky.SetFloat("_Exposure", Mathf.Lerp(0.2f, 0.45f, Mathf.InverseLerp(0.55f, 1f, Night)));
            RenderSettings.ambientIntensity = Mathf.Lerp(1.05f, 0.7f, Night);
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.35f, Night);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(new Color(0.70f, 0.81f, 0.95f), new Color(0.95f, 0.66f, 0.5f), Mathf.Clamp01(1f - Mathf.Abs(Night - 0.4f) * 2.5f)), new Color(0.07f, 0.09f, 0.16f), Mathf.InverseLerp(0.4f, 1f, Night));
            RenderSettings.fogStartDistance = Mathf.Lerp(300f, 160f, Night);
            RenderSettings.fogEndDistance = Mathf.Lerp(1600f, 700f, Night);
            // Ortam ışığı gökyüzünden gelir; ara sıra yenilenir (pahalı).
            if (Time.unscaledTime - lastEnvUpdate > 4f)
            {
                lastEnvUpdate = Time.unscaledTime;
                DynamicGI.UpdateEnvironment();
            }
        }

        void SwapMaterials(bool night)
        {
            nightMatsOn = night;
            var map = night ? toNight : toDay;
            if (map.Count == 0) return;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && map.TryGetValue(mats[i], out var other)) { mats[i] = other; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>Şablondan yeni bina kurulunca gece malzemesine geçirilmesi için.</summary>
        public void Refresh() => SwapMaterials(nightMatsOn);
    }
}
