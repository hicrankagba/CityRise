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
        /// <summary>Bir ürün satıldı (dükkan, ürün, fiyat).</summary>
        public event Action<OwnedBusiness, Product, long> Sold;
        /// <summary>Stok değişti (sipariş ya da satış).</summary>
        public event Action<OwnedBusiness> StockChanged;
        /// <summary>Dükkanın stoğu tamamen bitti.</summary>
        public event Action<OwnedBusiness> SoldOut;

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
            profile.stats.totalSpent += price;
            Occupy(plot, def);
            var owned = new OwnedBusiness { plotId = plot.id, businessId = def.id };
            GiveStarterStock(owned);
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
            profile.stats.totalSpent += price;
            // Komşunun işletmesi zaten çalışıyor: seviye 2 olarak devralınır.
            var owned = new OwnedBusiness { plotId = plot.id, businessId = plot.occupantId, acquired = true, level = 2 };
            // Komşunun dükkanındaki süsler dükkanla birlikte gelir.
            owned.look.GiveAllDecor();
            GiveStarterStock(owned);
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

        /// <summary>Kasanın alabileceği en fazla para (dolunca satış durur; toplamak gerekir).</summary>
        public double Capacity(OwnedBusiness owned) => Math.Max(50, SalesPerHour(owned) * AveragePrice(owned.businessId) * config.storageHours);

        // ------------------------------------------------------------ Satış ve stok

        /// <summary>Saatte kaç ürün satılır: hedef net kâr / ortalama kâr (seviye ve etkinlik dahil).</summary>
        public double SalesPerHour(OwnedBusiness b) => NetPerHour(b) / Products.AverageProfit(b.businessId);

        static double AveragePrice(string businessId)
        {
            var list = Products.For(businessId);
            if (list.Length == 0) return 1;
            double sum = 0;
            foreach (var p in list) sum += p.price;
            return sum / list.Length;
        }

        public int StockCapacity(OwnedBusiness b) => Products.Capacity(b.level);

        /// <summary>Yeni dükkan: her üründen bir sipariş kadar hediye stok.</summary>
        public void GiveStarterStock(OwnedBusiness b)
        {
            if (b.stockGiven) return;
            b.stockGiven = true;
            foreach (var p in Products.For(b.businessId))
                if (b.Count(p.id) < Products.Batch) b.Add(p.id, Products.Batch - b.Count(p.id));
        }

        /// <summary>Eski kayıtlarda stok yoksa bir kez başlangıç stoğu verilir.</summary>
        public void MigrateStock()
        {
            foreach (var b in profile.businesses) GiveStarterStock(b);
        }

        public long RestockCost(OwnedBusiness b, Product p, out int amount)
        {
            amount = Math.Min(Products.Batch, StockCapacity(b) - b.Count(p.id));
            return Math.Max(0, amount) * p.cost;
        }

        /// <summary>Tedarikçiden bir parti ürün sipariş eder (alış fiyatı ödenir, raf anında dolar).</summary>
        public bool Restock(int plotId, string productId)
        {
            var b = profile.Find(plotId);
            if (b == null) return false;
            var p = Products.Find(b.businessId, productId);
            if (p == null) return false;
            long cost = RestockCost(b, p, out int amount);
            if (amount <= 0 || profile.cash < cost) return false;
            profile.cash -= cost;
            profile.stats.totalSpent += cost;
            b.Add(p.id, amount);
            StockChanged?.Invoke(b);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Bütün rafları doldurur (para yettiği kadar, sırayla).</summary>
        public int RestockAll(int plotId)
        {
            var b = profile.Find(plotId);
            if (b == null) return 0;
            int n = 0;
            foreach (var p in Products.For(b.businessId))
                if (Restock(plotId, p.id)) n++;
            return n;
        }

        /// <summary>Satış: stoktaki ürünlerden rastgele biri satılır, fiyatı kasaya girer.</summary>
        int Sell(OwnedBusiness b, int count, bool notify)
        {
            var list = Products.For(b.businessId);
            int sold = 0;
            for (int i = 0; i < count; i++)
            {
                if (b.stored >= Capacity(b)) break;
                int inStock = 0;
                foreach (var p in list) if (b.Count(p.id) > 0) inStock++;
                if (inStock == 0) break;
                int pick = rng.Next(inStock);
                foreach (var p in list)
                {
                    if (b.Count(p.id) <= 0) continue;
                    if (pick-- > 0) continue;
                    b.Add(p.id, -1);
                    b.stored += p.price;
                    b.unitsSold++;
                    sold++;
                    if (notify) Sold?.Invoke(b, p, p.price);
                    break;
                }
            }
            if (sold > 0)
            {
                StockChanged?.Invoke(b);
                if (b.TotalStock() == 0) SoldOut?.Invoke(b);
            }
            return sold;
        }

        readonly System.Random rng = new System.Random();

        /// <summary>Oyun açıkken: müşteriler raftaki ürünleri alır.</summary>
        public void Tick(double seconds)
        {
            foreach (var b in profile.businesses)
            {
                if (b.TotalStock() == 0 || b.stored >= Capacity(b)) { b.salesCarry = 0; continue; }
                b.salesCarry += SalesPerHour(b) * seconds / 3600.0;
                int n = (int)Math.Floor(b.salesCarry);
                if (n <= 0) continue;
                b.salesCarry -= n;
                Sell(b, n, true);
            }
        }

        /// <summary>Oyun kapalıyken yapılan satışlar (en fazla 8 saat, stok ve kasa elverdiğince).</summary>
        public double ApplyOffline(double hoursAway)
        {
            double total = 0;
            double hours = Math.Min(hoursAway, EconomyRules.OfflineIncomeCapHours);
            foreach (var b in profile.businesses)
            {
                double before = b.stored;
                Sell(b, (int)Math.Floor(SalesPerHour(b) * hours), false);
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
            profile.stats.totalEarned += amount;
            profile.stats.collects++;
            long xp = Math.Max(config.minCollectXp, (long)Math.Round(amount * config.xpPerCollectDollar));
            Collected?.Invoke(layout.FindPlot(plotId), amount, xp);
            AddXp(xp);
            Changed?.Invoke();
            return amount;
        }

        // ------------------------------------------------------------ İstatistik

        /// <summary>İşletmenin bugünkü değeri (hazır işletme satın alma formülüyle aynı).</summary>
        public double BusinessWorth(OwnedBusiness b)
        {
            var plot = layout.FindPlot(b.plotId);
            return EconomyRules.BusinessValue(NetPerHour(b) * 24, config.businessValueK, config.businessValueM, plot != null ? LandPrice(plot) : 0);
        }

        /// <summary>Toplam servet: nakit + kasalardaki para + işletmelerin değeri.</summary>
        public double NetWorth()
        {
            double w = profile.cash;
            foreach (var b in profile.businesses) w += b.stored + BusinessWorth(b);
            return w;
        }

        public double TotalPerHour()
        {
            double t = 0;
            foreach (var b in profile.businesses) t += NetPerHour(b);
            return t;
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
            profile.stats.totalSpent += price;
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
            profile.stats.totalSpent += price;
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
            profile.stats.totalSpent += cost;
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
