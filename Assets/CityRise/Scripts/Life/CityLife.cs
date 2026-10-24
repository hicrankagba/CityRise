using System.Collections.Generic;
using CityRise.City;
using CityRise.Core;
using CityRise.Game;
using CityRise.Interior;
using UnityEngine;

namespace CityRise.Life
{
    /// <summary>
    /// Şehri canlandırır: yollarda şeritte giden arabalar, kaldırımlarda yürüyen ve dükkanlara girip çıkan yayalar.
    /// Sayılar aşamaya ve sahip olunan işletme sayısına göre artar; nesneler baştan havuzda oluşturulur,
    /// yalnızca gereken kadarı açılır. Ekranda görünmeyenlerin süs animasyonu atlanır.
    /// Modeller: Kenney Car Kit ve Mini Characters (CC0), InteriorLibrary üzerinden.
    /// </summary>
    public class CityLife : MonoBehaviour
    {
        public const int MaxCars = 20;
        public const int MaxWalkers = 40;

        /// <summary>Şerit, yol ortasından bu kadar sağda (sağdan trafik).</summary>
        const float LaneOffset = 1.6f;
        /// <summary>Kaldırım, blok kenarından bu kadar dışarıda.</summary>
        const float SidewalkOffset = 0.75f;

        GameManager game;
        CityGrid grid;
        HashSet<Vector2Int> roads = new HashSet<Vector2Int>();
        RectInt area;
        bool night;
        CityStage builtFor;
        int builtOwned;
        readonly List<Car> cars = new List<Car>();
        readonly List<Walker> walkers = new List<Walker>();
        System.Random rng = new System.Random(5);

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        static readonly string[] CarModels = { "car/sedan", "car/sedan", "car/suv", "car/hatchback-sports", "car/sedan-sports", "car/taxi", "car/van", "car/delivery", "car/truck", "car/police" };
        static readonly string[] People = { "ch/female-a", "ch/female-b", "ch/female-c", "ch/female-d", "ch/male-a", "ch/male-b", "ch/male-c", "ch/male-d" };

        public int ActiveCars { get; private set; }
        public int ActiveWalkers { get; private set; }

        public void Init(GameManager gm)
        {
            game = gm;
            grid = gm.town.Grid;
            night = RenderSettings.sun != null && RenderSettings.sun.name == "Ay";
            var lib = gm.interiorLibrary;
            if (lib == null) return;

            for (int i = 0; i < MaxCars; i++)
            {
                var e = lib.Get(CarModels[i % CarModels.Length]);
                if (e == null) continue;
                var go = Instantiate(e.prefab, transform);
                go.name = "Araba";
                go.transform.localScale = Vector3.one * e.scale;
                Tint(go, i);
                if (night) Headlights(go.transform);
                go.SetActive(false);
                cars.Add(new Car { t = go.transform, speed = 7f + (float)rng.NextDouble() * 3f });
            }
            for (int i = 0; i < MaxWalkers; i++)
            {
                var e = lib.Get(People[i % People.Length]);
                if (e == null) continue;
                var holder = new GameObject("Yaya");
                holder.transform.SetParent(transform, false);
                var body = Instantiate(e.prefab, holder.transform);
                body.transform.localScale = Vector3.one * e.scale;
                holder.SetActive(false);
                walkers.Add(new Walker { t = holder.transform, body = body.transform, scale = e.scale, speed = 1.1f + (float)rng.NextDouble() * 0.5f, phase = (float)rng.NextDouble() * 10f });
            }
            Rebuild();
        }

        /// <summary>Araba gövdesine biraz renk çeşitliliği (paletin üstüne hafif ton).</summary>
        static void Tint(GameObject go, int i)
        {
            Color[] tints = { Color.white, new Color(1f, 0.85f, 0.85f), new Color(0.85f, 0.9f, 1f), new Color(0.9f, 1f, 0.88f), new Color(1f, 0.97f, 0.8f) };
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tints[i % tints.Length]);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }

        static void Headlights(Transform car)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.95f, 0.7f) * 3f };
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(car, false);
                lamp.transform.localPosition = new Vector3(x / car.localScale.x, 0.55f / car.localScale.y, 1.32f / car.localScale.z);
                lamp.transform.localScale = new Vector3(0.28f, 0.14f, 0.05f) / car.localScale.x;
                lamp.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        /// <summary>Açık yolları ve hedef sayıları yeniden hesaplar (aşama değişince, işletme alınınca).</summary>
        public void Rebuild()
        {
            var profile = game.Profile;
            builtFor = profile.stage;
            builtOwned = profile.businesses.Count;
            roads.Clear();
            foreach (var r in game.town.layout.roads)
                if (r.unlockStage <= profile.stage) roads.Add(r.cell);
            int ring = Mathf.Min(CityRise.Progression.CityEconomy.MaxRingFor(profile.stage), grid.radiusBlocks);
            var min = grid.BlockRect(new Vector2Int(-ring, -ring)).min - Vector2Int.one;
            var max = grid.BlockRect(new Vector2Int(ring, ring)).max + Vector2Int.one;
            area = new RectInt(min, max - min);

            int stage = (int)profile.stage;
            int owned = profile.businesses.Count;
            int wantCars = Mathf.Min(cars.Count, 4 + 3 * stage + owned);
            int wantWalkers = Mathf.Min(walkers.Count, 10 + 5 * stage + 2 * owned);

            var roadList = new List<Vector2Int>(roads);
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                bool on = i < wantCars && roadList.Count > 0;
                if (on && !c.t.gameObject.activeSelf)
                {
                    var cell = roadList[rng.Next(roadList.Count)];
                    var dir = Dirs[rng.Next(4)];
                    if (!roads.Contains(cell + dir)) dir = NextDir(cell, -dir) ?? dir;
                    c.cell = cell;
                    c.dirIn = dir;
                    c.dirOut = NextDir(cell + dir, dir) ?? -dir;
                    c.cell = cell + dir;
                    c.t2 = (float)rng.NextDouble();
                    c.t.gameObject.SetActive(true);
                }
                if (!on) c.t.gameObject.SetActive(false);
            }
            for (int i = 0; i < walkers.Count; i++)
            {
                var w = walkers[i];
                bool on = i < wantWalkers;
                if (on && !w.t.gameObject.activeSelf)
                {
                    PickLoop(w);
                    w.t.gameObject.SetActive(true);
                }
                if (!on) w.t.gameObject.SetActive(false);
            }
            ActiveCars = Mathf.Min(wantCars, cars.Count);
            ActiveWalkers = Mathf.Min(wantWalkers, walkers.Count);
        }

        void Update()
        {
            if (game == null || game.InInterior) return;
            if (game.Profile.stage != builtFor || game.Profile.businesses.Count != builtOwned) Rebuild();
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            foreach (var c in cars) if (c.t.gameObject.activeSelf) MoveCar(c, dt);
            foreach (var w in walkers) if (w.t.gameObject.activeSelf) MoveWalker(w, dt);
        }

        // ------------------------------------------------------------ Arabalar

        class Car
        {
            public Transform t;
            public Vector2Int cell, dirIn, dirOut;
            public float t2, speed, current, waited;
        }

        Vector2Int? NextDir(Vector2Int cell, Vector2Int dirIn)
        {
            var options = new List<Vector2Int>();
            foreach (var d in Dirs)
                if (d != -dirIn && roads.Contains(cell + d) && area.Contains(cell + d)) options.Add(d);
            if (options.Count == 0) return null;
            // Düz gitmeyi biraz daha sık seç.
            if (options.Contains(dirIn) && rng.NextDouble() < 0.5) return dirIn;
            return options[rng.Next(options.Count)];
        }

        static Vector3 V(Vector2Int d) => new Vector3(d.x, 0, d.y);
        static Vector3 Right(Vector2Int d) => new Vector3(d.y, 0, -d.x);

        /// <summary>Hücre içindeki şerit eğrisi: girişten çıkışa ikinci dereceden Bezier.</summary>
        Vector3 LanePoint(Vector2Int cell, Vector2Int dirIn, Vector2Int dirOut, float t, out Vector3 tangent)
        {
            var c = grid.CellCenter(cell) + Vector3.up * 0.05f;
            float h = grid.cellSize * 0.5f;
            var p0 = c - V(dirIn) * h + Right(dirIn) * LaneOffset;
            var p2 = c + V(dirOut) * h + Right(dirOut) * LaneOffset;
            Vector3 p1;
            if (dirOut == dirIn) p1 = (p0 + p2) * 0.5f;
            else if (dirOut == -dirIn) p1 = c + V(dirIn) * h * 0.6f;
            else p1 = c + (Right(dirIn) + Right(dirOut)) * LaneOffset;
            float u = 1 - t;
            tangent = (2 * u * (p1 - p0) + 2 * t * (p2 - p1)).normalized;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }

        void MoveCar(Car c, float dt)
        {
            // Önündeki arabaya yaklaşınca yavaşla (basit takip).
            var pos = c.t.position;
            var fwd = c.t.forward;
            float limit = c.speed;
            foreach (var o in cars)
            {
                if (o == c || !o.t.gameObject.activeSelf) continue;
                var d = o.t.position - pos;
                float ahead = Vector3.Dot(d, fwd);
                if (ahead <= 0 || ahead > 9f) continue;
                float side = Vector3.Cross(fwd, d).magnitude;
                if (side > 1.3f) continue;
                limit = Mathf.Min(limit, Mathf.Max(0, (ahead - 4f) * 1.6f));
            }
            // Uzun süre kilitlenirse (kavşakta karşılıklı bekleme) yoluna devam etsin.
            c.waited = limit < 0.2f ? c.waited + dt : 0;
            if (c.waited > 2.5f) limit = c.speed * 0.4f;
            c.current = Mathf.MoveTowards(c.current, limit, (limit < c.current ? 14f : 4f) * dt);

            float turnLen = c.dirIn == c.dirOut ? grid.cellSize : grid.cellSize * 0.85f;
            c.t2 += c.current * dt / turnLen;
            while (c.t2 >= 1f)
            {
                c.t2 -= 1f;
                var next = c.cell + c.dirOut;
                c.dirIn = c.dirOut;
                c.cell = next;
                c.dirOut = NextDir(next, c.dirIn) ?? -c.dirIn;
            }
            var p = LanePoint(c.cell, c.dirIn, c.dirOut, c.t2, out var tan);
            c.t.SetPositionAndRotation(p, Quaternion.LookRotation(tan));
        }

        // ------------------------------------------------------------ Yayalar

        class Walker
        {
            public Transform t, body;
            public float speed, phase, pause, scale;
            public List<Vector3> loop = new List<Vector3>();
            public int target;
            public bool clockwise;
            public Vector3? shopDoor;
            public float inside;
        }

        /// <summary>Açık bir bloğun çevresindeki kaldırımı rota olarak seçer.</summary>
        void PickLoop(Walker w)
        {
            int ring = Mathf.Min(CityRise.Progression.CityEconomy.MaxRingFor(builtFor), grid.radiusBlocks);
            var block = new Vector2Int(rng.Next(-ring, ring + 1), rng.Next(-ring, ring + 1));
            var r = grid.BlockRect(block);
            float cs = grid.cellSize;
            float x0 = r.xMin * cs - SidewalkOffset, x1 = r.xMax * cs + SidewalkOffset;
            float z0 = r.yMin * cs - SidewalkOffset, z1 = r.yMax * cs + SidewalkOffset;
            w.loop.Clear();
            w.loop.Add(new Vector3(x0, 0.15f, z0));
            w.loop.Add(new Vector3(x1, 0.15f, z0));
            w.loop.Add(new Vector3(x1, 0.15f, z1));
            w.loop.Add(new Vector3(x0, 0.15f, z1));
            w.clockwise = rng.NextDouble() < 0.5;
            if (w.clockwise) w.loop.Reverse();
            int start = rng.Next(4);
            float k = (float)rng.NextDouble();
            w.t.position = Vector3.Lerp(w.loop[start], w.loop[(start + 1) % 4], k);
            w.target = (start + 1) % 4;
            w.shopDoor = null;
            w.inside = 0;
        }

        void MoveWalker(Walker w, float dt)
        {
            if (w.inside > 0)
            {
                // Dükkanın içinde: görünmez, sonra kapıdan çıkar.
                w.inside -= dt;
                if (w.inside <= 0) w.body.gameObject.SetActive(true);
                return;
            }
            if (w.pause > 0) { w.pause -= dt; Animate(w, 0); return; }

            var goal = w.shopDoor ?? w.loop[w.target];
            var pos = w.t.position;
            var to = goal - pos;
            to.y = 0;
            float step = w.speed * dt;
            if (to.magnitude <= step)
            {
                w.t.position = new Vector3(goal.x, pos.y, goal.z);
                if (w.shopDoor.HasValue)
                {
                    // Kapıya vardı: içeri girer, bir süre sonra çıkıp kaldırıma döner.
                    w.shopDoor = null;
                    w.body.gameObject.SetActive(false);
                    w.inside = 4f + (float)rng.NextDouble() * 8f;
                    return;
                }
                w.target = (w.target + 1) % w.loop.Count;
                if (rng.NextDouble() < 0.15) w.pause = 1f + (float)rng.NextDouble() * 2f;
                else if (rng.NextDouble() < 0.35) TryVisitShop(w);
                return;
            }
            var dir = to.normalized;
            w.t.position = pos + dir * step;
            w.t.rotation = Quaternion.Slerp(w.t.rotation, Quaternion.LookRotation(dir), 10f * dt);
            Animate(w, 1);
        }

        /// <summary>Kaldırım kenarındaki bir dükkanın kapısına yönelir.</summary>
        void TryVisitShop(Walker w)
        {
            var pos = w.t.position;
            Plot best = null;
            float bestD = 14f;
            foreach (var p in game.town.layout.plots)
            {
                if (p.type != PlotType.Business && game.Profile.Find(p.id) == null) continue;
                float d = Vector3.Distance(DoorOf(p), pos);
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best != null) w.shopDoor = DoorOf(best);
        }

        Vector3 DoorOf(Plot p)
        {
            var f = Plot.FacingDirection(p.facing);
            float half = Mathf.Min(p.cells.width, p.cells.height) * grid.cellSize * 0.5f;
            return grid.RectCenter(p.cells) + new Vector3(f.x, 0, f.y) * (half - 1.2f) + Vector3.up * 0.15f;
        }

        /// <summary>Yürüme hissi: hafif zıplama ve sağa-sola sallanma (yalnızca ekrandaysa).</summary>
        void Animate(Walker w, float moving)
        {
            if (w.body == null) return;
            w.phase += Time.deltaTime * 9f * moving;
            float bob = Mathf.Abs(Mathf.Sin(w.phase)) * 0.05f * moving;
            float sway = Mathf.Sin(w.phase) * 6f * moving;
            w.body.localPosition = new Vector3(0, bob, 0);
            w.body.localRotation = Quaternion.Euler(0, 0, sway);
        }
    }
}
