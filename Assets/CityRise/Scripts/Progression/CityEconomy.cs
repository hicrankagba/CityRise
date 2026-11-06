using System;
using System.Collections.Generic;
using CityRise.City;
using CityRise.Core;
using UnityEngine;

namespace CityRise.Progression
{
    /// <summary>
    /// Oyunun kuralları (görünümden bağımsız): işletme kurma, gelir biriktirme ve toplama,
    /// yükseltme, XP ve seviye, aşama değişince yeni halkaların açılması.
    /// Sahne ve arayüz bu sınıfın olaylarını dinler; ileride aynı kurallar sunucuda da çalışabilir.
    /// </summary>
    public class CityEconomy
    {
        public enum BuildCheck { Ok, LevelTooLow, NotEnoughCash, PlotUnavailable, StageLocked, PlotTooSmall }

        public readonly PlayerProfile profile;
        public readonly ProgressionConfig config;
        public readonly BusinessCatalog catalog;
        public readonly CityLayout layout;

        /// <summary>Para, XP ya da işletmeler değişti.</summary>
        public event Action Changed;
        /// <summary>Yeni seviye ve o seviyede açılan işletmeler.</summary>
        public event Action<int, List<BusinessDefinition>> LeveledUp;
        /// <summary>Yeni aşama ve açılan bloklar.</summary>
        public event Action<CityStage, List<Vector2Int>> StageUnlocked;
        public event Action<Plot, OwnedBusiness> BusinessBuilt;
        /// <summary>Komşunun hazır işletmesi satın alındı.</summary>
        public event Action<Plot, OwnedBusiness> BusinessAcquired;
        public event Action<Plot, OwnedBusiness> BusinessUpgraded;
        public event Action<Plot, double, long> Collected;
        /// <summary>Dükkanın görünümü değişti (satın alma ya da seçim).</summary>
        public event Action<Plot, OwnedBusiness> LookChanged;

        public CityEconomy(PlayerProfile profile, ProgressionConfig config, BusinessCatalog catalog, CityLayout layout)
        {
            this.profile = profile;
            this.config = config;
            this.catalog = catalog;
            this.layout = layout;
        }

        /// <summary>
        /// Kayıttan dönüşte düzeni oyuncunun durumuna getirir: ulaşılan aşamanın halkalarını açar,
        /// sahip olunan işletmeleri arsalarına yerleştirir. Sahne kurulmadan önce çağrılır.
        /// </summary>
        public void Restore()
        {
            UnlockUpTo(profile.stage);
            foreach (var owned in profile.businesses)
            {
                var plot = layout.FindPlot(owned.plotId);
                var def = catalog.Find(owned.businessId);
                if (plot == null || def == null) continue;
                Occupy(plot, def);
            }
        }

        // ------------------------------------------------------------ Kurma

        public long LandPrice(Plot plot) => plot.Area * config.landPricePerCell;

        public long TotalPrice(BusinessDefinition def, Plot plot) => def.buildCost + LandPrice(plot);

        public bool IsOpen(Plot plot) => plot != null && plot.unlockStage <= profile.stage && plot.type != PlotType.Locked;

        public BuildCheck CanBuild(BusinessDefinition def, Plot plot)
        {
            if (plot == null || plot.type != PlotType.ForSale || !IsOpen(plot)) return BuildCheck.PlotUnavailable;
            if (def.requiredStage > profile.stage) return BuildCheck.StageLocked;
            if (profile.level < def.requiredLevel) return BuildCheck.LevelTooLow;
            if (plot.cells.width < def.footprint.x || plot.cells.height < def.footprint.y) return BuildCheck.PlotTooSmall;
            if (profile.cash < TotalPrice(def, plot)) return BuildCheck.NotEnoughCash;
            return BuildCheck.Ok;
        }

        public bool Build(BusinessDefinition def, Plot plot)
        {
            if (CanBuild(def, plot) != BuildCheck.Ok) return false;
            long price = TotalPrice(def, plot);
            profile.cash -= price;
            Occupy(plot, def);
            var owned = new OwnedBusiness { plotId = plot.id, businessId = def.id };
            profile.businesses.Add(owned);
            BusinessBuilt?.Invoke(plot, owned);
            AddXp(Mathf.Max(1, Mathf.RoundToInt(price * config.xpPerBuildDollar)));
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ Hazır işletme satın alma

        /// <summary>
        /// Komşunun (şimdilik NPC) işletmesinin fiyatı: tasarım belgesindeki işletme değeri formülü,
        /// $50'ye yuvarlanmış. Oyuncular arası satış gelince aynı değer ilan fiyatının tabanı olur.
        /// </summary>
        public long AcquirePrice(Plot plot)
        {
            var def = plot != null ? catalog.Find(plot.occupantId) : null;
            if (def == null) return 0;
            double value = EconomyRules.BusinessValue(def.NetPerHour * 24, config.businessValueK, config.businessValueM, LandPrice(plot));
            return (long)(Math.Round(value / 50.0) * 50);
        }

        public BuildCheck CanAcquire(Plot plot)
        {
            var def = plot != null ? catalog.Find(plot.occupantId) : null;
            if (def == null || plot.type != PlotType.Business || profile.Find(plot.id) != null) return BuildCheck.PlotUnavailable;
            if (def.requiredStage > profile.stage) return BuildCheck.StageLocked;
            if (profile.level < def.requiredLevel) return BuildCheck.LevelTooLow;
            if (profile.cash < AcquirePrice(plot)) return BuildCheck.NotEnoughCash;
            return BuildCheck.Ok;
        }

        public bool Acquire(Plot plot)
        {
            if (CanAcquire(plot) != BuildCheck.Ok) return false;
            long price = AcquirePrice(plot);
            profile.cash -= price;
            // Komşunun işletmesi zaten çalışıyor: seviye 2 olarak devralınır.
            var owned = new OwnedBusiness { plotId = plot.id, businessId = plot.occupantId, acquired = true, level = 2 };
            // Komşunun dükkanındaki süsler dükkanla birlikte gelir.
            owned.look.GiveAllDecor();
            profile.businesses.Add(owned);
            BusinessAcquired?.Invoke(plot, owned);
            AddXp(Mathf.Max(1, Mathf.RoundToInt(price * config.xpPerBuildDollar)));
            Changed?.Invoke();
            return true;
        }

        static void Occupy(Plot plot, BusinessDefinition def)
        {
            plot.type = PlotType.Business;
            plot.occupantId = def.id;
            plot.styleId = def.styleId;
        }

        // ------------------------------------------------------------ Gelir

        public double NetPerHour(OwnedBusiness owned)
        {
            var def = catalog.Find(owned.businessId);
            // Haftalık etkinlikteki işletme türü katlı kazanır.
            return def == null ? 0 : def.NetPerHourAt(owned.level) * WeeklyEvent.IncomeMultiplier(owned.businessId);
        }

        public double Capacity(OwnedBusiness owned) => NetPerHour(owned) * config.storageHours;

        /// <summary>Oyun açıkken geçen süre kadar gelir biriktirir (depolama sınırına kadar).</summary>
        public void Tick(double seconds)
        {
            foreach (var b in profile.businesses)
                b.stored = Math.Min(Capacity(b), b.stored + NetPerHour(b) * seconds / 3600.0);
        }

        /// <summary>Oyun kapalıyken birikenler; EconomyRules gereği en fazla 8 saat.</summary>
        public double ApplyOffline(double hoursAway)
        {
            double total = 0;
            foreach (var b in profile.businesses)
            {
                double add = EconomyRules.OfflineIncome(NetPerHour(b), hoursAway);
                double before = b.stored;
                b.stored = Math.Min(Capacity(b), b.stored + add);
                total += b.stored - before;
            }
            return total;
        }

        public double Collect(int plotId)
        {
            var owned = profile.Find(plotId);
            if (owned == null || owned.stored < 1) return 0;
            double amount = Math.Floor(owned.stored);
            owned.stored -= amount;
            profile.cash += amount;
            long xp = Math.Max(config.minCollectXp, (long)Math.Round(amount * config.xpPerCollectDollar));
            Collected?.Invoke(layout.FindPlot(plotId), amount, xp);
            AddXp(xp);
            Changed?.Invoke();
            return amount;
        }

        // ------------------------------------------------------------ Görünüm

        /// <summary>Seçeneğin şu anki fiyatı: daha önce alındıysa 0.</summary>
        public long LookPrice(OwnedBusiness owned, ShopOptions.Group group, int index)
        {
            var opts = ShopOptions.Options(group);
            if (index < 0 || index >= opts.Length) return 0;
            return owned.look.Owns(ShopOptions.Key(group, index)) ? 0 : opts[index].price;
        }

        /// <summary>Renk/zemin/lamba/tabela/tente seçimi. Alınmamışsa parası ödenir. index = -1: varsayılana dön (tente: kaldır).</summary>
        public bool ChooseLook(int plotId, ShopOptions.Group group, int index)
        {
            var owned = profile.Find(plotId);
            if (owned == null) return false;
            long price = LookPrice(owned, group, index);
            if (profile.cash < price) return false;
            profile.cash -= price;
            if (index >= 0) { string key = ShopOptions.Key(group, index); if (!owned.look.Owns(key)) owned.look.bought.Add(key); }
            owned.look.Set(group, index);
            if (price > 0) AddXp(Mathf.Max(1, Mathf.RoundToInt(price * config.xpPerUpgradeDollar)));
            LookChanged?.Invoke(layout.FindPlot(plotId), owned);
            Changed?.Invoke();
            return true;
        }

        public long DecorPrice(OwnedBusiness owned, string decorId)
        {
            if (owned.look.Owns(ShopOptions.Key(ShopOptions.Group.Decor, decorId))) return 0;
            foreach (var d in ShopOptions.Decor) if (d.id == decorId) return d.price;
            return 0;
        }

        /// <summary>Dekoru açar/kapatır; ilk açışta satın alınır.</summary>
        public bool ToggleDecor(int plotId, string decorId)
        {
            var owned = profile.Find(plotId);
            if (owned == null) return false;
            var look = owned.look;
            if (look.Has(decorId))
            {
                look.decor.Remove(decorId);
            }
            else
            {
                long price = DecorPrice(owned, decorId);
                if (profile.cash < price) return false;
                profile.cash -= price;
                string key = ShopOptions.Key(ShopOptions.Group.Decor, decorId);
                if (!look.Owns(key)) look.bought.Add(key);
                look.decor.Add(decorId);
                if (price > 0) AddXp(Mathf.Max(1, Mathf.RoundToInt(price * config.xpPerUpgradeDollar)));
            }
            LookChanged?.Invoke(layout.FindPlot(plotId), owned);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ Yükseltme

        public long UpgradeCost(OwnedBusiness owned)
        {
            var def = catalog.Find(owned.businessId);
            return def == null ? 0 : def.UpgradeCost(owned.level);
        }

        public bool CanUpgrade(OwnedBusiness owned)
        {
            var def = catalog.Find(owned.businessId);
            return def != null && owned.level < def.maxLevel && profile.cash >= UpgradeCost(owned);
        }

        public bool Upgrade(int plotId)
        {
            var owned = profile.Find(plotId);
            if (owned == null || !CanUpgrade(owned)) return false;
            long cost = UpgradeCost(owned);
            profile.cash -= cost;
            owned.level++;
            BusinessUpgraded?.Invoke(layout.FindPlot(plotId), owned);
            AddXp(Mathf.Max(1, Mathf.RoundToInt(cost * config.xpPerUpgradeDollar)));
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ XP, seviye, aşama

        public void AddXp(long amount)
        {
            profile.xp += amount;
            while (profile.xp >= profile.XpToNextLevel)
            {
                profile.xp -= profile.XpToNextLevel;
                profile.level++;
                profile.cash += profile.level * config.levelUpCashPerLevel;
                LeveledUp?.Invoke(profile.level, catalog.UnlockedAt(profile.level));

                var stage = config.StageForLevel(profile.level);
                if (stage > profile.stage)
                {
                    profile.stage = stage;
                    var blocks = UnlockUpTo(stage);
                    StageUnlocked?.Invoke(stage, blocks);
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Halka r, aşama r'de açılır; başlangıç kasabası (halka 0-1) Seyyar'dır.</summary>
        public static int MaxRingFor(CityStage stage) => Mathf.Max(StartingTownGenerator.OpenRings, (int)stage);

        /// <summary>Verilen aşamaya kadar kilitli kalmış blokları satılık arsalara böler.</summary>
        public List<Vector2Int> UnlockUpTo(CityStage stage)
        {
            var opened = new List<Vector2Int>();
            var g = layout.grid;
            int maxRing = MaxRingFor(stage);
            for (int ring = 0; ring <= Mathf.Min(maxRing, g.radiusBlocks); ring++)
            for (int bx = -ring; bx <= ring; bx++)
            for (int by = -ring; by <= ring; by++)
            {
                var block = new Vector2Int(bx, by);
                if (CityGrid.Ring(block) != ring) continue;
                if (!layout.plots.Exists(p => p.block == block && p.type == PlotType.Locked)) continue;
                layout.UnlockBlock(block);
                opened.Add(block);
            }
            return opened;
        }
    }
}
