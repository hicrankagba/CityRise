using System;
using System.Collections.Generic;
using CityRise.City;
using CityRise.Game;
using CityRise.Progression;
using UnityEngine;

namespace CityRise.Life
{
    /// <summary>
    /// Tedarik kamyonları: oyuncu sipariş verince dükkanın önündeki yoldan bir kamyonet gelir,
    /// dükkanın önünde durur, kolileri boşaltır (koliler kapıya taşınır), sonra yoluna devam eder.
    /// Kamyon, siparişin varış zamanına göre hareket eder: varış anında dükkanın önündedir.
    /// </summary>
    public class DeliveryTrucks : MonoBehaviour
    {
        const float DriveSeconds = 9f;      // son düzlükte yaklaşma süresi
        const float UnloadSeconds = 4f;
        const float LeaveSeconds = 7f;
        const float Approach = 55f;          // yaklaşma mesafesi (m)

        class Truck
        {
            public int plotId;
            public long arriveTicks;
            public Transform t;
            public Vector3 start, stop, end;
            public List<Transform> boxes = new List<Transform>();
            public Vector3 door;
        }

        GameManager game;
        readonly List<Truck> trucks = new List<Truck>();
        GameObject truckPrefab, boxPrefab;
        float truckScale = 1f, boxScale = 1f;

        public void Init(GameManager gm)
        {
            game = gm;
            var lib = gm.interiorLibrary;
            var e = lib != null ? (lib.Get("rcar/pickup") ?? lib.Get("car/delivery") ?? lib.Get("car/van")) : null;
            if (e != null) { truckPrefab = e.prefab; truckScale = e.scale; }
            var b = lib != null ? (lib.Get("ph/cardboard_box_01") ?? lib.Get("ph/wooden_crate_01")) : null;
            if (b != null) { boxPrefab = b.prefab; boxScale = b.scale; }
        }

        void Update()
        {
            if (game == null || truckPrefab == null) return;
            long now = DateTime.UtcNow.Ticks;
            // Yeni kamyonlar: varışına DriveSeconds kalan her teslimat için (aynı dükkana tek kamyon).
            foreach (var b in game.Profile.businesses)
            {
                long next = long.MaxValue;
                foreach (var d in b.deliveries) next = Math.Min(next, d.arriveTicks);
                if (next == long.MaxValue) continue;
                double toArrive = (next - now) / (double)TimeSpan.TicksPerSecond;
                if (toArrive > DriveSeconds || toArrive < -UnloadSeconds) continue;
                if (trucks.Exists(x => x.plotId == b.plotId)) continue;
                var plot = game.town.layout.FindPlot(b.plotId);
                if (plot != null) Spawn(plot, next);
            }
            bool hidden = game.InInterior;
            for (int i = trucks.Count - 1; i >= 0; i--)
            {
                var k = trucks[i];
                double s = (now - k.arriveTicks) / (double)TimeSpan.TicksPerSecond;   // varışa göre saniye (eksi: yolda)
                if (s > UnloadSeconds + LeaveSeconds)
                {
                    foreach (var bx in k.boxes) if (bx != null) Destroy(bx.gameObject);
                    Destroy(k.t.gameObject);
                    trucks.RemoveAt(i);
                    continue;
                }
                k.t.gameObject.SetActive(!hidden);
                if (s < 0)
                {
                    // Yaklaşırken yavaşlayarak durur.
                    float a = Mathf.Clamp01(1f + (float)s / DriveSeconds);
                    k.t.position = Vector3.Lerp(k.start, k.stop, 1f - (1f - a) * (1f - a));
                }
                else if (s < UnloadSeconds)
                {
                    k.t.position = k.stop;
                    Unload(k, (float)s / UnloadSeconds, hidden);
                }
                else
                {
                    float a = Mathf.Clamp01((float)(s - UnloadSeconds) / LeaveSeconds);
                    k.t.position = Vector3.Lerp(k.stop, k.end, a * a);
                    foreach (var bx in k.boxes) if (bx != null) bx.gameObject.SetActive(false);
                }
            }
        }

        void Spawn(Plot plot, long arriveTicks)
        {
            var g = game.town.Grid;
            var f = Plot.FacingDirection(plot.facing);
            var front = new Vector3(f.x, 0, f.y);
            float halfDepth = (f.x != 0 ? plot.cells.width : plot.cells.height) * g.cellSize * 0.5f;
            var center = g.RectCenter(plot.cells);
            // Yolun ortası arsanın önünden yarım hücre ileride; dükkana yakın şeritte durur (sağdan trafik).
            var roadCenter = center + front * (halfDepth + g.cellSize * 0.5f);
            var dir = Vector3.Cross(front, Vector3.up).normalized;   // dükkan sağda kalacak yön
            var lane = roadCenter - front * 1.6f + Vector3.up * 0.02f;
            var k = new Truck
            {
                plotId = plot.id,
                arriveTicks = arriveTicks,
                start = lane - dir * Approach,
                stop = lane,
                end = lane + dir * Approach,
                door = center + front * (halfDepth - 1.4f),
            };
            var go = Instantiate(truckPrefab, transform);
            go.name = "TedarikKamyonu";
            go.transform.localScale = Vector3.one * truckScale;
            go.transform.rotation = Quaternion.LookRotation(dir);
            go.transform.position = k.start;
            k.t = go.transform;
            // Kasasında koliler.
            if (boxPrefab != null)
                for (int i = 0; i < 4; i++)
                {
                    var bx = Instantiate(boxPrefab, go.transform);
                    bx.name = "Koli";
                    bx.transform.localScale = Vector3.one * boxScale * 1.6f / Mathf.Max(0.01f, truckScale);
                    bx.transform.localPosition = new Vector3((i % 2 - 0.5f) * 0.5f, 0.95f / Mathf.Max(0.01f, truckScale), (-1.3f - (i / 2) * 0.5f) / Mathf.Max(0.01f, truckScale));
                    k.boxes.Add(bx.transform);
                }
            trucks.Add(k);
        }

        /// <summary>Boşaltma: koliler sırayla kamyondan kapıya doğru yay çizerek gider.</summary>
        void Unload(Truck k, float a, bool hidden)
        {
            for (int i = 0; i < k.boxes.Count; i++)
            {
                var bx = k.boxes[i];
                if (bx == null) continue;
                float start = i / (float)k.boxes.Count * 0.7f;
                float u = Mathf.Clamp01((a - start) / 0.3f);
                if (u <= 0) continue;
                if (bx.parent != null) bx.SetParent(null, true);
                var from = k.stop + Vector3.up * 1f;
                var to = k.door + Vector3.up * 0.3f + Vector3.Cross(Vector3.up, k.door - k.stop).normalized * ((i - 1.5f) * 0.35f);
                bx.position = Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 1.2f;
                bx.gameObject.SetActive(!hidden && u < 1f);
            }
        }
    }
}
