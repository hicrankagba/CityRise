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
            if (key == "mm/cash-register" && registerSpot == null) registerSpot = new Vector3(x, 0, z);
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

        /// <summary>Teşhir tezgahının üstüne karışık meyve-sebze.</summary>
        void Produce(float x, float z, float topY, string[] items, int count = 7)
        {
            for (int i = 0; i < count; i++)
                Place(items[rng.Next(items.Length)], x + R(-0.45f, 0.45f), z + R(-0.4f, 0.4f), Vector3.forward * (rng.Next(2) == 0 ? 1 : -1), topY + R(0f, 0.08f));
        }

        void Register(float x, float z, Vector3 facingCustomer)
        {
            Place("mm/cash-register", x, z, facingCustomer);
            Place("mm/employee", x - facingCustomer.x * 1.2f, z - facingCustomer.z * 1.2f, facingCustomer);
        }

        static readonly string[] Fruits = { "food/apple", "food/orange", "food/banana", "food/pear", "food/lemon", "food/grapes", "food/pineapple", "food/watermelon" };
        static readonly string[] Vegetables = { "food/tomato", "food/paprika", "food/eggplant", "food/carrot", "food/cabbage", "food/broccoli", "food/onion", "food/corn", "food/pumpkin-basic" };
        static readonly string[] Breads = { "food/loaf", "food/loaf-round", "food/loaf-baguette", "food/croissant", "food/muffin", "food/donut" };
        static readonly string[] Meats = { "food/meat-raw", "food/meat-ribs", "food/meat-sausage", "food/whole-ham", "food/fish" };

        // ------------------------------------------------------------ Düzenler

        void Market(int level, bool super)
        {
            // Arka duvar boyunca soğutucu dolaplar.
            for (float x = -HalfW + 1.2f; x < HalfW - 1.5f; x += 2.05f) Place("mm/freezers-standing", x + 1f, HalfD - 0.6f, Front);
            // Sağ duvar boyunca raflar.
            for (float z = -HalfD + 2.2f; z < HalfD - 1.8f; z += 1.65f) Place("mm/shelf-boxes", HalfW - 0.75f, z, Left);

            // Orta reyonlar: seviye arttıkça çoğalır.
            int rows = Mathf.Clamp(1 + level / 2 + (super ? 1 : 0), 1, super ? 5 : 3);
            float zStart = HalfD - 2.6f;
            for (int r = 0; r < rows; r++)
            {
                float z = zStart - r * 2.5f;
                if (z < -HalfD + 3.2f) break;
                float xStart = -HalfW + (super ? 4.5f : 4.2f), xEnd = HalfW - 2.4f;
                Place("mm/shelf-end", xStart - 0.6f, z, Left);
                for (float x = xStart; x < xEnd - 0.8f; x += 1.62f) Place(r % 2 == 0 ? "mm/shelf-boxes" : "mm/shelf-bags", x + 0.8f, z, Front);
            }

            // Manav köşesi.
            int stands = Mathf.Clamp(2 + level / 2, 2, 4) + (super ? 2 : 0);
            for (int i = 0; i < stands; i++)
            {
                float x = -HalfW + 1.4f + (i % 2) * 1.5f, z = HalfD - 2.4f - (i / 2) * 1.6f;
                Place("mm/display-fruit", x, z, Front);
                Produce(x, z, 1.02f, i % 2 == 0 ? Fruits : Vegetables);
            }
            // Fırın köşesi.
            if (level >= 2 || super)
            {
                Place("mm/display-bread", HalfW - 2.2f, -HalfD + 2.2f, Front);
                Produce(HalfW - 2.2f, -HalfD + 2.2f, 1f, Breads, 5);
            }
            if (super)
            {
                for (int i = 0; i < 2; i++)
                {
                    Place("mm/freezer", HalfW - 4.4f - i * 1.7f, -HalfD + 2.2f, Front);
                    Produce(HalfW - 4.4f - i * 1.7f, -HalfD + 2.2f, 0.72f, Meats, 4);
                }
            }

            // Kasalar ve kasiyerler.
            int registers = 1 + level / 3 + (super ? 2 : 0);
            for (int i = 0; i < registers; i++) Register(-HalfW + 1.6f + i * 1.9f, -HalfD + 1.3f, Right);
            for (int i = 0; i < 3; i++) Place("mm/shopping-cart", -HalfW + 0.6f, -HalfD + 2.6f + i * 0.55f, Right);
            Place("mm/shopping-basket", -HalfW + 0.6f, -HalfD + 4.4f, Front);
            Place("furn/pottedPlant", HalfW - 0.6f, -HalfD + 0.6f, Front);
            Customers(2 + level + (super ? 3 : 0), -HalfW + 3f, HalfW - 2f, -HalfD + 2.5f, HalfD - 2f);
        }

        void Manav(int level)
        {
            int cols = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < 2; r++)
            {
                float x = -HalfW + 1.8f + c * 1.6f, z = HalfD - 1.8f - r * 1.7f;
                Place("mm/display-fruit", x, z, Front);
                Produce(x, z, 1.02f, (c + r) % 2 == 0 ? Fruits : Vegetables, 8);
            }
            // Arka duvarda sebze kasaları.
            for (float x = -HalfW + 0.8f; x < HalfW - 0.8f; x += 1.0f)
            {
                Place("furn/cardboardBoxOpen", x, HalfD - 0.45f, Front);
                Produce(x, HalfD - 0.45f, 0.62f, Vegetables, 3);
            }
            Register(HalfW - 1.6f, -HalfD + 1.4f, Left);
            Place("mm/shopping-basket", -HalfW + 0.6f, -HalfD + 0.8f, Front);
            Customers(1 + level, -HalfW + 1f, HalfW - 2.5f, -HalfD + 1f, 0f);
        }

        void Kasap(int level)
        {
            // Et vitrini: soğutmalı tezgahlar.
            int counters = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < counters; i++)
            {
                float x = -HalfW + 1.5f + i * 1.7f;
                Place("mm/freezer", x, 0.2f, Front);
                Produce(x, 0.2f, 0.72f, Meats, 5);
            }
            // Arka tezgah: kesim tahtası, bıçak, asılı/duran but.
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.0f) Place("furn/kitchenCabinet", x, HalfD - 0.55f, Front);
            Place("food/cutting-board", -HalfW + 2f, HalfD - 0.55f, Front, 0.98f);
            Place("food/cooking-knife-chopping", -HalfW + 2f, HalfD - 0.55f, Front, 1.02f);
            Place("food/whole-ham", HalfW - 2f, HalfD - 0.55f, Front, 0.98f);
            Place("food/meat-ribs", HalfW - 3f, HalfD - 0.55f, Front, 0.98f);
            Place("mm/employee", -HalfW + 2.4f, 1.6f, Front);
            if (level >= 3) Place("furn/kitchenFridge", HalfW - 0.7f, 0.5f, Left);
            Register(HalfW - 1.8f, -HalfD + 1.3f, Left);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 3f, -HalfD + 0.8f, -1.2f);
        }

        void Firin(int level)
        {
            int displays = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < displays; i++)
            {
                float x = -HalfW + 1.5f + i * 1.6f;
                Place("mm/display-bread", x, -0.2f, Front);
                Produce(x, -0.2f, 1.0f, Breads, 5);
            }
            // Arkada taş fırınlar ve ekmek rafları.
            for (int i = 0; i < 2 + level / 3; i++) Place("furn/kitchenStove", -HalfW + 1f + i * 1.05f, HalfD - 0.55f, Front);
            for (int i = 0; i < 2; i++)
            {
                float x = HalfW - 1.2f - i * 1.0f;
                Place("furn/bookcaseOpenLow", x, HalfD - 0.35f, Front);
                Produce(x, HalfD - 0.35f, 0.92f, Breads, 3);
            }
            Place("mm/employee", 0f, 1.4f, Front);
            Register(HalfW - 1.6f, -HalfD + 1.3f, Left);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 3f, -HalfD + 0.8f, -1.5f);
        }

        void Simit(int level)
        {
            Place("furn/kitchenBar", -0.5f, -0.4f, Front);
            Place("furn/kitchenBarEnd", 0.5f, -0.4f, Front);
            Place("mm/display-bread", -HalfW + 1.2f, -0.4f, Front);
            Produce(-HalfW + 1.2f, -0.4f, 1.0f, new[] { "food/donut", "food/loaf-round", "food/croissant" }, 7);
            Place("furn/kitchenCoffeeMachine", -0.5f, -0.4f, Front, 0.98f);
            for (int i = 0; i < 3; i++) Place("food/cup-saucer", 0.2f + i * 0.25f, -0.4f, Front, 0.98f);
            Place("furn/kitchenStove", HalfW - 1f, HalfD - 0.55f, Front);
            Place("mm/employee", 0f, 0.8f, Front);
            if (level >= 2) Place("furn/stoolBar", -0.8f, -1.4f, Back);
            if (level >= 3) Place("furn/stoolBar", 0.3f, -1.4f, Back);
            Customers(1 + level / 2, -HalfW + 0.8f, HalfW - 0.8f, -HalfD + 0.6f, -1.6f);
        }

        void Bakkal(int level)
        {
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.62f) Place("mm/shelf-boxes", x + 0.4f, HalfD - 0.75f, Front);
            for (float z = -HalfD + 2f; z < HalfD - 2f; z += 1.62f) Place("mm/shelf-bags", HalfW - 0.75f, z, Left);
            if (level >= 2)
                for (float x = -HalfW + 2f; x < HalfW - 3f; x += 1.62f) Place("mm/shelf-boxes", x + 0.4f, 0f, Front);
            Place("mm/freezer", -HalfW + 1.2f, -0.5f, Front);
            Produce(-HalfW + 1.2f, -0.5f, 0.72f, new[] { "food/soda-can", "food/soda", "food/carton" }, 5);
            Register(-HalfW + 1.6f, -HalfD + 1.3f, Right);
            Customers(1 + level / 2, -HalfW + 3f, HalfW - 2f, -HalfD + 1f, -0.8f);
        }

        void Kafe(int level)
        {
            for (float x = -HalfW + 1f; x < HalfW - 1.5f; x += 1.0f) Place("furn/kitchenBar", x, HalfD - 0.55f, Front);
            Place("furn/kitchenCoffeeMachine", -HalfW + 1.2f, HalfD - 0.55f, Front, 0.98f);
            for (int i = 0; i < 4; i++) Place("food/cup-coffee", -HalfW + 2f + i * 0.35f, HalfD - 0.55f, Front, 0.98f);
            Place("mm/display-bread", HalfW - 1.2f, HalfD - 1f, Front);
            Produce(HalfW - 1.2f, HalfD - 1f, 1f, new[] { "food/cake", "food/cupcake", "food/croissant", "food/muffin" }, 4);
            int tables = Mathf.Clamp(2 + level / 2, 2, 5);
            for (int i = 0; i < tables; i++)
            {
                float x = -HalfW + 2f + (i % 3) * 2.6f, z = -0.4f - (i / 3) * 2.2f;
                Place("furn/tableRound", x, z, Front);
                Place("furn/chair", x - 0.85f, z, Right);
                Place("furn/chair", x + 0.85f, z, Left);
                Place("food/cup-coffee", x, z, Front, 0.82f);
            }
            Place("furn/pottedPlant", -HalfW + 0.5f, -HalfD + 0.5f, Front);
            Place("mm/employee", 0f, HalfD - 1.5f, Front);
            Customers(1 + level, -HalfW + 1f, HalfW - 1f, -HalfD + 0.8f, -HalfD + 1.6f);
        }

        void Eczane(int level)
        {
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.0f) Place("furn/bookcaseOpen", x, HalfD - 0.35f, Front);
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.0f)
                for (int s = 0; s < 3; s++) Produce(x, HalfD - 0.35f, 0.45f + s * 0.55f, new[] { "food/carton", "food/soda-can", "food/bottle-oil" }, 2);
            for (float x = -HalfW + 1.5f; x < HalfW - 1.5f; x += 1.0f) Place("furn/kitchenBar", x, 0.6f, Front);
            Place("mm/cash-register", 0f, 0.6f, Front, 0.98f);
            Place("mm/employee", 0f, 1.6f, Front);
            if (level >= 2) Place("mm/shelf-boxes", -HalfW + 0.8f, -1f, Right);
            Place("furn/pottedPlant", HalfW - 0.5f, -HalfD + 0.5f, Front);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 1f, -HalfD + 0.8f, -0.5f);
        }

        /// <summary>Belediye: vezne tezgahı, bekleme sandalyeleri, arkada masalı ofis ve dolaplar.</summary>
        void Belediye(int level)
        {
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.0f) Place("furn/bookcaseOpen", x, HalfD - 0.35f, Front);
            for (float x = -HalfW + 1f; x < HalfW - 0.8f; x += 1.0f)
                for (int s = 0; s < 3; s++) Produce(x, HalfD - 0.35f, 0.45f + s * 0.55f, new[] { "furn/cardboardBoxClosed" }, 1);
            for (int i = 0; i < 3; i++)
            {
                float x = -HalfW + 2f + i * 2.6f;
                Place("furn/desk", x, HalfD - 2.2f, Front);
                Place("furn/chairModernCushion", x, HalfD - 1.5f, Front);
                Place("mm/employee", x, HalfD - 1.3f, Front);
            }
            // Vezne.
            for (float x = -HalfW + 1.5f; x < HalfW - 2.5f; x += 1.0f) Place("furn/kitchenBar", x, 0f, Front);
            for (int i = 0; i < 6; i++) Place("furn/chair", -HalfW + 1.4f + i * 0.85f, -HalfD + 1.2f, Back);
            Place("furn/pottedPlant", HalfW - 0.6f, -HalfD + 0.6f, Front);
            Place("furn/pottedPlant", -HalfW + 0.6f, HalfD - 1.4f, Front);
            Place("furn/coatRackStanding", HalfW - 0.6f, HalfD - 1.6f, Left);
            Customers(3, -HalfW + 1f, HalfW - 2f, -HalfD + 1.8f, -0.8f);
        }

        void Berber(int level)
        {
            int chairs = Mathf.Clamp(2 + level / 2, 2, 4);
            for (int i = 0; i < chairs; i++)
            {
                float x = -HalfW + 1.3f + i * 1.6f;
                Place("furn/bathroomMirror", x, HalfD - 0.1f, Front, 1.2f);
                Place("furn/chairModernCushion", x, HalfD - 1.3f, Back);
                if (i < chairs - 1) Place("furn/sideTableDrawers", x + 0.8f, HalfD - 0.35f, Front);
            }
            Place("furn/bathroomSink", HalfW - 0.6f, HalfD - 0.6f, Left);
            Place("furn/coatRackStanding", -HalfW + 0.5f, -HalfD + 0.6f, Right);
            for (int i = 0; i < 3; i++) Place("furn/chair", HalfW - 0.7f, -HalfD + 1f + i * 0.9f, Left);
            Place("mm/employee", -HalfW + 2f, HalfD - 2.2f, Back);
            Customers(1 + level / 2, -HalfW + 1f, HalfW - 2f, -HalfD + 0.8f, 0f);
        }
    }
}
