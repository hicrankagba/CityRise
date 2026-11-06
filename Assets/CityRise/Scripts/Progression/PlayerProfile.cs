using System;
using System.Collections.Generic;
using System.IO;
using CityRise.Core;
using UnityEngine;

namespace CityRise.Progression
{
    [Serializable]
    public class OwnedBusiness
    {
        public int plotId;
        public string businessId;
        public int level = 1;
        /// <summary>Toplanmayı bekleyen gelir ($).</summary>
        public double stored;
        /// <summary>Komşudan hazır satın alındı (bina sahnede zaten var, yeniden kurulmaz).</summary>
        public bool acquired;
        /// <summary>Oyuncunun seçtiği iç ve dış görünüm (zemin, renkler, dekor, tabela, tente).</summary>
        public ShopLook look = new ShopLook();
        /// <summary>Raftaki ürünler (ürün kimliği ve adet).</summary>
        public List<StockItem> stock = new List<StockItem>();
        /// <summary>Dükkanın arkasındaki depo (raf boşaldıkça çalışan buradan doldurur).</summary>
        public List<StockItem> depot = new List<StockItem>();
        public int depotLevel;
        /// <summary>Tedarikçiden yoldaki siparişler.</summary>
        public List<Delivery> deliveries = new List<Delivery>();
        public double refillCarry;

        public int DepotCount(string productId)
        {
            var s = depot.Find(x => x.id == productId);
            return s != null ? s.count : 0;
        }

        public int DepotTotal()
        {
            int t = 0;
            foreach (var s in depot) t += s.count;
            return t;
        }

        public int OnTheWay(string productId)
        {
            int t = 0;
            foreach (var d in deliveries) if (d.id == productId) t += d.amount;
            return t;
        }

        public int OnTheWayTotal()
        {
            int t = 0;
            foreach (var d in deliveries) t += d.amount;
            return t;
        }

        public void AddDepot(string productId, int amount)
        {
            var s = depot.Find(x => x.id == productId);
            if (s == null) depot.Add(s = new StockItem { id = productId });
            s.count += amount;
        }
        /// <summary>İlk stok verildi mi (eski kayıtlar için bir kez başlangıç stoğu).</summary>
        public bool stockGiven;
        /// <summary>Kesirli satış birikimi (saniyelik akışta küsurat kaybolmasın).</summary>
        public double salesCarry;
        public int unitsSold;

        public int Count(string productId)
        {
            var s = stock.Find(x => x.id == productId);
            return s != null ? s.count : 0;
        }

        public int TotalStock()
        {
            int t = 0;
            foreach (var s in stock) t += s.count;
            return t;
        }

        public void Add(string productId, int amount)
        {
            var s = stock.Find(x => x.id == productId);
            if (s == null) stock.Add(s = new StockItem { id = productId });
            s.count += amount;
        }
    }

    /// <summary>İstatistik ekranı için birikenler: toplam kazanç/harcama ve servet geçmişi.</summary>
    [Serializable]
    public class PlayerStats
    {
        public double totalEarned;
        public double totalSpent;
        public int collects;
        /// <summary>Servet örnekleri (en fazla MaxSamples): zaman (UTC tick) ve değer.</summary>
        public List<long> sampleTicks = new List<long>();
        public List<double> sampleWorth = new List<double>();
        public const int MaxSamples = 48;

        public void AddSample(double worth)
        {
            sampleTicks.Add(DateTime.UtcNow.Ticks);
            sampleWorth.Add(worth);
            while (sampleWorth.Count > MaxSamples) { sampleWorth.RemoveAt(0); sampleTicks.RemoveAt(0); }
        }
    }

    /// <summary>
    /// Oyuncunun kaydı: para, XP, seviye, aşama ve sahip olduğu işletmeler.
    /// Şimdilik cihazda JSON olarak tutulur; sunucu gelince aynı yapı oraya taşınır.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = 1;
        public double cash;
        /// <summary>Mevcut seviyede biriken XP (seviye atlayınca sıfırdan başlar).</summary>
        public long xp;
        public int level = 1;
        public CityStage stage = CityStage.Seyyar;
        public List<OwnedBusiness> businesses = new List<OwnedBusiness>();
        /// <summary>Açık görevler ve ilerlemeleri.</summary>
        public List<MissionProgress> missions = new List<MissionProgress>();
        /// <summary>Görev sırasında kaçıncı göreve gelindi (sıradaki açılacak görev).</summary>
        public int missionCursor;
        public int missionsClaimed;
        /// <summary>Rehber (muhtar) adımı; tutorialDone olunca bir daha gösterilmez.</summary>
        public int tutorialStep;
        public bool tutorialDone;
        public PlayerStats stats = new PlayerStats();
        public long lastSaveUtcTicks;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "cityrise_kayit.json");

        public long XpToNextLevel => EconomyRules.XpForLevel(level);

        public OwnedBusiness Find(int plotId) => businesses.Find(b => b.plotId == plotId);

        public static PlayerProfile New(ProgressionConfig config) => new PlayerProfile
        {
            cash = config.startingCash,
            lastSaveUtcTicks = DateTime.UtcNow.Ticks,
        };

        public void Save()
        {
            lastSaveUtcTicks = DateTime.UtcNow.Ticks;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CityRise: Kayıt yazılamadı: {e.Message}");
            }
        }

        public static PlayerProfile LoadOrNew(ProgressionConfig config)
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(SavePath));
                    if (p != null) return p;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CityRise: Kayıt okunamadı, yeni oyun başlıyor: {e.Message}");
            }
            return New(config);
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
