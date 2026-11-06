using System.Collections.Generic;
using System.Globalization;
using CityRise.Progression;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityRise.Game
{
    /// <summary>
    /// Dükkanın dış görünümü: oyuncunun seçtiği tabela rengi (yazı oyun içinde yeniden çizilir)
    /// ve kapının üstündeki çizgili tente. Tabela bina kurulurken "Tabela/Yazi" adıyla eklenir.
    /// </summary>
    public static class ShopExterior
    {
        static readonly Dictionary<string, Material> signCache = new Dictionary<string, Material>();
        static readonly Dictionary<int, Material> awningCache = new Dictionary<int, Material>();
        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        public static string SignText(string businessId) => Loc.T("business." + businessId).ToUpper(Turkish);

        /// <summary>Görünümü binaya uygular (tabela rengi ve tente).</summary>
        public static void Apply(Transform plotView, OwnedBusiness owned)
        {
            if (plotView == null || owned == null) return;
            var look = owned.look;
            foreach (var sign in FindAll(plotView, "Tabela"))
            {
                var face = sign.Find("Yazi");
                if (face == null) continue;
                if (look.sign >= 0 && look.sign < ShopOptions.Signs.Length)
                {
                    var opt = ShopOptions.Signs[look.sign];
                    float aspect = face.localScale.x / Mathf.Max(0.01f, face.localScale.y);
                    face.GetComponent<Renderer>().sharedMaterial = SignMaterial(SignText(owned.businessId), opt.color, opt.color2, aspect);
                }
                Awning(sign, face, look.awning);
            }
        }

        static IEnumerable<Transform> FindAll(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) yield return t;
        }

        // ------------------------------------------------------------ Tabela

        /// <summary>Tabela yazısı: geçici bir kamerayla dokuya çizilir (yazı tipi her yerde aynı görünür).</summary>
        public static Material SignMaterial(string text, Color bg, Color fg, float aspect)
        {
            string key = $"{text}|{bg}|{fg}";
            if (signCache.TryGetValue(key, out var cached) && cached != null) return cached;
            int h = 128, w = Mathf.Clamp(Mathf.RoundToInt(h * aspect / 4f) * 4, 256, 1024);

            var far = new Vector3(-9000f, 500f, -9000f);
            var go = new GameObject("TabelaYazisi");
            go.transform.position = far;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 120;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = fg;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            var b = mr.bounds;

            var camGo = new GameObject("TabelaKamera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.aspect = w / (float)h;
            cam.orthographicSize = Mathf.Max(b.extents.y * 1.35f, b.extents.x * 1.12f / cam.aspect);
            cam.transform.position = b.center - Vector3.forward * 10f;
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 50f;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Tabela_" + text };
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            Object.Destroy(camGo);
            Object.Destroy(go);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tabela_" + text };
            mat.SetTexture("_BaseMap", rt);
            mat.SetFloat("_Smoothness", 0.3f);
            signCache[key] = mat;
            return mat;
        }

        // ------------------------------------------------------------ Tente

        /// <summary>Tabelanın altında, kapının üstünde eğimli çizgili tente ve sarkan saçak. index -1: kaldır.</summary>
        static void Awning(Transform sign, Transform face, int index)
        {
            var parent = sign.parent;
            var old = parent != null ? parent.Find("Tente") : null;
            if (old != null) Object.Destroy(old.gameObject);
            if (index < 0 || index >= ShopOptions.Awnings.Length || parent == null) return;

            var mat = AwningMaterial(index);
            // Tabelanın ileri yönü binanın içine bakar; ön = -forward.
            var front = -sign.forward;
            float width = face.lossyScale.x + 0.6f;
            const float depth = 1.35f, tilt = 24f;
            var root = new GameObject("Tente").transform;
            root.SetParent(parent, true);
            root.position = sign.position - Vector3.up * 0.95f + front * 0.02f;
            root.rotation = Quaternion.LookRotation(front, Vector3.up);

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(canopy.GetComponent<Collider>());
            canopy.name = "Bez";
            canopy.transform.SetParent(root, false);
            canopy.transform.localRotation = Quaternion.Euler(tilt, 0, 0);
            canopy.transform.localPosition = canopy.transform.localRotation * new Vector3(0, 0, depth * 0.5f);
            canopy.transform.localScale = new Vector3(width, 0.035f, depth);
            canopy.GetComponent<Renderer>().sharedMaterial = mat;

            var tip = root.TransformPoint(Quaternion.Euler(tilt, 0, 0) * new Vector3(0, 0, depth));
            var valance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(valance.GetComponent<Collider>());
            valance.name = "Sacak";
            valance.transform.SetParent(root, false);
            valance.transform.position = tip - Vector3.up * 0.13f;
            valance.transform.localRotation = Quaternion.identity;
            valance.transform.localScale = new Vector3(width, 0.26f, 0.03f);
            valance.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Material AwningMaterial(int index)
        {
            if (awningCache.TryGetValue(index, out var m) && m != null) return m;
            var opt = ShopOptions.Awnings[index];
            const int W = 64, H = 8;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Tente_" + opt.name };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = (x / 8) % 2 == 0 ? opt.color : new Color(0.97f, 0.95f, 0.9f);
            tex.SetPixels(px);
            tex.Apply(true);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tente_" + opt.name };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(3f, 1f));
            m.SetFloat("_Smoothness", 0.15f);
            awningCache[index] = m;
            return m;
        }
    }
}
