using System.Collections.Generic;
using CityRise.Interior;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityRise.Game
{
    /// <summary>
    /// Ürünlerin gerçek 3B modelinden küçük resim üretir (arayüzdeki ürün kartları için).
    /// Model uzak bir köşede, yalnızca bu kameranın gördüğü katmanda bir kez çizilir; resim önbellekte tutulur.
    /// </summary>
    public static class ProductIcons
    {
        const int Layer = 30;
        const int Size = 160;
        static readonly Dictionary<string, Texture> cache = new Dictionary<string, Texture>();
        static Camera cam;
        static Transform stage;

        public static Texture Get(InteriorLibrary lib, string modelKey)
        {
            if (string.IsNullOrEmpty(modelKey) || lib == null) return null;
            if (cache.TryGetValue(modelKey, out var t) && t != null) return t;
            var e = lib.Get(modelKey);
            if (e == null || e.prefab == null) return null;
            Setup();

            var go = Object.Instantiate(e.prefab, stage);
            go.transform.localScale = Vector3.one * e.scale;
            go.transform.localRotation = Quaternion.Euler(0, 35f, 0);
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.Destroy(go); return null; }
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // Model ne boyda olursa olsun kareye sığsın.
            float radius = Mathf.Max(0.01f, b.extents.magnitude);
            cam.transform.position = b.center + new Vector3(0, 0.45f, -1f).normalized * radius * 3.1f;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = radius * 0.1f;
            cam.farClipPlane = radius * 10f;

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Urun_" + modelKey };
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            Object.DestroyImmediate(go);
            cache[modelKey] = rt;
            return rt;
        }

        static void Setup()
        {
            if (cam != null) return;
            stage = new GameObject("UrunResimleri").transform;
            stage.position = new Vector3(8000f, -500f, 8000f);
            var camGo = new GameObject("UrunKamera");
            camGo.transform.SetParent(stage, false);
            cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.95f, 0.93f, 0.88f, 1f);
            cam.cullingMask = 1 << Layer;
            var key = new GameObject("UrunIsigi").AddComponent<Light>();
            key.transform.SetParent(stage, false);
            key.transform.rotation = Quaternion.Euler(40f, -30f, 0);
            key.type = LightType.Directional;
            key.intensity = 1.6f;
            key.color = new Color(1f, 0.97f, 0.92f);
            key.cullingMask = 1 << Layer;
            key.shadows = LightShadows.None;
            var fill = new GameObject("UrunDolgu").AddComponent<Light>();
            fill.transform.SetParent(stage, false);
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0);
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.cullingMask = 1 << Layer;
            fill.shadows = LightShadows.None;
            Object.DontDestroyOnLoad(stage.gameObject);
        }
    }
}
