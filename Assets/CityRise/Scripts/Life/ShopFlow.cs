using System.Collections.Generic;
using CityRise.Game;
using CityRise.Interior;
using CityRise.Progression;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityRise.Life
{
    /// <summary>
    /// Canlı satış (oyuncu kendi dükkanının içindeyken): müşteriler kapıdan girer, başlarının üstünde istedikleri
    /// ürünün resmi çıkar, raftan alır, kasada sıraya girer ve öder. Ürün raftaysa satış olur ("+$3 Simit"),
    /// yoksa müşteri kızgın çıkar ve satış kaçar. Kasiyer sıradakine kendisi bakar; oyuncu kasadaki müşteriye
    /// dokunursa hemen satılır ve %20 bahşiş gelir (kendin sat).
    /// </summary>
    public class ShopFlow : MonoBehaviour
    {
        public const double TapTip = 0.2;
        const float CashierSeconds = 3.2f;

        GameManager game;
        CityEconomy eco;
        OwnedBusiness owned;
        ShopLayout layout;
        Camera cam;
        readonly List<ShopCustomer> queue = new List<ShopCustomer>();
        readonly List<ShopCustomer> all = new List<ShopCustomer>();
        float serveTimer;
        public System.Action<Vector3, string, Color> Popup;
        public static ShopFlow Active { get; private set; }

        /// <summary>Kasada bekleyen var mı (arayüzde ipucu göstermek için).</summary>
        public bool SomeoneAtRegister => queue.Count > 0 && queue[0].AtRegister;

        public void Init(GameManager gm, OwnedBusiness b, ShopLayout l, Camera camera)
        {
            game = gm;
            eco = gm.Economy;
            owned = b;
            layout = l;
            cam = camera;
            Active = this;
            // Sabit sıra bekleyenler canlı sırayı kapatmasın.
            foreach (var w in l.waiting) if (w != null) w.SetActive(false);
            var products = Products.For(b.businessId);
            // Dükkan canlı görünsün: en az 3 müşteri (gerçekçi karakterlerden), kapıdan sırayla gelirler.
            var people = new List<GameObject>();
            if (gm.interiorLibrary != null)
                foreach (var m in gm.interiorLibrary.models) if (m.prefab != null && m.key.StartsWith("mx/")) people.Add(m.prefab);
            var rng = new System.Random(b.plotId);
            while (people.Count > 0 && l.customers.Count < 3)
            {
                var holder = new GameObject("Musteri").transform;
                holder.SetParent(l.transform, false);
                holder.localPosition = l.door;
                var body = Instantiate(people[rng.Next(people.Count)], holder);
                body.transform.localPosition = Vector3.zero;
                l.customers.Add(holder.gameObject);
            }
            int i = 0;
            foreach (var c in l.customers)
            {
                if (c == null) continue;
                var old = c.GetComponent<InteriorWanderer>();
                if (old != null) Destroy(old);
                var sc = c.AddComponent<ShopCustomer>();
                sc.Init(this, l, products, game.interiorLibrary, i == 0 ? 0f : i * 4f);
                all.Add(sc);
                i++;
            }
        }

        void OnDestroy() { if (Active == this) Active = null; }

        /// <summary>Müşteri raftan aldı, kasaya geliyor: sıranın sonuna.</summary>
        public int JoinQueue(ShopCustomer c)
        {
            queue.Add(c);
            return queue.Count - 1;
        }

        public Vector3 QueuePosition(int index) => layout.register + layout.queueDir.normalized * (0.85f * index);

        public int IndexOf(ShopCustomer c) => queue.IndexOf(c);

        /// <summary>Raftaki stok: müşteri ürünü bulabilir mi?</summary>
        public bool InStock(string productId) => owned.Count(productId) > 0;

        /// <summary>Ürünü seç: stokta olanlara ağırlık verilir ama bazen bitmiş ürün de istenir (kaçan satış).</summary>
        public Product PickWanted(Product[] list, System.Random rng)
        {
            var inStock = new List<Product>();
            foreach (var p in list) if (owned.Count(p.id) > 0) inStock.Add(p);
            if (inStock.Count > 0 && rng.NextDouble() < 0.85) return inStock[rng.Next(inStock.Count)];
            return list[rng.Next(list.Length)];
        }

        /// <summary>Bir müşterinin bir sonraki gelişine kadar bekleme: satış hızını dükkanın gücüne bağlar.</summary>
        public float RespawnDelay(System.Random rng)
        {
            // Oyuncu izlerken dükkan daha hareketli: normal satış hızının 3 katı.
            double perHour = System.Math.Max(10, eco.SalesPerHour(owned) * 3);
            float cycle = (float)(3600.0 / perHour) * Mathf.Max(1, all.Count);
            // Bir tur ~18 sn sürüyor; kalan süre kadar dışarıda bekler.
            return Mathf.Clamp(cycle - 18f, 1f, 90f) * (0.7f + 0.6f * (float)rng.NextDouble());
        }

        public void Lost(ShopCustomer c, Product p)
        {
            Popup?.Invoke(c.transform.position + Vector3.up * 2.2f, Loc.T("live.lost", p.name), new Color(1f, 0.45f, 0.4f));
        }

        void Update()
        {
            if (game == null || !game.InInterior) return;
            // Sıradakiler yerlerine ilerler.
            for (int i = 0; i < queue.Count; i++) queue[i].SetQueueIndex(i);
            if (queue.Count > 0 && queue[0].AtRegister)
            {
                serveTimer += Time.deltaTime;
                if (serveTimer >= CashierSeconds) Serve(queue[0], 0);
            }
            else serveTimer = 0;
            HandleTap();
        }

        void Serve(ShopCustomer c, double tip)
        {
            serveTimer = 0;
            queue.Remove(c);
            var p = c.Wanted;
            if (p != null && eco.SellOne(owned.plotId, p.id, tip))
            {
                if (tip > 0) Popup?.Invoke(c.transform.position + Vector3.up * 2.4f, Loc.T("live.tip"), new Color(1f, 0.9f, 0.3f));
                c.Paid();
            }
            else
            {
                if (p != null) Lost(c, p);
                c.LeaveAngry();
            }
        }

        /// <summary>Kendin sat: kasadaki (ya da sıradaki) müşteriye dokununca hemen satış ve bahşiş.</summary>
        void HandleTap()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || cam == null) return;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
            if (queue.Count == 0 || !queue[0].AtRegister) return;
            // Kasadaki müşteriye ya da kasaya dokunuldu mu?
            var c = queue[0];
            var target = c.transform.position + Vector3.up * 1f;
            float d = Vector3.Cross(ray.direction, target - ray.origin).magnitude;
            var reg = layout.transform.TransformPoint(layout.register) + Vector3.up * 1f;
            float d2 = Vector3.Cross(ray.direction, reg - ray.origin).magnitude;
            if (d < 1.1f || d2 < 1.3f) Serve(c, TapTip);
#endif
        }
    }
}
