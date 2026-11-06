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

        // ------------------------------------------------------------ Kimlik: ne dükkanı olduğu uzaktan belli olsun

        /// <summary>Çatıdaki dev ürün: fırında kruvasan, manavda elma, kafede kahve fincanı, markette alışveriş arabası...</summary>
        static string RoofModel(string id)
        {
            switch (id)
            {
                case "simit_bufesi": return "food/donut";
                case "firin": return "ph/croissant";
                case "manav": return "ph/food_apple_01";
                case "kasap": return "food/whole-ham";
                case "kafe": return "food/cup-coffee";
                case "bakkal": return "mm/shopping-basket";
                case "market":
                case "supermarket": return "mm/shopping-cart";
                case "eczane": return null;
                case "berber": return null;
                default: return null;
            }
        }

        /// <summary>
        /// İşletmeyi uzaktan tanıtan parçalar: çatıda kaide üstünde dev ürün (ışıklı), fırında tuğla baca,
        /// berberde kapı yanında dönen direk ve çatıda dev direk, eczanede cepheden taşan ışıklı yeşil haç.
        /// </summary>
        static void Identity(Transform root, float W, float D, float H, string id, Materials m)
        {
            float hw = W * 0.5f, hd = D * 0.5f;
            var roofFront = new Vector3(0, H, hd - Mathf.Min(1.6f, D * 0.3f));
            float iconSize = Mathf.Clamp(W * 0.52f, 2.6f, 4.6f);
            string key = RoofModel(id);
            if (key != null && ModelFor != null)
            {
                var prefab = ModelFor(key);
                if (prefab != null)
                {
                    // Kaide: koyu kutu ve üst kenarında ışık şeridi.
                    Box(root, "Kaide", roofFront + new Vector3(0, 0.25f, 0), new Vector3(iconSize * 0.8f, 0.5f, iconSize * 0.6f), m.trim);
                    Box(root, "KaideIsigi", roofFront + new Vector3(0, 0.52f, iconSize * 0.3f + 0.01f), new Vector3(iconSize * 0.8f, 0.05f, 0.04f), m.glow);
                    var icon = Object.Instantiate(prefab, root);
                    icon.name = "DevUrun";
                    foreach (var c in icon.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    // Simit dik durur (halkası sokağa bakar); diğerleri hafif dönük.
                    icon.transform.localRotation = id == "simit_bufesi" ? Quaternion.Euler(90f, 0, 0)
                        : Quaternion.Euler(0, id == "kafe" ? 200f : id == "manav" ? 20f : 160f, 0);
                    icon.transform.localPosition = Vector3.zero;
                    var b = Bounds(icon);
                    float big = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (big > 0.0001f) icon.transform.localScale *= iconSize / big;
                    b = Bounds(icon);
                    // Kaidenin üstüne otur, ortala.
                    var target = root.TransformPoint(roofFront + new Vector3(0, 0.55f, 0));
                    icon.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
                    if (id == "simit_bufesi")
                    {
                        // Kenney çöreği simit rengine boyanır.
                        var mat = ProductMat(new Color(0.78f, 0.5f, 0.22f));
                        foreach (var r in icon.GetComponentsInChildren<Renderer>()) r.sharedMaterials = new[] { mat };
                    }
                }
            }
            switch (id)
            {
                case "firin":
                {
                    // Tuğla fırın bacası.
                    var p = new Vector3(-hw + 0.9f, H, -hd + 1.0f);
                    Box(root, "FirinBacasi", p + new Vector3(0, 1.3f, 0), new Vector3(0.9f, 2.6f, 0.9f), m.wall);
                    Box(root, "BacaSapka", p + new Vector3(0, 2.66f, 0), new Vector3(1.1f, 0.14f, 1.1f), m.trim);
                    break;
                }
                case "berber":
                {
                    BarberPole(root, new Vector3(-hw + 0.75f, 0, hd + 0.35f), 2.4f, 0.14f);
                    BarberPole(root, roofFront + new Vector3(0, 0, 0), 3.2f, 0.45f);
                    break;
                }
                case "eczane":
                {
                    // Cepheden dışarı taşan ışıklı yeşil haç (iki yüzlü) ve çatıda büyük haç.
                    var green = ProductMat(new Color(0.1f, 0.8f, 0.35f));
                    green.EnableKeyword("_EMISSION");
                    green.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 0.35f) * 1.4f);
                    var blade = new Vector3(hw + 0.55f, 3.9f, hd - 0.4f);
                    Box(root, "HacKolu", blade + new Vector3(-0.35f, 0, 0), new Vector3(0.7f, 0.08f, 0.08f), m.trim);
                    Box(root, "HacDikey", blade + new Vector3(0.2f, 0, 0), new Vector3(0.12f, 1.0f, 0.34f), green);
                    Box(root, "HacYatay", blade + new Vector3(0.2f, 0, 0), new Vector3(0.12f, 0.34f, 1.0f), green);
                    Box(root, "CatiHacDikey", roofFront + new Vector3(0, 1.45f, 0), new Vector3(0.75f, 2.5f, 0.3f), green);
                    Box(root, "CatiHacYatay", roofFront + new Vector3(0, 1.45f, 0), new Vector3(2.5f, 0.75f, 0.3f), green);
                    Box(root, "CatiHacKaide", roofFront + new Vector3(0, 0.1f, 0), new Vector3(1.4f, 0.2f, 0.8f), m.trim);
                    break;
                }
            }
        }

        static Material barberMat;

        /// <summary>Berber direği: kırmızı-beyaz-mavi sarmal şeritli silindir, üstte ve altta metal başlık.</summary>
        static void BarberPole(Transform root, Vector3 basePos, float height, float radius)
        {
            if (barberMat == null)
            {
                const int W = 64, Hh = 64;
                var tex = new Texture2D(W, Hh, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "BerberDirek" };
                var px = new Color[W * Hh];
                for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    int band = ((x + y) / 11) % 4;
                    px[y * W + x] = band == 0 ? new Color(0.85f, 0.1f, 0.1f) : band == 2 ? new Color(0.1f, 0.25f, 0.75f) : Color.white;
                }
                tex.SetPixels(px);
                tex.Apply(true);
                barberMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BerberDirek" };
                barberMat.SetTexture("_BaseMap", tex);
                barberMat.SetTextureScale("_BaseMap", new Vector2(1f, 3f));
                barberMat.SetFloat("_Smoothness", 0.7f);
            }
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.name = "BerberDiregi";
            pole.transform.SetParent(root, false);
            pole.transform.localPosition = basePos + new Vector3(0, height * 0.5f + 0.15f, 0);
            pole.transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);
            pole.GetComponent<Renderer>().sharedMaterial = barberMat;
            var metal = ProductMat(new Color(0.8f, 0.8f, 0.82f));
            foreach (float y in new[] { 0.08f, height + 0.22f })
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.name = "DirekBasligi";
                cap.transform.SetParent(root, false);
                cap.transform.localPosition = basePos + new Vector3(0, y, 0);
                cap.transform.localScale = Vector3.one * radius * 2.6f;
                cap.GetComponent<Renderer>().sharedMaterial = metal;
            }
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>İşletmeye göre istenen genişlik, derinlik ve yükseklik (metre).</summary>
        public static (float w, float d, float h) Proportions(string id)
        {
            switch (id)
            {
                case "simit_bufesi": return (5.0f, 4.2f, 5.2f);
                case "berber": return (5.8f, 5.0f, 5.6f);
                case "bakkal": return (6.0f, 5.4f, 5.8f);
                case "manav": return (6.2f, 4.8f, 5.6f);
                case "eczane": return (6.4f, 5.6f, 6.0f);
                case "kasap": return (6.4f, 5.6f, 6.0f);
                case "firin": return (6.8f, 5.8f, 6.2f);
                case "kafe": return (6.8f, 5.2f, 6.2f);
                case "market": return (6.8f, 6.4f, 6.8f);
                case "supermarket": return (15f, 12f, 7.4f);
                default: return (6.4f, 5.6f, 6.0f);
            }
        }

        /// <summary>
        /// Binanın önünde kalan boşluğa işletmeye göre süs: kafe ve simitte masa-şemsiye,
        /// manavda meyve kasaları, markette alışveriş arabaları, diğerlerinde saksılar.
        /// </summary>
        public static void Frontage(Transform building, Vector2 size, float depth, string id, Materials m)
        {
            float hd = size.y * 0.5f, hw = size.x * 0.5f;
            float z0 = hd + 0.9f;
            bool room = depth > 1.6f;
            switch (id)
            {
                case "kafe":
                case "simit_bufesi":
                    if (!room) break;
                    foreach (float x in new[] { -hw + 0.9f, hw - 0.9f })
                    {
                        float z = z0 + Mathf.Min(depth - 1.4f, 1.6f);
                        Box(building, "Masa", new Vector3(x, 0.72f, z), new Vector3(0.75f, 0.05f, 0.75f), m.trim);
                        Box(building, "MasaAyak", new Vector3(x, 0.36f, z), new Vector3(0.07f, 0.72f, 0.07f), m.trim);
                        Box(building, "SemsiyeDirek", new Vector3(x, 1.25f, z), new Vector3(0.05f, 2.5f, 0.05f), m.trim);
                        Box(building, "Semsiye", new Vector3(x, 2.45f, z), new Vector3(2.0f, 0.08f, 2.0f), ProductMat(id == "kafe" ? new Color(0.15f, 0.45f, 0.3f) : new Color(0.85f, 0.25f, 0.2f)));
                        foreach (float dx in new[] { -0.6f, 0.6f })
                            Box(building, "Tabure", new Vector3(x + dx, 0.24f, z), new Vector3(0.38f, 0.48f, 0.38f), m.wood);
                    }
                    break;
                case "manav":
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -hw + 0.8f + i * (size.x - 1.6f) / 3f;
                        var colors = Products(id);
                        Box(building, "KasaSehpa", new Vector3(x, 0.3f, z0 - 0.35f), new Vector3(1.0f, 0.6f, 0.6f), m.wood);
                        Box(building, "Meyve", new Vector3(x, 0.66f, z0 - 0.35f), new Vector3(0.9f, 0.12f, 0.5f), ProductMat(colors[i % colors.Length]));
                    }
                    break;
                case "market":
                case "supermarket":
                    for (int i = 0; i < 3; i++)
                        Box(building, "AlisverisArabasi", new Vector3(-hw + 0.8f + i * 0.35f, 0.5f, z0 - 0.2f), new Vector3(0.55f, 0.6f, 0.9f), ProductMat(new Color(0.7f, 0.72f, 0.75f)));
                    break;
                default:
                    foreach (float x in new[] { -hw + 0.45f, hw - 0.45f })
                    {
                        Box(building, "Saksi", new Vector3(x, 0.3f, z0 - 0.45f), new Vector3(0.6f, 0.6f, 0.6f), m.stone);
                        Box(building, "Bitki", new Vector3(x, 0.85f, z0 - 0.45f), new Vector3(0.5f, 0.6f, 0.5f), ProductMat(new Color(0.25f, 0.5f, 0.22f)));
                    }
                    break;
            }
        }

        static readonly Dictionary<Color, Material> productMats = new Dictionary<Color, Material>();

        /// <summary>Binayı kurar. size: genişlik (x) ve derinlik (z), height: parapet dahil yükseklik.</summary>
        /// <summary>Model anahtarından ("ph/croissant", "food/donut", "mm/shopping-cart") prefab veren işlev (TownBuilder sağlar).</summary>
        public static System.Func<string, GameObject> ModelFor;

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
            // Alçak binada (büfe) cam ve tabela bandı aşağı kayar.
            float glassTop = Mathf.Min(3.45f, H - 1.95f);   // vitrin camının üstü
            float bandBottom = glassTop + 0.15f, bandTop = Mathf.Min(glassTop + 1.3f, H - 0.15f);
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
            float dw = 1.4f, dh = Mathf.Min(2.6f, glassTop - plinth - 0.1f);
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
                Box(root, "YanCam", new Vector3(x + sx * 0.02f, (0.9f + glassTop) * 0.5f, zc), new Vector3(0.03f, glassTop - 0.9f, sideLen), m.glass);
                Box(root, "YanCerceveUst", new Vector3(x + sx * 0.05f, glassTop, zc), new Vector3(0.08f, 0.1f, sideLen + 0.1f), m.trim);
                Box(root, "YanCerceveAlt", new Vector3(x + sx * 0.06f, 0.88f, zc), new Vector3(0.12f, 0.1f, sideLen + 0.1f), m.trim);
                int n = Mathf.Max(1, Mathf.RoundToInt(sideLen / 1.6f));
                for (int i = 0; i <= n; i++)
                    Box(root, "YanDikme", new Vector3(x + sx * 0.05f, (0.9f + glassTop) * 0.5f, zc - sideLen * 0.5f + sideLen * i / n), new Vector3(0.08f, glassTop - 0.9f, 0.08f), m.trim);
            }

            // Arka ve yan cephelerde pencereler (gece yanar) ve arka kapı: arkadan bakınca da dükkan görünsün.
            BackFacade(root, W, D, H, m);
            Interior(root, W, D, id, m);
            Identity(root, W, D, H, id, m);
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
            // Arka kapının üstünde saçak ve lamba, köşelerde yağmur borusu, duvarda klima, yanında çöp kutuları.
            float doorX = -hw + W * 0.5f / n;
            Box(root, "ArkaSacak", new Vector3(doorX, 2.5f, -hd - 0.35f), new Vector3(1.6f, 0.08f, 0.7f), m.trim);
            Box(root, "ArkaLamba", new Vector3(doorX, 2.7f, -hd - 0.06f), new Vector3(0.25f, 0.15f, 0.12f), m.glow);
            foreach (float sx in new[] { -1f, 1f })
                Box(root, "YagmurBorusu", new Vector3(sx * (hw - 0.12f), H * 0.5f, -hd - 0.08f), new Vector3(0.1f, H, 0.1f), m.stone);
            if (W > 4.5f)
                Box(root, "DuvarKlimasi", new Vector3(hw - 0.9f, Mathf.Min(H - 0.7f, bandTopY + 0.55f), -hd - 0.25f), new Vector3(0.9f, 0.6f, 0.45f), m.interiorWall);
            Box(root, "CopKutusu", new Vector3(doorX + 1.2f, 0.55f, -hd - 0.5f), new Vector3(0.7f, 1.1f, 0.7f), ProductMat(new Color(0.2f, 0.42f, 0.25f)));
            Box(root, "CopKutusu", new Vector3(doorX + 2.0f, 0.55f, -hd - 0.5f), new Vector3(0.7f, 1.1f, 0.7f), ProductMat(new Color(0.25f, 0.3f, 0.45f)));
            // Duvar dibinde taş kaide (arka ve yanlar).
            Box(root, "KaideArka", new Vector3(0, 0.25f, -hd - 0.02f), new Vector3(W + 0.04f, 0.5f, 0.06f), m.stone);
            Box(root, "KaideSol", new Vector3(-hw - 0.02f, 0.25f, 0), new Vector3(0.06f, 0.5f, D + 0.04f), m.stone);
            Box(root, "KaideSag", new Vector3(hw + 0.02f, 0.25f, 0), new Vector3(0.06f, 0.5f, D + 0.04f), m.stone);
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
