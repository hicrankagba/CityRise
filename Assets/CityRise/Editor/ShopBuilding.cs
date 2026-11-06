using System.Collections.Generic;
using CityRise.Interior;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityRise.EditorTools
{
    /// <summary>
    /// Dükkan binası: binanın kendisi dükkandır. Ön cephe baştan sona camlı vitrin (ortada kapı),
    /// üstünde cephe boyunca büyük tabela bandı, yanlarda da vitrin camı; camın arkasında aydınlık
    /// bir iç (raflar, ürünler, tezgah) görünür. Önü yerel +Z'ye bakar.
    /// Tabela "Tabela/Yazi" adıyla kurulur; oyundaki tabela rengi ve tente bunu kullanır.
    /// </summary>
    public static class ShopBuilding
    {
        public struct Materials
        {
            public Material wall, trim, stone, glass, glow, signFrame, sign, interiorFloor, interiorWall, wood, window;
        }

        /// <summary>İşletme türüne göre camın arkasında görünen ürün renkleri.</summary>
        static Color[] Products(string id)
        {
            switch (id)
            {
                case "manav": return new[] { new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.75f, 0.15f), new Color(0.35f, 0.65f, 0.2f), new Color(0.95f, 0.5f, 0.1f) };
                case "firin":
                case "simit_bufesi": return new[] { new Color(0.8f, 0.55f, 0.25f), new Color(0.9f, 0.7f, 0.4f), new Color(0.65f, 0.4f, 0.2f) };
                case "kasap": return new[] { new Color(0.75f, 0.18f, 0.18f), new Color(0.9f, 0.45f, 0.4f), new Color(0.95f, 0.9f, 0.85f) };
                case "kafe": return new[] { new Color(0.45f, 0.28f, 0.18f), new Color(0.9f, 0.85f, 0.75f), new Color(0.7f, 0.35f, 0.3f) };
                case "eczane": return new[] { new Color(0.95f, 0.95f, 0.95f), new Color(0.3f, 0.7f, 0.45f), new Color(0.35f, 0.55f, 0.85f) };
                case "berber": return new[] { new Color(0.15f, 0.15f, 0.17f), new Color(0.8f, 0.2f, 0.2f), new Color(0.9f, 0.9f, 0.92f) };
                default: return new[] { new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.8f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.65f, 0.35f), new Color(0.95f, 0.95f, 0.92f) };
            }
        }

        static readonly Dictionary<Color, Material> productMats = new Dictionary<Color, Material>();

        /// <summary>Binayı kurar. size: genişlik (x) ve derinlik (z), height: parapet dahil yükseklik.</summary>
        public static GameObject Build(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, float height, string id, Materials m)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = rotation;
            float W = size.x, D = size.y, H = height;
            float hw = W * 0.5f, hd = D * 0.5f;
            const float t = 0.25f;            // duvar kalınlığı
            const float plinth = 0.45f;       // camın altındaki taş şerit
            const float glassTop = 3.45f;     // vitrin camının üstü
            const float bandBottom = 3.6f, bandTop = 4.75f;
            const float pil = 0.4f;           // köşe ayakları

            // Gövde: arka ve yan duvarlar, ön cephede ayaklar ve cam üstü duvar.
            Box(root, "ArkaDuvar", new Vector3(0, H * 0.5f, -hd + t * 0.5f), new Vector3(W, H, t), m.wall);
            Box(root, "SolDuvar", new Vector3(-hw + t * 0.5f, H * 0.5f, 0), new Vector3(t, H, D), m.wall);
            Box(root, "SagDuvar", new Vector3(hw - t * 0.5f, H * 0.5f, 0), new Vector3(t, H, D), m.wall);
            Box(root, "OnSolAyak", new Vector3(-hw + pil * 0.5f, H * 0.5f, hd - t * 0.5f), new Vector3(pil, H, t), m.wall);
            Box(root, "OnSagAyak", new Vector3(hw - pil * 0.5f, H * 0.5f, hd - t * 0.5f), new Vector3(pil, H, t), m.wall);
            Box(root, "OnUstDuvar", new Vector3(0, (glassTop + H) * 0.5f, hd - t * 0.5f), new Vector3(W, H - glassTop, t), m.wall);
            Box(root, "Cati", new Vector3(0, H - 0.6f, 0), new Vector3(W - 2 * t, 0.12f, D - 2 * t), m.trim);
            // Parapet şapkası ve köşe kaplamaları.
            Box(root, "Sapka", new Vector3(0, H + 0.05f, 0), new Vector3(W + 0.18f, 0.12f, D + 0.18f), m.trim);
            Box(root, "SapkaIc", new Vector3(0, H - 0.02f, 0), new Vector3(W - 0.5f, 0.06f, D - 0.5f), m.stone);
            Box(root, "Kaide", new Vector3(0, plinth * 0.5f, hd - t * 0.5f + 0.03f), new Vector3(W + 0.06f, plinth, t + 0.06f), m.stone);
            // Çatıda klima ve havalandırma kutuları.
            Box(root, "Klima", new Vector3(hw * 0.45f, H + 0.45f, -hd * 0.35f), new Vector3(1.2f, 0.8f, 0.9f), m.stone);
            Box(root, "Baca", new Vector3(-hw * 0.5f, H + 0.35f, -hd * 0.5f), new Vector3(0.5f, 0.6f, 0.5f), m.trim);

            // Vitrin: ön cephe boyunca cam, dikmeler, kapı.
            float gx0 = -hw + pil, gx1 = hw - pil, gw = gx1 - gx0;
            float gz = hd - t * 0.5f;
            Box(root, "VitrinCam", new Vector3(0, (plinth + glassTop) * 0.5f, gz), new Vector3(gw, glassTop - plinth, 0.03f), m.glass);
            int bays = Mathf.Max(2, Mathf.RoundToInt(gw / 1.7f));
            for (int i = 0; i <= bays; i++)
            {
                float x = gx0 + gw * i / bays;
                Box(root, "Dikme", new Vector3(x, (plinth + glassTop) * 0.5f, gz + 0.04f), new Vector3(0.09f, glassTop - plinth, 0.1f), m.trim);
            }
            Box(root, "Kayit", new Vector3(0, 2.95f, gz + 0.04f), new Vector3(gw, 0.08f, 0.1f), m.trim);
            Box(root, "CamUstu", new Vector3(0, glassTop + 0.04f, gz + 0.04f), new Vector3(gw, 0.1f, 0.12f), m.trim);
            // Kapı: ortada, koyu çerçeveli, kolu ve basamağı var.
            const float dw = 1.4f, dh = 2.6f;
            Box(root, "KapiCerceve", new Vector3(0, plinth + dh * 0.5f, gz + 0.06f), new Vector3(dw + 0.16f, dh + 0.12f, 0.08f), m.trim);
            Box(root, "Kapi", new Vector3(0, plinth + dh * 0.5f, gz + 0.1f), new Vector3(dw, dh, 0.03f), m.glass);
            Box(root, "KapiKolu", new Vector3(0.45f, plinth + 1.1f, gz + 0.16f), new Vector3(0.05f, 0.6f, 0.05f), m.stone);
            Box(root, "Basamak", new Vector3(0, 0.08f, hd + 0.35f), new Vector3(dw + 0.8f, 0.16f, 0.7f), m.stone);
            // Kapının önündeki kaide boşluğu (kapı yere kadar iner).
            Box(root, "KapiAlti", new Vector3(0, plinth * 0.5f, gz + 0.1f), new Vector3(dw, plinth, 0.04f), m.glass);

            // Tabela bandı: cephe boyunca; yazı ortada.
            Box(root, "TabelaBandi", new Vector3(0, (bandBottom + bandTop) * 0.5f, hd + 0.08f), new Vector3(W - 0.2f, bandTop - bandBottom, 0.16f), m.signFrame);
            var sign = new GameObject("Tabela").transform;
            sign.SetParent(root, false);
            sign.localPosition = new Vector3(0, (bandBottom + bandTop) * 0.5f, hd + 0.17f);
            sign.localRotation = Quaternion.Euler(0, 180f, 0);
            float sw = Mathf.Min(W - 1.2f, 7.5f), sh = (bandTop - bandBottom) - 0.2f;
            Box(sign, "Cerceve", new Vector3(0, 0, 0.02f), new Vector3(sw + 0.14f, sh + 0.12f, 0.03f), m.trim);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Yazi";
            face.transform.SetParent(sign, false);
            face.transform.localPosition = new Vector3(0, 0, -0.005f);
            face.transform.localScale = new Vector3(sw, sh, 1f);
            face.GetComponent<Renderer>().sharedMaterial = m.sign;
            // Bandın altında geceleri yanan ışık şeridi (vitrini aydınlatır).
            Box(root, "TabelaIsigi", new Vector3(0, bandBottom - 0.04f, hd + 0.12f), new Vector3(W - 0.6f, 0.05f, 0.08f), m.glow);

            // Yan vitrinler: köşeden geriye doğru.
            float sideLen = Mathf.Min(D * 0.6f, 4.5f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (hw - t * 0.5f);
                float zc = hd - 0.6f - sideLen * 0.5f;
                Box(root, "YanCam", new Vector3(x + sx * 0.02f, 2.05f, zc), new Vector3(0.03f, 2.3f, sideLen), m.glass);
                Box(root, "YanCerceveUst", new Vector3(x + sx * 0.05f, 3.24f, zc), new Vector3(0.08f, 0.1f, sideLen + 0.1f), m.trim);
                Box(root, "YanCerceveAlt", new Vector3(x + sx * 0.06f, 0.88f, zc), new Vector3(0.12f, 0.1f, sideLen + 0.1f), m.trim);
                int n = Mathf.Max(1, Mathf.RoundToInt(sideLen / 1.6f));
                for (int i = 0; i <= n; i++)
                    Box(root, "YanDikme", new Vector3(x + sx * 0.05f, 2.05f, zc - sideLen * 0.5f + sideLen * i / n), new Vector3(0.08f, 2.3f, 0.08f), m.trim);
            }

            // Arka ve yan cephelerde pencereler (gece yanar) ve arka kapı: arkadan bakınca da dükkan görünsün.
            BackFacade(root, W, D, H, m);
            Interior(root, W, D, id, m);
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = true;
            return root.gameObject;
        }

        static void BackFacade(Transform root, float W, float D, float H, Materials m)
        {
            float hw = W * 0.5f, hd = D * 0.5f;
            var win = m.window != null ? m.window : m.glass;
            void Window(Vector3 pos, bool alongX)
            {
                var size = alongX ? new Vector3(1.3f, 1.5f, 0.05f) : new Vector3(0.05f, 1.5f, 1.3f);
                var frame = alongX ? new Vector3(1.48f, 1.68f, 0.06f) : new Vector3(0.06f, 1.68f, 1.48f);
                var outDir = alongX ? new Vector3(0, 0, Mathf.Sign(pos.z)) : new Vector3(Mathf.Sign(pos.x), 0, 0);
                Box(root, "PencereCerceve", pos + outDir * 0.01f, frame, m.trim);
                Box(root, "Pencere", pos + outDir * 0.03f, size, win);
                var sill = alongX ? new Vector3(1.6f, 0.08f, 0.16f) : new Vector3(0.16f, 0.08f, 1.6f);
                Box(root, "Denizlik", pos - Vector3.up * 0.86f + outDir * 0.07f, sill, m.stone);
            }
            // Arka cephe: pencereler ve bir servis kapısı.
            int n = Mathf.Max(2, Mathf.FloorToInt(W / 2.6f));
            for (int i = 0; i < n; i++)
            {
                float x = -hw + W * (i + 0.5f) / n;
                if (i == 0) { Box(root, "ArkaKapi", new Vector3(x, 1.15f, -hd - 0.02f), new Vector3(1.1f, 2.3f, 0.06f), m.trim); continue; }
                Window(new Vector3(x, 2.3f, -hd), true);
            }
            // Yanlar: vitrin camının gerisinde pencereler.
            float sideStart = hd - 0.6f - Mathf.Min(D * 0.6f, 4.5f);
            foreach (float sx in new[] { -1f, 1f })
                for (float z = sideStart - 1.4f; z > -hd + 1f; z -= 2.6f)
                    Window(new Vector3(sx * hw, 2.3f, z), false);
            // Cephe boyunca kat silmesi (düz duvarı böler).
            Box(root, "SilmeArka", new Vector3(0, bandTopY, -hd - 0.04f), new Vector3(W + 0.08f, 0.14f, 0.08f), m.trim);
            Box(root, "SilmeSol", new Vector3(-hw - 0.04f, bandTopY, 0), new Vector3(0.08f, 0.14f, D + 0.08f), m.trim);
            Box(root, "SilmeSag", new Vector3(hw + 0.04f, bandTopY, 0), new Vector3(0.08f, 0.14f, D + 0.08f), m.trim);
        }

        const float bandTopY = 3.95f;

        /// <summary>Camın arkasından görünen iç: zemin, aydınlık arka duvar, tavan ışığı, ürünlü raflar, tezgah.</summary>
        static void Interior(Transform root, float W, float D, string id, Materials m)
        {
            float hw = W * 0.5f, hd = D * 0.5f;
            const float inset = 0.3f;
            Box(root, "IcZemin", new Vector3(0, 0.2f, 0), new Vector3(W - 0.6f, 0.04f, D - 0.6f), m.interiorFloor);
            float backZ = hd - Mathf.Min(3.2f, D * 0.55f);
            Box(root, "IcArka", new Vector3(0, 1.8f, backZ), new Vector3(W - 0.6f, 3.3f, 0.1f), m.interiorWall);
            Box(root, "IcTavan", new Vector3(0, 3.5f, (backZ + hd) * 0.5f), new Vector3(W - 0.6f, 0.06f, hd - backZ), m.interiorWall);
            int lamps = Mathf.Max(2, Mathf.RoundToInt(W / 2.6f));
            for (int i = 0; i < lamps; i++)
                Box(root, "IcLamba", new Vector3(-hw + inset + (W - 2 * inset) * (i + 0.5f) / lamps, 3.44f, (backZ + hd) * 0.5f), new Vector3(0.9f, 0.04f, 0.3f), m.glow);
            // Arka duvarda raflar ve ürün şeritleri.
            var colors = Products(id);
            int shelves = Mathf.Max(2, Mathf.FloorToInt((W - 1f) / 1.5f));
            for (int i = 0; i < shelves; i++)
            {
                float x = -hw + 0.5f + (W - 1f) * (i + 0.5f) / shelves;
                Box(root, "Raf", new Vector3(x, 1.15f, backZ + 0.3f), new Vector3(1.25f, 1.9f, 0.45f), m.wood);
                for (int k = 0; k < 4; k++)
                {
                    var c = colors[(i + k) % colors.Length];
                    Box(root, "Urun", new Vector3(x, 0.55f + k * 0.42f, backZ + 0.56f), new Vector3(1.1f, 0.2f, 0.1f), ProductMat(c));
                }
            }
            // Tezgah: bir yanda.
            Box(root, "Tezgah", new Vector3(hw - 1.4f, 0.65f, hd - 1.6f), new Vector3(1.6f, 0.9f, 0.6f), m.wood);
        }

        static Material ProductMat(Color c)
        {
            if (productMats.TryGetValue(c, out var mat) && mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "VitrinUrun" };
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.3f);
            return productMats[c] = mat;
        }

        /// <summary>Metre UV'li kutu (dokular her boyutta gerçek ölçekte görünür).</summary>
        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = InteriorBuilder.MetricBox(size);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (mat != null && mat.name.StartsWith("VitrinCami")) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
