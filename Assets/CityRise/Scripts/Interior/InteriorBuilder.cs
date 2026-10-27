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
        public static GameObject Build(InteriorLibrary lib, string businessId, int level, Material signMaterial, Vector3 origin)
        {
            var go = new GameObject("IcGorunum_" + businessId);
            go.transform.position = origin;
            var size = RoomSize(businessId, level);
            var b = new InteriorBuilder(lib, go.transform, size, businessId.GetHashCode() ^ level);
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
            b.Lights();
            b.Animate();
            return go;
        }

        // ------------------------------------------------------------ Yardımcılar

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        readonly List<GameObject> furniture = new List<GameObject>();
        readonly List<GameObject> customers = new List<GameObject>();
        readonly List<GameObject> staff = new List<GameObject>();
        Vector3? registerSpot;

        GameObject Place(string key, float x, float z, Vector3 facing, float y = 0f, float extraScale = 1f)
        {
            var e = lib.Get(key);
            if (e == null) return null;
            bool person = key.StartsWith("ch/") || key == "mm/employee";
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y + (key.StartsWith("ch/") ? 0f : ModelFrontYaw);
            // Kişiler bir taşıyıcı nesnenin içinde: taşıyıcı yürür, model hafifçe sallanır.
            var holder = person ? new GameObject(key.StartsWith("ch/") ? "Musteri" : "Calisan").transform : null;
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
                go.transform.localRotation = Quaternion.Euler(0, key.StartsWith("ch/") ? 0f : ModelFrontYaw, 0);
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
                (key.StartsWith("ch/") ? customers : staff).Add(holder.gameObject);
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
        }

        /// <summary>
        /// Kutu. UV'leri metre cinsindendir; böylece paylaşılan malzemenin dokusu (tiling = 1 / doku metresi)
        /// her boyuttaki duvar ve zeminde gerçek ölçekte görünür, malzeme kopyalamaya gerek kalmaz.
        /// </summary>
        GameObject Box(string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.AddComponent<MeshFilter>().sharedMesh = MetricBox(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Mesh MetricBox(Vector3 size)
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
            var floor = Box("Zemin", new Vector3(0, -0.05f, 0), new Vector3(w, 0.1f, d), lib.floorTiles);
            floor.name = "KaroZemin";

            // Arka duvarlar (yüksek, pencereli), ön duvarlar (alçak kesik).
            float t = 0.22f;
            Box("ArkaDuvar", new Vector3(0, WallHeight * 0.5f, HalfD + t * 0.5f), new Vector3(w + 2 * t, WallHeight, t), lib.wallInside);
            Box("SagDuvar", new Vector3(HalfW + t * 0.5f, WallHeight * 0.5f, 0), new Vector3(t, WallHeight, d), lib.wallInside);
            Box("OnDuvar", new Vector3(0, 0.3f, -HalfD - t * 0.5f), new Vector3(w + 2 * t, 0.6f, t), lib.wallBrick);
            Box("SolDuvar", new Vector3(-HalfW - t * 0.5f, 0.3f, 0), new Vector3(t, 0.6f, d), lib.wallBrick);
            // Duvar üstlerinde koyu kesit çizgisi (görseldeki gibi).
            Box("ArkaKesit", new Vector3(0, WallHeight + 0.03f, HalfD + t * 0.5f), new Vector3(w + 2 * t, 0.06f, t + 0.02f), lib.wallCap);
            Box("SagKesit", new Vector3(HalfW + t * 0.5f, WallHeight + 0.03f, 0), new Vector3(t + 0.02f, 0.06f, d + 2 * t), lib.wallCap);
            Box("OnKesit", new Vector3(0, 0.63f, -HalfD - t * 0.5f), new Vector3(w + 2 * t, 0.06f, t + 0.02f), lib.wallCap);
            Box("SolKesit", new Vector3(-HalfW - t * 0.5f, 0.63f, 0), new Vector3(t + 0.02f, 0.06f, d + 2 * t), lib.wallCap);

            // Pencere şeritleri.
            for (float x = -HalfW + 1.4f; x < HalfW - 1f; x += 2.6f)
                Box("Pencere", new Vector3(x + 0.8f, 2.45f, HalfD - 0.01f), new Vector3(1.8f, 0.9f, 0.02f), lib.glass);
            for (float z = -HalfD + 1.4f; z < HalfD - 1f; z += 2.6f)
                Box("Pencere", new Vector3(HalfW - 0.01f, 2.45f, z + 0.8f), new Vector3(0.02f, 0.9f, 1.8f), lib.glass);

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

        void Lights()
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / 6f)), nz = Mathf.Max(1, Mathf.RoundToInt(size.y / 6f));
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var go = new GameObject("TavanIsigi");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-HalfW + size.x * (i + 0.5f) / nx, 3.6f, -HalfD + size.y * (j + 0.5f) / nz);
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
                Place(people[rng.Next(people.Length)], R(xMin, xMax), R(zMin, zMax), new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad)));
            }
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
            for (int i = 0; i < fridges; i++, x += 1.05f) Place("mm/freezers-standing", x + 0.5f, HalfD - 0.55f, Front);
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
            for (int i = 0; i < registers; i++) Register(-HalfW + 1.4f + i * 1.9f, -HalfD + 1.2f, Right);
            for (int i = 0; i < 4; i++) Place("ph/wicker_basket_02", -HalfW + 0.4f, -HalfD + 2.6f + i * 0.3f, Front, i * 0.12f);
            Plants();
            Customers(2 + level + (super ? 3 : 0), -HalfW + 3f, HalfW - 2f, -HalfD + 2.5f, HalfD - 2f);
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
            int counters = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < counters; i++)
            {
                float x = -HalfW + 1.5f + i * 1.7f;
                Place("mm/freezer", x, 0.2f, Front);
                Produce(x, 0.2f, 0.72f, Meats, 5);
            }
            for (float x = -HalfW + 1.2f; x < HalfW - 1f; x += 2.0f) Place("ph/metal_office_desk", x + 1f, HalfD - 0.55f, Front, 0f, 0.9f);
            Place("ph/wooden_cutting_board", -HalfW + 2.2f, HalfD - 0.55f, Front, 0.72f, 1.3f);
            Place("food/meat-ribs", -HalfW + 2.2f, HalfD - 0.55f, Front, 0.77f);
            Place("food/whole-ham", HalfW - 2f, HalfD - 0.55f, Front, 0.72f);
            Place("ph/worn_metal_rack", HalfW - 0.5f, 0.2f, Left);
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
                Place("ph/WoodenTable_01", x, -0.2f, Front, 0f, 0.65f);
                Place("ph/wicker_basket_02", x, -0.2f, Front, 0.69f);
                Produce(x, -0.2f, 0.78f, new[] { "ph/hamburger_buns", "ph/croissant" }, 3);
            }
            Place("mm/employee", 0f, 1.2f, Front);
            Register(HalfW - 1.6f, -HalfD + 1.3f, Left);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 3f, -HalfD + 0.8f, -1.5f);
        }

        void Simit(int level)
        {
            Place("ph/WoodenTable_01", -0.6f, -0.4f, Front, 0f, 1.1f);
            for (int i = 0; i < 3; i++)
            {
                Place("ph/wicker_basket_02", -1.4f + i * 0.6f, -0.4f, Front, 0.6f);
                Produce(-1.4f + i * 0.6f, -0.4f, 0.69f, new[] { "ph/hamburger_buns", "ph/croissant" }, 3);
            }
            Place("ph/CoffeeCart_01", HalfW - 1.4f, HalfD - 0.7f, Front);
            FillShelf(Place("ph/painted_wooden_shelves", -HalfW + 0.5f, HalfD - 0.3f, Front), Breads, 0.9f);
            Place("mm/employee", -0.4f, 0.6f, Front);
            if (level >= 2) Place("ph/metal_stool_01", -1f, -1.4f, Back);
            if (level >= 3) Place("ph/metal_stool_01", 0f, -1.4f, Back);
            registerSpot = new Vector3(-0.6f, 0, -0.4f);
            Customers(1 + level / 2, -HalfW + 0.8f, HalfW - 0.8f, -HalfD + 0.6f, -1.6f);
        }

        void Bakkal(int level)
        {
            for (float x = -HalfW + 0.6f; x < HalfW - 2f; x += 2.4f) FillShelf(Place("ph/steel_frame_shelves_03", x + 1.2f, HalfD - 0.45f, Front), Groceries);
            for (float z = HalfD - 2f; z > -HalfD + 2f; z -= 1.2f) FillShelf(Place("ph/steel_frame_shelves_01", HalfW - 0.35f, z, Left), Groceries);
            if (level >= 2)
                for (float x = -HalfW + 2.5f; x < HalfW - 2.5f; x += 1.15f) FillShelf(Place("ph/steel_frame_shelves_01", x + 0.55f, 0f, Front), Groceries);
            Place("mm/freezer", -HalfW + 1.2f, -0.6f, Front);
            Produce(-HalfW + 1.2f, -0.6f, 0.72f, new[] { "ph/plastic_bottle_gallon", "ph/multi_cleaner_bottle" }, 4);
            Register(-HalfW + 1.6f, -HalfD + 1.3f, Right);
            Customers(1 + level / 2, -HalfW + 3f, HalfW - 2f, -HalfD + 1f, -0.8f);
        }

        void Kafe(int level)
        {
            Place("ph/CoffeeCart_01", -HalfW + 1.4f, HalfD - 0.7f, Front);
            Place("ph/WoodenTable_01", 0.5f, HalfD - 0.6f, Front, 0f, 1.3f);
            Produce(0.5f, HalfD - 0.6f, 0.71f, new[] { "ph/carrot_cake", "ph/croissant" }, 4);
            FillShelf(Place("ph/painted_wooden_shelves", HalfW - 0.5f, HalfD - 0.3f, Front), Breads, 0.9f);
            int tables = Mathf.Clamp(2 + level / 2, 2, 5);
            for (int i = 0; i < tables; i++)
            {
                float x = -HalfW + 1.8f + (i % 3) * 2.4f, z = -0.6f - (i / 3) * 2.0f;
                Place("ph/round_wooden_table_02", x, z, Front);
                Place("ph/gallinera_chair", x - 0.75f, z, Right);
                Place("ph/gallinera_chair", x + 0.75f, z, Left);
                Place("ph/carrot_cake", x, z, Front, 0.75f);
            }
            Plants();
            Place("mm/employee", -HalfW + 2.6f, HalfD - 1.4f, Front);
            registerSpot = new Vector3(0.5f, 0, HalfD - 1.2f);
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
