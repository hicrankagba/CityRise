using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityRise.Interior
{
    /// <summary>
    /// İşletmenin içini izometrik kesit (diorama) olarak kurar: çatı yok, ön iki duvar alçak,
    /// arka duvarlar pencereli, karo zemin, altında beton temel ve toprak dilimi, tepede tabela.
    /// İç düzen işletme türüne ve seviyesine göre üretilir; seviye arttıkça raf ve tezgah sayısı artar.
    /// Kamera odaya -X/-Z köşesinden bakar; bu yüzden +X ve +Z duvarları yüksek, diğerleri alçaktır.
    /// </summary>
    public class InteriorBuilder
    {
        /// <summary>Kenney mobilya ve market modellerinin ön yüzü -Z'ye bakar.</summary>
        const float ModelFrontYaw = 180f;
        const float WallHeight = 3.4f;

        readonly InteriorLibrary lib;
        readonly Transform root;
        readonly System.Random rng;
        public readonly Vector2 size;
        float HalfW => size.x * 0.5f;
        float HalfD => size.y * 0.5f;

        public static Vector2 RoomSize(string businessId, int level)
        {
            switch (businessId)
            {
                case "simit_bufesi": return new Vector2(6f, 5f);
                case "berber": return new Vector2(7f, 6f);
                case "kafe": return new Vector2(9f, 7f);
                case "market": return new Vector2(12f, 9f);
                case "supermarket": return new Vector2(16f, 12f);
                case "belediye": return new Vector2(11f, 8f);
                default: return new Vector2(8f, 6f);
            }
        }

        InteriorBuilder(InteriorLibrary lib, Transform root, Vector2 size, int seed)
        {
            this.lib = lib;
            this.root = root;
            this.size = size;
            rng = new System.Random(seed);
        }

        /// <summary>Diorama kurar ve kökünü döndürür. origin: odanın zemin ortası (dünya).</summary>
        /// <param name="look">Oyuncunun seçtiği görünüm; boşsa (komşu dükkanı) türün varsayılanı ve bütün süsler.</param>
        /// <param name="products">Oyuncunun dükkanında satılan ürünlerin model anahtarları (raflara bunlar dizilir).</param>
        /// <param name="stockRatio">Raf doluluğu 0-1 (stok azaldıkça raflar boşalır).</param>
        public static GameObject Build(InteriorLibrary lib, string businessId, int level, Material signMaterial, Vector3 origin,
            CityRise.Progression.ShopLook look = null, string[] products = null, float stockRatio = 1f)
        {
            var go = new GameObject("IcGorunum_" + businessId);
            go.transform.position = origin;
            var size = RoomSize(businessId, level);
            var b = new InteriorBuilder(lib, go.transform, size, businessId.GetHashCode() ^ level);
            b.style = StyleFor(businessId);
            b.look = look;
            if (products != null && products.Length > 0)
            {
                // Yalnızca kütüphanede modeli olan ürünler.
                var ok = new List<string>();
                foreach (var key in products) if (lib.Get(key) != null) ok.Add(key);
                if (ok.Count > 0) b.productOverride = ok.ToArray();
            }
            b.stock = Mathf.Clamp01(stockRatio);
            b.ApplyLook();
            b.Shell(signMaterial);
            switch (businessId)
            {
                case "market": b.Market(level, false); break;
                case "supermarket": b.Market(level, true); break;
                case "manav": b.Manav(level); break;
                case "kasap": b.Kasap(level); break;
                case "firin": b.Firin(level); break;
                case "simit_bufesi": b.Simit(level); break;
                case "bakkal": b.Bakkal(level); break;
                case "kafe": b.Kafe(level); break;
                case "eczane": b.Eczane(level); break;
                case "berber": b.Berber(level); break;
                case "belediye": b.Belediye(level); break;
                default: b.Bakkal(level); break;
            }
            b.Decorate(businessId);
            b.Lights();
            b.Animate();
            return go;
        }

        // ------------------------------------------------------------ Yardımcılar

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        readonly List<GameObject> furniture = new List<GameObject>();
        readonly List<GameObject> customers = new List<GameObject>();
        readonly List<GameObject> staff = new List<GameObject>();
        readonly List<GameObject> seated = new List<GameObject>();
        readonly List<GameObject> waiting = new List<GameObject>();
        List<string> realPeople;

        /// <summary>Gerçekçi (Mixamo) karakter anahtarları; yoksa boş liste.</summary>
        List<string> RealPeople()
        {
            if (realPeople != null) return realPeople;
            realPeople = new List<string>();
            foreach (var m in lib.models) if (m.prefab != null && m.key.StartsWith("mx/")) realPeople.Add(m.key);
            return realPeople;
        }

        string RandomRealPerson() => RealPeople().Count > 0 ? RealPeople()[rng.Next(RealPeople().Count)] : null;
        Vector3? registerSpot;

        GameObject Place(string key, float x, float z, Vector3 facing, float y = 0f, float extraScale = 1f)
        {
            bool isStaff = key == "mm/employee";
            // Çalışanlar da gerçekçi karakterlerden seçilir (varsa).
            if (isStaff && RandomRealPerson() is string real) key = real;
            var e = lib.Get(key);
            if (e == null) return null;
            bool person = key.StartsWith("ch/") || key.StartsWith("mx/") || isStaff;
            bool frontIsZ = key.StartsWith("ch/") || key.StartsWith("mx/");
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y + (frontIsZ ? 0f : ModelFrontYaw);
            // Kişiler bir taşıyıcı nesnenin içinde: taşıyıcı yürür, model animasyonla hareket eder.
            var holder = person ? new GameObject(isStaff ? "Calisan" : "Musteri").transform : null;
            if (holder != null)
            {
                holder.SetParent(root, false);
                holder.localPosition = new Vector3(x, y, z);
                holder.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            var go = Object.Instantiate(e.prefab, holder != null ? holder : root);
            if (holder != null)
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(0, frontIsZ ? 0f : ModelFrontYaw, 0);
            }
            else
            {
                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            go.transform.localScale = Vector3.one * e.scale * extraScale;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            if (holder != null)
            {
                (isStaff ? staff : customers).Add(holder.gameObject);
                return holder.gameObject;
            }
            if (y < 0.3f) furniture.Add(go);

            return go;
        }

        /// <summary>
        /// Müşterileri canlandırır: odada mobilyaya çarpmayan noktalar bulunur, müşteriler bunlar arasında dolaşır.
        /// </summary>
        void Animate()
        {
            var blocked = new List<Bounds>();
            foreach (var f in furniture)
            {
                var rs = f.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                b.Expand(new Vector3(0.7f, 10f, 0.7f));
                blocked.Add(b);
            }
            var spots = new List<Vector3>();
            for (float x = -HalfW + 0.7f; x <= HalfW - 0.7f; x += 0.6f)
            for (float z = -HalfD + 0.7f; z <= HalfD - 0.7f; z += 0.6f)
            {
                var world = root.TransformPoint(new Vector3(x, 0.5f, z));
                bool free = true;
                foreach (var b in blocked) if (b.Contains(world)) { free = false; break; }
                if (free) spots.Add(new Vector3(x, 0, z));
            }
            // Kasa önündeki en yakın boş nokta.
            Vector3 reg = spots.Count > 0 ? spots[0] : Vector3.zero;
            if (registerSpot.HasValue)
            {
                float best = float.MaxValue;
                foreach (var sp in spots)
                {
                    float d = Vector3.Distance(sp, registerSpot.Value);
                    if (d < best) { best = d; reg = sp; }
                }
            }
            foreach (var c in customers)
            {
                var w = c.AddComponent<CityRise.Life.InteriorWanderer>();
                w.spots = spots;
                w.register = reg;
                w.speed = R(0.8f, 1.2f);
                // Başlangıçta mobilyanın içinde kalan müşteriyi boş bir noktaya al.
                if (spots.Count > 0)
                {
                    var lp = c.transform.localPosition;
                    var world = root.TransformPoint(lp + Vector3.up * 0.5f);
                    foreach (var b in blocked)
                        if (b.Contains(world)) { c.transform.localPosition = spots[rng.Next(spots.Count)]; break; }
                }
            }
            foreach (var s in staff) s.AddComponent<CityRise.Life.InteriorWanderer>().idleOnly = true;
            foreach (var s in seated) s.AddComponent<CityRise.Life.InteriorWanderer>().seated = true;
            foreach (var s in waiting) s.AddComponent<CityRise.Life.InteriorWanderer>().idleOnly = true;
        }

        /// <summary>
        /// Kutu. UV'leri metre cinsindendir; böylece paylaşılan malzemenin dokusu (tiling = 1 / doku metresi)
        /// her boyuttaki duvar ve zeminde gerçek ölçekte görünür, malzeme kopyalamaya gerek kalmaz.
        /// </summary>
        GameObject Box(string name, Vector3 center, Vector3 size, Material mat, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = center;
            go.AddComponent<MeshFilter>().sharedMesh = MetricBox(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Mesh MetricBox(Vector3 size)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var half = size * 0.5f;
            void Face(Vector3 n, Vector3 u, Vector3 v, float uLen, float vLen)
            {
                var c = Vector3.Scale(n, half);
                var hu = u * uLen * 0.5f;
                var hv = v * vLen * 0.5f;
                int i = verts.Count;
                verts.Add(c - hu - hv); uvs.Add(new Vector2(0, 0));
                verts.Add(c + hu - hv); uvs.Add(new Vector2(uLen, 0));
                verts.Add(c + hu + hv); uvs.Add(new Vector2(uLen, vLen));
                verts.Add(c - hu + hv); uvs.Add(new Vector2(0, vLen));
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.AddRange(new[] { i, i + 3, i + 2, i, i + 2, i + 1 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            var mesh = new Mesh { name = "Kutu" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static readonly Vector3 Front = Vector3.back;   // kameraya doğru
        static readonly Vector3 Back = Vector3.forward;
        static readonly Vector3 Left = Vector3.left;
        static readonly Vector3 Right = Vector3.right;

        // ------------------------------------------------------------ Kabuk

        void Shell(Material signMaterial)
        {
            float w = size.x, d = size.y;
            // Toprak ve beton temel kesiti.
            Box("Toprak", new Vector3(0, -1.25f, 0), new Vector3(w + 0.9f, 2.1f, d + 0.9f), lib.soil);
            Box("Temel", new Vector3(0, -0.3f, 0), new Vector3(w + 0.5f, 0.45f, d + 0.5f), lib.concrete);
            var floorMat = style.floor == FloorKind.Marble && lib.floorMarble != null ? lib.floorMarble
                : style.floor == FloorKind.Parquet && lib.floorParquet != null ? lib.floorParquet
                : style.floor == FloorKind.Planks && lib.floorPlanks != null ? lib.floorPlanks : lib.floorTiles;
            var floor = Box("Zemin", new Vector3(0, -0.05f, 0), new Vector3(w, 0.1f, d), floorMat);
            floor.name = "KaroZemin";

            // Arka duvarlar (yüksek, pencereli), ön duvarlar (alçak kesik).
            float t = 0.22f;
            var wallMat = Tinted(lib.wallInside, style.wall);
            Box("ArkaDuvar", new Vector3(0, WallHeight * 0.5f, HalfD + t * 0.5f), new Vector3(w + 2 * t, WallHeight, t), wallMat);
            Box("SagDuvar", new Vector3(HalfW + t * 0.5f, WallHeight * 0.5f, 0), new Vector3(t, WallHeight, d), wallMat);
            // Lambri (duvarın alt bandı), üstünde ince çıta ve süpürgelik.
            var panel = Flat("Lambri", style.wainscot, 0.35f);
            var trim = Flat("Cita", style.trim, 0.45f);
            Box("ArkaLambri", new Vector3(0, WainscotHeight * 0.5f, HalfD - 0.012f), new Vector3(w, WainscotHeight, 0.025f), panel);
            Box("SagLambri", new Vector3(HalfW - 0.012f, WainscotHeight * 0.5f, 0), new Vector3(0.025f, WainscotHeight, d), panel);
            Box("ArkaCita", new Vector3(0, WainscotHeight, HalfD - 0.025f), new Vector3(w, 0.05f, 0.05f), trim);
            Box("SagCita", new Vector3(HalfW - 0.025f, WainscotHeight, 0), new Vector3(0.05f, 0.05f, d), trim);
            Box("ArkaSupurgelik", new Vector3(0, 0.06f, HalfD - 0.03f), new Vector3(w, 0.12f, 0.06f), trim);
            Box("SagSupurgelik", new Vector3(HalfW - 0.03f, 0.06f, 0), new Vector3(0.06f, 0.12f, d), trim);
            Box("OnDuvar", new Vector3(0, 0.3f, -HalfD - t * 0.5f), new Vector3(w + 2 * t, 0.6f, t), lib.wallBrick);
            Box("SolDuvar", new Vector3(-HalfW - t * 0.5f, 0.3f, 0), new Vector3(t, 0.6f, d), lib.wallBrick);
            // Duvar üstlerinde koyu kesit çizgisi (görseldeki gibi).
            Box("ArkaKesit", new Vector3(0, WallHeight + 0.03f, HalfD + t * 0.5f), new Vector3(w + 2 * t, 0.06f, t + 0.02f), lib.wallCap);
            Box("SagKesit", new Vector3(HalfW + t * 0.5f, WallHeight + 0.03f, 0), new Vector3(t + 0.02f, 0.06f, d + 2 * t), lib.wallCap);
            Box("OnKesit", new Vector3(0, 0.63f, -HalfD - t * 0.5f), new Vector3(w + 2 * t, 0.06f, t + 0.02f), lib.wallCap);
            Box("SolKesit", new Vector3(-HalfW - t * 0.5f, 0.63f, 0), new Vector3(t + 0.02f, 0.06f, d + 2 * t), lib.wallCap);

            // Pencereler: çerçeveli, ortası kayıtlı, altında denizlik.
            for (float x = -HalfW + 1.4f; x < HalfW - 1f; x += 2.6f) Window(new Vector3(x + 0.8f, WindowY, HalfD), Vector3.right, Front);
            for (float z = -HalfD + 1.4f; z < HalfD - 1f; z += 2.6f) Window(new Vector3(HalfW, WindowY, z + 0.8f), Vector3.forward, Left);

            // Tabela: arka duvarın üstünde.
            if (signMaterial != null)
            {
                float sw = Mathf.Min(w * 0.6f, 6f);
                Box("TabelaCerceve", new Vector3(0, WallHeight + 0.75f, HalfD + 0.1f), new Vector3(sw + 0.2f, 1.25f, 0.12f), lib.wallCap);
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(face.GetComponent<Collider>());
                face.name = "Tabela";
                face.transform.SetParent(root, false);
                face.transform.localPosition = new Vector3(0, WallHeight + 0.75f, HalfD + 0.03f);
                face.transform.localScale = new Vector3(sw, 1.05f, 1f);
                face.GetComponent<Renderer>().sharedMaterial = signMaterial;
            }
        }

        const float WainscotHeight = 1.0f;
        const float WindowY = 2.45f;

        /// <summary>Duvar penceresi: cam, dört kenar çerçeve, orta kayıt ve denizlik. inward: odaya doğru.</summary>
        void Window(Vector3 center, Vector3 along, Vector3 inward)
        {
            const float ww = 1.8f, wh = 0.9f, f = 0.07f;
            var frame = Flat("PencereCercevesi", style.frame, 0.4f);
            Vector3 Size(float a, float h, float depth) => along * a + Vector3.up * h + new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z)) * depth;
            var glassPos = center + inward * 0.012f;
            Box("Pencere", glassPos, Size(ww, wh, 0.02f), lib.glass);
            Box("CerceveUst", glassPos + Vector3.up * (wh * 0.5f) + inward * 0.02f, Size(ww + f, f, 0.05f), frame);
            Box("CerceveAlt", glassPos - Vector3.up * (wh * 0.5f) + inward * 0.02f, Size(ww + f, f, 0.05f), frame);
            Box("CerceveSol", glassPos - along * (ww * 0.5f) + inward * 0.02f, Size(f, wh + f, 0.05f), frame);
            Box("CerceveSag", glassPos + along * (ww * 0.5f) + inward * 0.02f, Size(f, wh + f, 0.05f), frame);
            Box("Kayit", glassPos + inward * 0.02f, Size(0.04f, wh, 0.04f), frame);
            Box("Denizlik", glassPos - Vector3.up * (wh * 0.5f + 0.05f) + inward * 0.07f, Size(ww + 0.2f, 0.05f, 0.14f), frame);
        }

        // ------------------------------------------------------------ İç mekân stili

        string[] productOverride;
        float stock = 1f;

        /// <summary>Raflara dizilecek ürünler: oyuncunun dükkanında sattığı ürünler, yoksa türün varsayılanı.</summary>
        string[] Items(string[] fallback) => productOverride ?? fallback;

        enum FloorKind { Tiles, Parquet, Planks, Marble }

        CityRise.Progression.ShopLook look;

        /// <summary>Dekor açık mı? Komşu dükkanlarında (görünüm yok) bütün süsler vardır.</summary>
        bool D(string id) => look == null || look.Has(id);

        /// <summary>Oyuncunun seçtiği zemin, renk ve lambayı stile işler.</summary>
        void ApplyLook()
        {
            if (look == null) return;
            if (look.floor >= 0) style.floor = (FloorKind)Mathf.Clamp(look.floor, 0, 3);
            var walls = CityRise.Progression.ShopOptions.WallColors;
            if (look.wall >= 0 && look.wall < walls.Length) style.wall = walls[look.wall].color;
            var panels = CityRise.Progression.ShopOptions.WainscotColors;
            if (look.wainscot >= 0 && look.wainscot < panels.Length)
            {
                style.wainscot = panels[look.wainscot].color;
                style.trim = Color.Lerp(style.wainscot, Color.black, 0.35f);
            }
            if (look.lamp == 0) style.lamp = "ph/modern_ceiling_lamp_01";
            else if (look.lamp == 1) style.lamp = "ph/hanging_industrial_lamp";
        }

        class Style
        {
            public FloorKind floor = FloorKind.Tiles;
            public Color wall = new Color(1f, 0.95f, 0.86f);
            public Color wainscot = new Color(0.55f, 0.38f, 0.24f);
            public Color trim = new Color(0.32f, 0.21f, 0.13f);
            public Color frame = new Color(0.95f, 0.94f, 0.9f);
            /// <summary>Tavandan sarkan lamba modeli.</summary>
            public string lamp = "ph/modern_ceiling_lamp_01";
        }

        Style style = new Style();

        /// <summary>İşletme türüne göre zemin, duvar ve lambri renkleri.</summary>
        static Style StyleFor(string id)
        {
            var s = new Style();
            switch (id)
            {
                case "kafe":
                    s.floor = FloorKind.Parquet; s.wall = new Color(0.96f, 0.88f, 0.76f);
                    s.wainscot = new Color(0.36f, 0.24f, 0.16f); s.trim = new Color(0.22f, 0.15f, 0.1f);
                    s.frame = new Color(0.2f, 0.2f, 0.22f); s.lamp = "ph/hanging_industrial_lamp";
                    break;
                case "firin":
                case "simit_bufesi":
                    s.floor = FloorKind.Planks; s.wall = new Color(1f, 0.9f, 0.74f);
                    s.wainscot = new Color(0.62f, 0.42f, 0.26f); s.lamp = "ph/hanging_industrial_lamp";
                    break;
                case "berber":
                    s.floor = FloorKind.Parquet; s.wall = new Color(0.82f, 0.87f, 0.9f);
                    s.wainscot = new Color(0.95f, 0.95f, 0.93f); s.trim = new Color(0.25f, 0.27f, 0.3f);
                    break;
                case "eczane":
                    s.wall = new Color(0.97f, 0.98f, 0.97f); s.wainscot = new Color(0.62f, 0.83f, 0.74f);
                    s.trim = new Color(0.85f, 0.87f, 0.86f);
                    break;
                case "kasap":
                    s.wall = new Color(0.97f, 0.96f, 0.94f); s.wainscot = new Color(0.86f, 0.3f, 0.26f);
                    s.trim = new Color(0.6f, 0.15f, 0.13f);
                    break;
                case "manav":
                    s.floor = FloorKind.Planks; s.wall = new Color(0.93f, 0.96f, 0.84f);
                    s.wainscot = new Color(0.4f, 0.55f, 0.3f); s.trim = new Color(0.28f, 0.38f, 0.2f);
                    break;
                case "belediye":
                    s.floor = FloorKind.Parquet; s.wall = new Color(0.94f, 0.92f, 0.88f);
                    s.wainscot = new Color(0.45f, 0.3f, 0.2f);
                    break;
                case "market":
                case "supermarket":
                case "bakkal":
                    s.wall = new Color(0.97f, 0.96f, 0.92f); s.wainscot = new Color(0.3f, 0.45f, 0.65f);
                    s.trim = new Color(0.2f, 0.3f, 0.45f);
                    break;
            }
            return s;
        }

        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        /// <summary>Düz renkli malzeme (aynı renk tekrar istenirse aynısı verilir).</summary>
        static Material Flat(string name, Color c, float smooth)
        {
            string key = $"{name}{c}{smooth}";
            if (matCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = c };
            m.SetFloat("_Smoothness", smooth);
            return matCache[key] = m;
        }

        /// <summary>Dokulu malzemenin renk tonlu kopyası (duvar boyası).</summary>
        static Material Tinted(Material src, Color c)
        {
            if (src == null) return null;
            string key = $"{src.name}{c}";
            if (matCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + "_ton" };
            m.SetColor("_BaseColor", c);
            return matCache[key] = m;
        }

        // ------------------------------------------------------------ Süsleme

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Nesnenin üst yüzeyinin oda zeminine göre yüksekliği.</summary>
        float TopY(GameObject go) => go == null ? 0.75f : WorldBounds(go).max.y - root.position.y;

        /// <summary>
        /// Duvara asılan süs (saat, tablo, aplik): duvara yaslanır, en büyük kenarı maxSize'ı geçmez.
        /// back = true: arka duvar (x boyunca), false: sağ duvar (z boyunca).
        /// </summary>
        GameObject WallItem(string key, bool back, float along, float y, float maxSize)
        {
            var go = Place(key, back ? along : HalfW - 0.3f, back ? HalfD - 0.3f : along, back ? Front : Left, y);
            if (go == null) return null;
            var b = WorldBounds(go);
            float big = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (big > maxSize) go.transform.localScale *= maxSize / big;
            b = WorldBounds(go);
            var wallPoint = root.TransformPoint(back ? new Vector3(0, 0, HalfD) : new Vector3(HalfW, 0, 0));
            var shift = back ? new Vector3(0, y - (b.center.y - root.position.y), wallPoint.z - b.max.z - 0.005f)
                             : new Vector3(wallPoint.x - b.max.x - 0.005f, y - (b.center.y - root.position.y), 0);
            go.transform.position += shift;
            furniture.Remove(go);
            return go;
        }

        static Material glassMat;

        /// <summary>Saydam vitrin camı.</summary>
        static Material Glass()
        {
            if (glassMat != null) return glassMat;
            glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "VitrinCami" };
            glassMat.SetFloat("_Surface", 1f);
            glassMat.SetFloat("_Blend", 0f);
            glassMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glassMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMat.SetFloat("_ZWrite", 0f);
            glassMat.SetFloat("_Smoothness", 0.95f);
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMat.renderQueue = (int)RenderQueue.Transparent;
            glassMat.SetColor("_BaseColor", new Color(0.8f, 0.92f, 1f, 0.18f));
            return glassMat;
        }

        /// <summary>Tezgah: gövde ve üst tabla kutulardan; üst yüzeyin yüksekliğini döndürür. Yürüyenler içinden geçmez.</summary>
        float Counter(float x, float z, float length, float depth, Color body, Color top)
        {
            const float h = 1.0f;
            furniture.Add(Box("TezgahGovde", new Vector3(x, h * 0.5f - 0.03f, z), new Vector3(length, h - 0.06f, depth), Flat("TezgahGovde", body, 0.35f)));
            Box("TezgahUst", new Vector3(x, h - 0.03f, z - 0.04f), new Vector3(length + 0.08f, 0.06f, depth + 0.12f), Flat("TezgahUst", top, 0.7f));
            Box("TezgahSupurgelik", new Vector3(x, 0.05f, z - depth * 0.5f + 0.02f), new Vector3(length, 0.1f, 0.06f), Flat("TezgahSupurgelik", top, 0.4f));
            return h;
        }

        /// <summary>Camlı vitrin: ahşap alt dolap, iki katlı cam kutu, içinde ürünler.</summary>
        void Vitrine(float x, float z, float length, string[] items, Color? baseColor = null)
        {
            const float depth = 0.7f, baseH = 0.75f, glassH = 0.55f;
            furniture.Add(Box("VitrinAlt", new Vector3(x, baseH * 0.5f, z), new Vector3(length, baseH, depth), Flat("VitrinAlt", baseColor ?? new Color(0.55f, 0.37f, 0.22f), 0.4f)));
            Box("VitrinTaban", new Vector3(x, baseH + 0.01f, z), new Vector3(length, 0.02f, depth), Flat("VitrinTaban", new Color(0.95f, 0.93f, 0.88f), 0.5f));
            float mid = baseH + glassH * 0.5f;
            Box("VitrinRaf", new Vector3(x, mid, z + 0.08f), new Vector3(length - 0.06f, 0.015f, depth * 0.6f), Glass());
            Box("VitrinOn", new Vector3(x, baseH + glassH * 0.5f, z - depth * 0.5f), new Vector3(length, glassH, 0.01f), Glass());
            Box("VitrinUst", new Vector3(x, baseH + glassH, z), new Vector3(length, 0.01f, depth), Glass());
            Box("VitrinYanSol", new Vector3(x - length * 0.5f, baseH + glassH * 0.5f, z), new Vector3(0.01f, glassH, depth), Glass());
            Box("VitrinYanSag", new Vector3(x + length * 0.5f, baseH + glassH * 0.5f, z), new Vector3(0.01f, glassH, depth), Glass());
            var metal = Flat("VitrinKenar", new Color(0.75f, 0.75f, 0.73f), 0.8f);
            Box("VitrinKenarUst", new Vector3(x, baseH + glassH, z - depth * 0.5f), new Vector3(length, 0.025f, 0.025f), metal);
            // Ürünler: alt katta önde, rafta arkada.
            items = Items(items);
            int n = Mathf.RoundToInt(length / 0.32f);
            int shown = Mathf.RoundToInt(n * stock);
            for (int i = 0; i < shown; i++)
            {
                float px = x - length * 0.5f + 0.18f + i * (length - 0.36f) / Mathf.Max(1, n - 1);
                Place(items[i % items.Length], px, z - 0.12f, Front, baseH + 0.02f);
                if (i % 2 == 0) Place(items[(i + 1) % items.Length], px + 0.1f, z + 0.12f, Front, mid + 0.01f);
            }
        }

        static readonly string[] Drinks = { "ph/plastic_bottle_gallon", "ph/jug_01", "ph/multi_cleaner_bottle", "ph/long_life_food" };

        /// <summary>
        /// Camlı içecek dolabı (2 m): beyaz gövde, cam kapı, aydınlık iç, dört rafta şişeler.
        /// facing: kapının baktığı yön; (x, z) dolabın tabanının ortası.
        /// </summary>
        void Fridge(float x, float z, Vector3 facing, float width = 0.95f)
        {
            const float h = 2.0f, depth = 0.7f;
            var holder = new GameObject("IcecekDolabi").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(x, 0, z);
            holder.localRotation = Quaternion.LookRotation(facing);
            var body = Flat("DolapGovde", new Color(0.93f, 0.94f, 0.95f), 0.6f);
            var inner = Flat("DolapIc", new Color(0.97f, 0.98f, 1f), 0.2f);
            furniture.Add(Box("Govde", new Vector3(0, 0.06f, 0), new Vector3(width, 0.12f, depth), body, holder));
            Box("Arka", new Vector3(0, h * 0.5f, -depth * 0.5f + 0.02f), new Vector3(width, h, 0.04f), inner, holder);
            Box("SolYan", new Vector3(-width * 0.5f + 0.02f, h * 0.5f, 0), new Vector3(0.04f, h, depth), body, holder);
            Box("SagYan", new Vector3(width * 0.5f - 0.02f, h * 0.5f, 0), new Vector3(0.04f, h, depth), body, holder);
            Box("Tavan", new Vector3(0, h - 0.06f, 0), new Vector3(width, 0.12f, depth), body, holder);
            Box("Kapi", new Vector3(0, h * 0.5f, depth * 0.5f - 0.01f), new Vector3(width - 0.06f, h - 0.24f, 0.015f), Glass(), holder);
            Box("Kulp", new Vector3(width * 0.5f - 0.1f, h * 0.5f, depth * 0.5f + 0.02f), new Vector3(0.03f, 0.5f, 0.03f), Flat("Kulp", new Color(0.6f, 0.6f, 0.62f), 0.8f), holder);
            var shelfMat = Flat("DolapRaf", new Color(0.85f, 0.88f, 0.9f), 0.7f);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.14f + k * 0.44f;
                Box("Raf", new Vector3(0, y, 0), new Vector3(width - 0.08f, 0.02f, depth - 0.1f), shelfMat, holder);
                int n = 3;
                for (int i = 0; i < n; i++)
                {
                    var local = new Vector3(-width * 0.5f + 0.18f + i * (width - 0.36f) / (n - 1), 0, 0.05f);
                    var p = root.InverseTransformPoint(holder.TransformPoint(local));
                    var item = Place(Drinks[(k + i) % Drinks.Length], p.x, p.z, facing, y + 0.012f, 0.75f);
                    if (item != null) { furniture.Remove(item); if (WorldBounds(item).size.y > 0.4f) item.transform.localScale *= 0.4f / WorldBounds(item).size.y; }
                }
            }
            var light = new GameObject("DolapIsigi").AddComponent<Light>();
            light.transform.SetParent(holder, false);
            light.transform.localPosition = new Vector3(0, h - 0.25f, 0.1f);
            light.type = LightType.Point;
            light.range = 1.2f;
            light.intensity = 1.2f;
            light.color = new Color(0.85f, 0.93f, 1f);
            light.shadows = LightShadows.None;
        }

        /// <summary>Market reyon adları (tabela dokuları sahne kurulurken bunlardan üretilir; sonuncusu manav köşesi).</summary>
        public static readonly string[] AisleNames = { "GIDA", "TEMİZLİK", "KONSERVE", "İÇECEK", "MEYVE SEBZE" };

        /// <summary>Raf sırasının üstünde asılı reyon tabelası: çerçeve ve iki yüzünde yazı.</summary>
        void AisleSign(float x, float z, string text, Color color)
        {
            const float y = 2.95f, w = 1.7f, h = 0.36f;
            Box("ReyonTabela", new Vector3(x, y, z), new Vector3(w + 0.08f, h + 0.08f, 0.04f), Flat("ReyonCerceve", new Color(0.1f, 0.1f, 0.12f), 0.4f));
            var rod = Flat("ReyonAski", new Color(0.3f, 0.3f, 0.3f), 0.6f);
            Box("Aski", new Vector3(x - w * 0.4f, y + 0.4f, z), new Vector3(0.015f, 0.62f, 0.015f), rod);
            Box("Aski", new Vector3(x + w * 0.4f, y + 0.4f, z), new Vector3(0.015f, 0.62f, 0.015f), rod);
            var mat = lib.Sign(text);
            foreach (var side in new[] { -1f, 1f })
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(quad.GetComponent<Collider>());
                quad.name = "ReyonYazi";
                quad.transform.SetParent(root, false);
                quad.transform.localPosition = new Vector3(x, y, z + side * 0.022f);
                // Quad'ın görünen yüzü -Z'ye bakar: ön yüz için +Z ileri, arka yüz için -Z ileri.
                quad.transform.localRotation = Quaternion.LookRotation(new Vector3(0, 0, -side));
                quad.transform.localScale = new Vector3(w, h, 1f);
                var r = quad.GetComponent<Renderer>();
                if (mat != null) r.sharedMaterial = mat;
                else r.sharedMaterial = Flat("ReyonYedek", color, 0.3f);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Çerçeveli manzara tablosu (koyu ahşap çerçeve, beyaz paspartu).</summary>
        void Picture(bool back, float along, float y, int index)
        {
            if (lib.pictures == null || lib.pictures.Count == 0) return;
            const float w = 0.74f, h = 0.42f;
            var inward = back ? Front : Left;
            var basePos = back ? new Vector3(along, y, HalfD) : new Vector3(HalfW, y, along);
            Vector3 Size(float a, float hh, float depth) => (back ? Vector3.right : Vector3.forward) * a + Vector3.up * hh + (back ? Vector3.forward : Vector3.right) * depth;
            Box("TabloCerceve", basePos + inward * 0.02f, Size(w + 0.12f, h + 0.12f, 0.04f), Flat("TabloCerceve", new Color(0.18f, 0.12f, 0.08f), 0.5f));
            Box("Paspartu", basePos + inward * 0.042f, Size(w + 0.05f, h + 0.05f, 0.004f), Flat("Paspartu", new Color(0.95f, 0.94f, 0.9f), 0.2f));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "Tablo";
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = basePos + inward * 0.046f;
            quad.transform.localRotation = Quaternion.LookRotation(-inward);
            quad.transform.localScale = new Vector3(w, h, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = lib.pictures[Mathf.Abs(index) % lib.pictures.Count];
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Tavandan sarkan lamba: alt ucu 2,75 m'de.</summary>
        void HangingLamp(float x, float z)
        {
            var go = Place(style.lamp, x, z, Front, 2f);
            if (go == null) return;
            var b = WorldBounds(go);
            if (b.size.y > 1.1f) go.transform.localScale *= 1.1f / b.size.y;
            b = WorldBounds(go);
            go.transform.position += Vector3.up * (root.position.y + 2.75f - b.min.y);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            furniture.Remove(go);
        }

        /// <summary>
        /// Duvar süsleri ve lambalar: pencere aralarına saat ve tablolar, tavana lambalar, köşelere saksı bitkisi,
        /// yeme-içme dükkanlarının girişine kara tahta menü.
        /// </summary>
        void Decorate(string id)
        {
            // Arka duvarda pencereler arası boşluklar (pencere genişliği 1,8, aralık 2,6).
            var gaps = new List<float>();
            for (float x = -HalfW + 1.4f; x < HalfW - 1f; x += 2.6f) gaps.Add(x + 0.8f + 1.3f);
            gaps.RemoveAll(g => g > HalfW - 0.6f);
            int pic = rng.Next(100);
            bool clockWanted = D("saat"), clock = false;
            // Komşu dükkanlarında aplik yalnızca kafe ve fırında; oyuncu satın aldıysa her dükkanda.
            bool sconces = look != null ? look.Has("aplik") : (id == "kafe" || id == "firin");
            for (int i = 0; i < gaps.Count; i++)
            {
                if (clockWanted && !clock && i == gaps.Count / 2) { WallItem("ph/wall_clock", true, gaps[i], WindowY, 0.7f); clock = true; continue; }
                if (id == "eczane" && i == 0) continue;
                if (sconces && i % 2 == 0) WallItem("ph/industrial_wall_lamp", true, gaps[i], WindowY, 0.45f);
                else if (D("tablo")) Picture(true, gaps[i], WindowY, pic++);
            }
            if (clockWanted && !clock) WallItem("ph/wall_clock", true, gaps.Count > 0 ? gaps[0] : 0f, WindowY, 0.7f);
            // Sağ duvar: pencere araları tablolar.
            if (D("tablo"))
                for (float z = -HalfD + 1.4f; z < HalfD - 1f; z += 2.6f)
                {
                    float g = z + 0.8f + 1.3f;
                    if (g > HalfD - 0.6f) break;
                    Picture(false, g, WindowY, pic++);
                }
            // Saksı bitkileri: boş köşelere (satın alındıysa üç tane, komşuda bir tane).
            if (D("bitki"))
            {
                int want = look != null ? 3 : 1, placed = 0;
                foreach (var spot in new[] { new Vector2(HalfW - 0.45f, -HalfD + 1.3f), new Vector2(HalfW - 0.45f, 0f), new Vector2(-HalfW + 0.45f, 0.5f), new Vector2(HalfW - 0.45f, HalfD - 1.2f), new Vector2(-HalfW + 0.45f, -HalfD + 2.2f) })
                    if (placed < want && IsFree(spot.x, spot.y, 0.6f)) { Place("ph/potted_plant_04", spot.x, spot.y, Left, 0f, 2.4f); placed++; }
            }
            // Kilim: girişte, yürüme yolunda.
            if (D("hali")) Rug(new Vector3(0.2f, 0, -HalfD + 1.5f), new Vector2(Mathf.Min(3.2f, size.x - 2.4f), 1.7f));
            // Kara tahta menü: yeme-içme dükkanları, girişe yakın.
            if (D("menu") && (id == "kafe" || id == "firin" || id == "simit_bufesi" || id == "manav"))
                foreach (var spot in new[] { new Vector2(HalfW - 0.8f, -HalfD + 2.2f), new Vector2(-HalfW + 0.7f, -HalfD + 1.6f), new Vector2(0.5f - HalfW * 0.3f, -HalfD + 0.7f) })
                    if (IsFree(spot.x, spot.y, 0.7f)) { Place("ph/standing_chalkboard_01", spot.x, spot.y, Front + Left * 0.4f, 0f, 0.8f); break; }
            // Eczane: arka duvarda yeşil haç.
            if (id == "eczane" && gaps.Count > 0)
            {
                var green = Flat("EczaneHac", new Color(0.1f, 0.75f, 0.35f), 0.6f);
                float gx = gaps[0];
                Box("Hac", new Vector3(gx, WindowY, HalfD - 0.04f), new Vector3(0.18f, 0.55f, 0.06f), green);
                Box("Hac", new Vector3(gx, WindowY, HalfD - 0.04f), new Vector3(0.55f, 0.18f, 0.06f), green);
            }
        }

        static Material kilimMat;

        /// <summary>Yere serili kilim (doku kodla üretilir: kenar şeridi ve baklava desenleri).</summary>
        void Rug(Vector3 center, Vector2 sizeM)
        {
            if (kilimMat == null)
            {
                const int W = 256, H = 128;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Kilim" };
                var red = new Color(0.62f, 0.12f, 0.1f);
                var cream = new Color(0.93f, 0.86f, 0.7f);
                var navy = new Color(0.12f, 0.17f, 0.32f);
                var ochre = new Color(0.85f, 0.6f, 0.2f);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                    Color c;
                    if (edge < 6) c = navy;
                    else if (edge < 10) c = cream;
                    else if (edge < 18) c = ((x + y) / 6) % 2 == 0 ? ochre : navy;
                    else if (edge < 22) c = cream;
                    else
                    {
                        // Ortada baklavalar.
                        float cx = (x - 22) % 52 - 26f, cy = y - H * 0.5f;
                        float dd = Mathf.Abs(cx) + Mathf.Abs(cy) * 1.1f;
                        c = dd < 10 ? cream : dd < 16 ? ochre : dd < 22 ? navy : red;
                    }
                    // Hafif doku: ip lifi gibi.
                    float n = (Mathf.PerlinNoise(x * 0.35f, y * 0.9f) - 0.5f) * 0.08f;
                    px[y * W + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
                }
                tex.SetPixels(px);
                tex.Apply(true);
                kilimMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Kilim" };
                kilimMat.SetTexture("_BaseMap", tex);
                kilimMat.SetFloat("_Smoothness", 0.05f);
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "Kilim";
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = center + Vector3.up * 0.006f;
            quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            quad.transform.localScale = new Vector3(sizeM.x, sizeM.y, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = kilimMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Bu noktanın çevresinde mobilya ya da kişi yok mu?</summary>
        bool IsFree(float x, float z, float radius)
        {
            var p = new Vector3(x, 0, z);
            foreach (var f in furniture)
            {
                if (f == null) continue;
                var b = WorldBounds(f);
                var lb = new Bounds(root.InverseTransformPoint(b.center), b.size);
                lb.Expand(new Vector3(radius * 2, 10f, radius * 2));
                if (lb.Contains(p + Vector3.up * 0.5f)) return false;
            }
            foreach (var list in new[] { staff, customers, seated, waiting })
                foreach (var c in list)
                    if (c != null && Vector2.Distance(new Vector2(c.transform.localPosition.x, c.transform.localPosition.z), new Vector2(x, z)) < radius + 0.3f) return false;
            return true;
        }

        void Lights()
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / 4f)), nz = Mathf.Max(1, Mathf.RoundToInt(size.y / 4.5f));
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var go = new GameObject("TavanIsigi");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-HalfW + size.x * (i + 0.5f) / nx, 3.6f, -HalfD + size.y * (j + 0.5f) / nz);
                HangingLamp(go.transform.localPosition.x, go.transform.localPosition.z);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.9f, 0.78f);
                l.intensity = 4f;
                l.range = Mathf.Max(size.x, size.y) * 0.75f;
                l.shadows = LightShadows.None;
            }
        }

        void Customers(int count, float xMin, float xMax, float zMin, float zMax)
        {
            string[] people = { "ch/female-a", "ch/female-b", "ch/female-c", "ch/female-d", "ch/male-a", "ch/male-b", "ch/male-c", "ch/male-d" };
            for (int i = 0; i < count; i++)
            {
                float a = R(0, 360);
                string key = RandomRealPerson() ?? people[rng.Next(people.Length)];
                Place(key, R(xMin, xMax), R(zMin, zMax), new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad)));
            }
        }

        /// <summary>Kasa önünde sırada bekleyen müşteriler (yerinde durur, arada telefona bakar).</summary>
        void Queue(float x, float z, int count)
        {
            for (int i = 0; i < count; i++)
            {
                string key = RandomRealPerson();
                if (key == null) return;
                // İlki kasaya döner, arkadakiler sıranın önüne bakar.
                var facing = i == 0 ? Left : Front;
                var go = Place(key, x + R(-0.08f, 0.08f), z + i * 0.85f, facing);
                if (go == null) return;
                customers.Remove(go);
                waiting.Add(go);
            }
        }

        /// <summary>Sandalyede oturan müşteri (yalnızca gerçekçi karakterlerle; oturma animasyonu gerekir).</summary>
        void SeatedCustomer(float x, float z, Vector3 facing)
        {
            string key = RandomRealPerson();
            if (key == null) return;
            var go = Place(key, x - facing.x * 0.12f, z - facing.z * 0.12f, facing);
            if (go == null) return;
            customers.Remove(go);
            seated.Add(go);
        }

        // ------------------------------------------------------------ Gerçekçi eşya yardımcıları (Poly Haven, CC0)

        static readonly string[] Fruits = { "ph/food_apple_01", "ph/food_pears_asian_01", "ph/lemon", "ph/food_lime_01", "ph/food_pomegranate_01", "ph/food_kiwi_01", "ph/food_avocado_01" };
        static readonly string[] Vegetables = { "ph/yellow_onion", "ph/sweet_potato", "ph/food_avocado_01", "ph/lemon" };
        static readonly string[] Breads = { "ph/croissant", "ph/hamburger_buns", "ph/carrot_cake" };
        static readonly string[] Groceries = { "ph/long_life_food", "ph/russian_food_cans_01", "ph/multi_cleaner_bottle", "ph/plastic_bottle_gallon", "ph/jug_01", "ph/cardboard_box_01" };
        static readonly string[] Pharmacy = { "ph/medical_box", "ph/multi_cleaner_bottle", "ph/russian_food_cans_01" };
        static readonly string[] Meats = { "food/meat-raw", "food/meat-ribs", "food/meat-sausage", "food/whole-ham" };

        /// <summary>Raf katlarını bulur (yukarıdan aşağı ışın) ve her kata ürünleri dizer.</summary>
        void FillShelf(GameObject shelf, string[] items, float fill = 0.8f)
        {
            if (shelf == null) return;
            items = Items(items);
            fill *= stock;
            if (fill <= 0.01f) return;
            var rs = shelf.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var right = shelf.transform.right;
            var fwd = shelf.transform.forward;
            float halfW = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z)))) - 0.08f;
            // Raf katları: yukarı bakan yüzeylerin yükseklikleri (ağ köşelerinden, fizik gerektirmez).
            var counts = new Dictionary<int, int>();
            foreach (var mf in shelf.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var v = mesh.vertices;
                var n = mesh.normals;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < v.Length && i < n.Length; i++)
                {
                    if (m.MultiplyVector(n[i]).normalized.y < 0.95f) continue;
                    var wp = m.MultiplyPoint3x4(v[i]);
                    int key = Mathf.RoundToInt(wp.y * 20f);
                    counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
                }
            }
            var levels = new List<float>();
            foreach (var kv in counts)
            {
                float y = kv.Key / 20f;
                if (kv.Value >= 4 && y < b.max.y - 0.15f && y > root.position.y + 0.03f) levels.Add(y);
            }
            levels.Sort();
            float prev = -10f;
            foreach (float y in levels)
            {
                if (y - prev < 0.18f) continue;
                prev = y;
                for (float t = -halfW + 0.08f; t < halfW - 0.05f; t += 0f)
                {
                    if (rng.NextDouble() > fill) { t += 0.15f; continue; }
                    var key = items[rng.Next(items.Length)];
                    var e = lib.Get(key);
                    if (e == null) break;
                    var p = new Vector3(b.center.x, y, b.center.z) + right * t + fwd * R(-0.05f, 0.05f);
                    var lp = root.InverseTransformPoint(p);
                    var item = Place(key, lp.x, lp.z, -fwd, lp.y);
                    float w = ItemWidth(item);
                    if (t + w > halfW) { Object.Destroy(item); break; }
                    item.transform.position += right * (w * 0.5f);
                    t += w + 0.02f;
                }
            }
        }

        static float ItemWidth(GameObject go)
        {
            if (go == null) return 0.2f;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0.2f;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z) * 0.8f);
        }

        /// <summary>Kasa içi yığın: tepede gerçek meyveler, altı meyve dokulu dolgu (az üçgenle dolu görünüm).</summary>
        void ProduceCrate(float x, float z, float baseY, string fruit, Vector3 facing)
        {
            var crate = Place("ph/plastic_crate_02", x, z, facing, baseY);
            if (crate == null) return;
            if (productOverride != null) fruit = productOverride[Mathf.Abs(Mathf.RoundToInt(x * 7 + z * 13)) % productOverride.Length];
            if (stock <= 0.01f) return;
            var e = lib.Get(fruit);
            float top = baseY + 0.2f;
            if (e != null)
            {
                var src = e.prefab.GetComponentInChildren<Renderer>();
                if (src != null)
                {
                    var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(fill.GetComponent<Collider>());
                    fill.name = "Yigin";
                    fill.transform.SetParent(crate.transform, false);
                    fill.transform.localPosition = new Vector3(0, 0.1f, 0);
                    fill.transform.localScale = new Vector3(0.46f, 0.18f, 0.36f);
                    var m = new Material(src.sharedMaterial);
                    m.SetTextureScale("_BaseMap", new Vector2(4f, 3f));
                    fill.GetComponent<Renderer>().sharedMaterial = m;
                }
                var right = Vector3.Cross(Vector3.up, facing).normalized;
                for (int i = 0; i < 7; i++)
                {
                    var off = right * R(-0.18f, 0.18f) + facing.normalized * R(-0.13f, 0.13f);
                    var it = Place(fruit, x + off.x, z + off.z, Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward, top + R(-0.02f, 0.03f));
                }
            }
        }

        /// <summary>Kasa: tezgah (çelik masa) üstünde yazar kasa, arkasında çalışan.</summary>
        void Register(float x, float z, Vector3 facingCustomer)
        {
            Place("ph/metal_office_desk", x, z, facingCustomer, 0f, 0.75f);
            Place("ph/CashRegister_01", x, z + 0.05f * facingCustomer.z, facingCustomer, 0.6f);
            Place("mm/employee", x - facingCustomer.x * 0.9f, z - facingCustomer.z * 0.9f, facingCustomer);
            registerSpot = new Vector3(x, 0, z);
        }

        void Produce(float x, float z, float topY, string[] items, int count = 7)
        {
            items = Items(items);
            count = Mathf.RoundToInt(count * stock);
            for (int i = 0; i < count; i++)
                Place(items[rng.Next(items.Length)], x + R(-0.35f, 0.35f), z + R(-0.3f, 0.3f), Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward, topY);
        }

        void Plants()
        {
            Place("ph/planter_pot_clay", HalfW - 0.4f, -HalfD + 0.4f, Front, 0f, 1.6f);
            Place("furn/pottedPlant", -HalfW + 0.5f, -HalfD + 0.5f, Front);
        }

        // ------------------------------------------------------------ Düzenler

        void Market(int level, bool super)
        {
            // Arka duvar: içecek dolapları ve büyük raflar.
            float x = -HalfW + 0.6f;
            int fridges = super ? 4 : 2;
            for (int i = 0; i < fridges; i++, x += 1.05f) Fridge(x + 0.5f, HalfD - 0.45f, Front);
            for (; x < HalfW - 2.4f; x += 2.4f) FillShelf(Place("ph/steel_frame_shelves_03", x + 1.2f, HalfD - 0.45f, Front), Groceries);
            // Sağ duvar: raflar.
            for (float z = HalfD - 2.2f; z > -HalfD + 2.4f; z -= 1.2f) FillShelf(Place("ph/steel_frame_shelves_01", HalfW - 0.35f, z, Left), Groceries);
            // Orta reyonlar (sırt sırta raf sıraları).
            int rows = Mathf.Clamp(1 + level / 2 + (super ? 1 : 0), 1, super ? 4 : 3);
            for (int r = 0; r < rows; r++)
            {
                float z = HalfD - 2.6f - r * 2.3f;
                if (z < -HalfD + 3f) break;
                for (float rx = -HalfW + 4.2f; rx < HalfW - 2.6f; rx += 1.15f)
                {
                    FillShelf(Place("ph/steel_frame_shelves_01", rx + 0.55f, z + 0.27f, Back), Groceries);
                    FillShelf(Place("ph/steel_frame_shelves_01", rx + 0.55f, z - 0.27f, Front), Groceries);
                }
            }
            // Reyon tabelaları: her raf sırasının üstünde asılı.
            var aisles = AisleNames;
            for (int r = 0; r < rows; r++)
            {
                float z = HalfD - 2.6f - r * 2.3f;
                if (z < -HalfD + 3f) break;
                AisleSign(((-HalfW + 4.2f) + (HalfW - 2.6f)) * 0.5f + 0.3f, z, aisles[r % (aisles.Length - 1)], new Color(0.2f, 0.42f, 0.7f));
            }
            AisleSign(-HalfW + 1.6f, HalfD - 3f, "MEYVE SEBZE", new Color(0.3f, 0.6f, 0.3f));
            // Manav köşesi: masalar üstünde meyve-sebze kasaları.
            int tables = Mathf.Clamp(1 + level / 2, 1, 3) + (super ? 1 : 0);
            for (int i = 0; i < tables; i++)
            {
                float tz = HalfD - 2.5f - i * 1.3f;
                Place("ph/WoodenTable_01", -HalfW + 1.6f, tz, Front, 0f, 1.25f);
                for (int k = 0; k < 3; k++) ProduceCrate(-HalfW + 1.0f + k * 0.55f, tz, 0.69f, (k + i) % 2 == 0 ? Fruits[(k + i) % Fruits.Length] : Vegetables[(k + i) % Vegetables.Length], Front);
            }
            if (level >= 2 || super)
            {
                FillShelf(Place("ph/wooden_display_shelves_01", HalfW - 2.2f, -HalfD + 1.9f, Front), Breads);
            }
            int registers = 1 + level / 3 + (super ? 2 : 0);
            for (int i = 0; i < registers; i++)
            {
                // Kasalar arası 2,6 m: kasiyer, müşteri sırası ve sonraki kasiyer birbirine girmez.
                Register(-HalfW + 1.4f + i * 2.6f, -HalfD + 1.2f, Right);
                // Kasada sıra: önde ödeyen, arkasında bekleyenler.
                Queue(-HalfW + 1.4f + i * 2.6f + 0.85f, -HalfD + 1.2f, Mathf.Min(1 + level / 3 + (super ? 1 : 0), 3));
            }
            for (int i = 0; i < 4; i++) Place("ph/wicker_basket_02", -HalfW + 0.4f, -HalfD + 2.6f + i * 0.3f, Front, i * 0.12f);
            Plants();
            Customers(3 + level + (super ? 5 : 0), -HalfW + 3f, HalfW - 2f, -HalfD + 2.5f, HalfD - 2f);
        }

        void Manav(int level)
        {
            int cols = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int c = 0; c < cols; c++)
            {
                float tx = -HalfW + 1.6f + c * 2.0f;
                if (tx > HalfW - 2.2f) break;
                Place("ph/WoodenTable_01", tx, 0.3f, Front, 0f, 1.25f);
                for (int k = 0; k < 3; k++) ProduceCrate(tx - 0.6f + k * 0.6f, 0.3f, 0.69f, (c + k) % 2 == 0 ? Fruits[(c * 3 + k) % Fruits.Length] : Vegetables[(c * 3 + k) % Vegetables.Length], Front);
            }
            // Arka duvar: kademeli raflarda kasalar.
            for (float x = -HalfW + 0.8f; x < HalfW - 1f; x += 1.15f)
            {
                Place("ph/wooden_display_shelves_01", x + 0.55f, HalfD - 0.3f, Front);
                for (int s = 0; s < 2; s++) ProduceCrate(x + 0.55f, HalfD - 0.35f, 0.05f + s * 0.48f, s == 0 ? Vegetables[rng.Next(Vegetables.Length)] : Fruits[rng.Next(Fruits.Length)], Front);
            }
            for (int i = 0; i < 3; i++) Place("ph/wooden_crate_02", HalfW - 0.8f, 0.6f - i * 0.65f, Left);
            Register(HalfW - 1.6f, -HalfD + 1.3f, Left);
            Plants();
            Customers(1 + level, -HalfW + 1f, HalfW - 2.5f, -HalfD + 1f, -0.8f);
        }

        void Kasap(int level)
        {
            // Et vitrini: camlı, soğuk beyaz taban; seviye arttıkça uzar.
            float vitrineLength = Mathf.Min(size.x - 3.4f, 2.6f + level * 0.5f);
            Vitrine(-HalfW + 0.8f + vitrineLength * 0.5f, 0.2f, vitrineLength, Meats, new Color(0.92f, 0.92f, 0.9f));
            for (float x = -HalfW + 1.2f; x < HalfW - 1f; x += 2.0f) Place("ph/metal_office_desk", x + 1f, HalfD - 0.55f, Front, 0f, 0.9f);
            Place("ph/wooden_cutting_board", -HalfW + 2.2f, HalfD - 0.55f, Front, 0.72f, 1.3f);
            Place("food/meat-ribs", -HalfW + 2.2f, HalfD - 0.55f, Front, 0.77f);
            Place("food/whole-ham", HalfW - 2f, HalfD - 0.55f, Front, 0.72f);
            FillShelf(Place("ph/worn_metal_rack", HalfW - 0.5f, 0.2f, Left), new[] { "ph/cardboard_box_01", "ph/russian_food_cans_01" }, 0.7f);
            Place("mm/employee", -HalfW + 2.4f, 1.6f, Front);
            Register(HalfW - 1.8f, -HalfD + 1.3f, Left);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 3f, -HalfD + 0.8f, -1.2f);
        }

        void Firin(int level)
        {
            int displays = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < displays; i++) FillShelf(Place("ph/wooden_display_shelves_01", -HalfW + 1.2f + i * 1.2f, HalfD - 0.3f, Front), Breads, 0.95f);
            FillShelf(Place("ph/painted_wooden_shelves", HalfW - 0.5f, HalfD - 0.3f, Front), Breads, 0.95f);
            // Tezgah: sepetlerde ekmek.
            for (int i = 0; i < 3; i++)
            {
                float x = -HalfW + 1.5f + i * 1.2f;
                // Yükseklikler modelden ölçülür: sepet masanın, ekmekler sepetin üstünde.
                float top = TopY(Place("ph/WoodenTable_01", x, -0.2f, Front, 0f, 0.9f));
                float basketTop = TopY(Place("ph/wicker_basket_02", x, -0.2f, Front, top));
                Produce(x, -0.2f, Mathf.Lerp(top, basketTop, 0.6f), new[] { "ph/hamburger_buns", "ph/croissant" }, 3);
            }
            // Camlı ekmek vitrini; arkasında satıcı.
            Vitrine(0.4f, 0.9f, 2.4f, new[] { "ph/croissant", "ph/hamburger_buns", "ph/carrot_cake" });
            Place("mm/employee", 0.4f, 1.75f, Front);
            Register(HalfW - 1.6f, -HalfD + 1.3f, Left);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 3f, -HalfD + 0.8f, -1.5f);
        }

        void Simit(int level)
        {
            // Camlı simit vitrini; yanında sepetlerde ekmek.
            Vitrine(-0.5f, -0.2f, 2.0f, new[] { "ph/hamburger_buns", "ph/croissant" });
            float top = TopY(Place("ph/WoodenTable_01", HalfW - 1.2f, -0.3f, Left, 0f, 0.8f));
            float basketTop = TopY(Place("ph/wicker_basket_02", HalfW - 1.2f, -0.3f, Front, top));
            Produce(HalfW - 1.2f, -0.3f, Mathf.Lerp(top, basketTop, 0.6f), new[] { "ph/hamburger_buns" }, 3);
            Place("ph/CoffeeCart_01", HalfW - 1.4f, HalfD - 0.7f, Front);
            FillShelf(Place("ph/painted_wooden_shelves", -HalfW + 0.5f, HalfD - 0.3f, Front), Breads, 0.9f);
            Place("mm/employee", -0.5f, 0.75f, Front);
            if (level >= 2) Place("ph/metal_stool_01", -1.6f, -1.6f, Back);
            if (level >= 3) Place("ph/metal_stool_01", 0.6f, -1.6f, Back);
            registerSpot = new Vector3(-0.5f, 0, -0.9f);
            Customers(1 + level / 2, -HalfW + 0.8f, HalfW - 0.8f, -HalfD + 0.6f, -1.6f);
        }

        void Bakkal(int level)
        {
            for (float x = -HalfW + 0.6f; x < HalfW - 2f; x += 2.4f) FillShelf(Place("ph/steel_frame_shelves_03", x + 1.2f, HalfD - 0.45f, Front), Groceries);
            for (float z = HalfD - 2f; z > -HalfD + 2f; z -= 1.2f) FillShelf(Place("ph/steel_frame_shelves_01", HalfW - 0.35f, z, Left), Groceries);
            if (level >= 2)
                for (float x = -HalfW + 2.5f; x < HalfW - 2.5f; x += 1.15f) FillShelf(Place("ph/steel_frame_shelves_01", x + 0.55f, 0f, Front), Groceries);
            // Sağ duvarın önünde camlı içecek dolabı (kapısı kameraya dönük).
            Fridge(HalfW - 0.42f, -HalfD + 1.0f, Left);
            Register(-HalfW + 1.6f, -HalfD + 1.3f, Right);
            Customers(1 + level / 2, -HalfW + 3f, HalfW - 2f, -HalfD + 1f, -0.8f);
        }

        void Kafe(int level)
        {
            Place("ph/CoffeeCart_01", -HalfW + 1.4f, HalfD - 0.7f, Front);
            // Bar tezgahı: ahşap gövde, koyu üst tabla, önünde tabureler, üstünde pastalar.
            float counterTop = Counter(0.9f, HalfD - 1.0f, 3.2f, 0.7f, new Color(0.3f, 0.19f, 0.12f), new Color(0.12f, 0.1f, 0.09f));
            for (int i = 0; i < 3; i++) Place("ph/carrot_cake", -0.2f + i * 1.1f, HalfD - 1.0f, Front, counterTop);
            Produce(1.8f, HalfD - 1.0f, counterTop, new[] { "ph/croissant" }, 3);
            for (int i = 0; i < 3; i++) Place("ph/metal_stool_01", -0.3f + i * 1.2f, HalfD - 1.75f, Back);
            FillShelf(Place("ph/painted_wooden_shelves", HalfW - 0.5f, HalfD - 0.3f, Front), Breads, 0.9f);
            int tables = Mathf.Clamp(2 + level / 2, 2, 5);
            for (int i = 0; i < tables; i++)
            {
                float x = -HalfW + 1.8f + (i % 3) * 2.4f, z = -0.6f - (i / 3) * 2.0f;
                float tableTop = TopY(Place("ph/round_wooden_table_02", x, z, Front));
                Place("ph/gallinera_chair", x - 0.75f, z, Right);
                Place("ph/gallinera_chair", x + 0.75f, z, Left);
                Place("ph/carrot_cake", x, z, Front, tableTop);
                // Bazı masalarda oturup sohbet eden müşteriler.
                if (rng.NextDouble() < 0.6) SeatedCustomer(x - 0.75f, z, Right);
                if (rng.NextDouble() < 0.4) SeatedCustomer(x + 0.75f, z, Left);
            }
            Plants();
            Place("mm/employee", 0.9f, HalfD - 0.35f, Front);
            registerSpot = new Vector3(2.1f, 0, HalfD - 1.8f);
            Customers(1 + level, -HalfW + 1f, HalfW - 1f, -HalfD + 0.8f, -HalfD + 1.6f);
        }

        void Eczane(int level)
        {
            for (float x = -HalfW + 0.5f; x < HalfW - 1.3f; x += 1.4f) FillShelf(Place("ph/wooden_bookshelf_worn", x + 0.7f, HalfD - 0.35f, Front), Pharmacy, 0.9f);
            for (float x = -HalfW + 1.5f; x < HalfW - 1.5f; x += 2.0f) Place("ph/metal_office_desk", x + 1f, 0.6f, Front, 0f, 0.95f);
            Place("ph/CashRegister_01", 0f, 0.6f, Front, 0.75f);
            registerSpot = new Vector3(0f, 0, 0.2f);
            Place("mm/employee", 0f, 1.6f, Front);
            if (level >= 2) FillShelf(Place("ph/painted_wooden_shelves", -HalfW + 0.4f, -1f, Right), Pharmacy, 0.9f);
            Plants();
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 1f, -HalfD + 0.8f, -0.5f);
        }

        void Berber(int level)
        {
            int chairs = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < chairs; i++)
            {
                float x = -HalfW + 1.3f + i * 1.6f;
                Place("ph/ornate_mirror_01", x, HalfD - 0.05f, Front, 1.2f, 1.2f);
                Place("ph/BarberShopChair_01", x, HalfD - 1.4f, Back);
                FillShelf(Place("ph/painted_wooden_shelves", x + 0.8f, HalfD - 0.25f, Front, 0f, 0.8f), new[] { "ph/multi_cleaner_bottle", "ph/jug_01" }, 0.7f);
            }
            for (int i = 0; i < 3; i++) Place("ph/ArmChair_01", HalfW - 0.6f, -HalfD + 1f + i * 1.0f, Left);
            Place("mm/employee", -HalfW + 2f, HalfD - 2.4f, Back);
            Plants();
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 2f, -HalfD + 0.8f, 0f);
        }

        /// <summary>Belediye: vezne, ofis masaları, dosya rafları, bekleme koltukları.</summary>
        void Belediye(int level)
        {
            for (float x = -HalfW + 0.5f; x < HalfW - 1.3f; x += 1.4f) FillShelf(Place("ph/wooden_bookshelf_worn", x + 0.7f, HalfD - 0.35f, Front), new[] { "ph/cardboard_box_01", "ph/medical_box" }, 0.7f);
            for (int i = 0; i < 3; i++)
            {
                float x = -HalfW + 2f + i * 2.8f;
                Place("ph/metal_office_desk", x, HalfD - 2.1f, Front);
                Place("ph/SchoolChair_01", x, HalfD - 1.4f, Front);
                Place("mm/employee", x, HalfD - 1.3f, Front);
            }
            for (float x = -HalfW + 1.5f; x < HalfW - 2.5f; x += 2.0f) Place("ph/metal_office_desk", x + 1f, 0f, Front, 0f, 1f);
            for (int i = 0; i < 4; i++) Place("ph/modern_arm_chair_01", -HalfW + 1.4f + i * 1.1f, -HalfD + 1.1f, Back);
            Plants();
            Customers(3, -HalfW + 1f, HalfW - 2f, -HalfD + 1.8f, -0.8f);
        }
    }
}
