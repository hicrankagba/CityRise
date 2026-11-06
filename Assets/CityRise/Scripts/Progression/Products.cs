using System;
using System.Collections.Generic;

namespace CityRise.Progression
{
    /// <summary>Dükkanın sattığı bir ürün: alış maliyeti, satış fiyatı ve 3B modeli (InteriorLibrary anahtarı).</summary>
    public class Product
    {
        public string id, name, model;
        public long cost, price;
        public long Profit => price - cost;

        public Product(string id, string name, string model, long cost, long price)
        {
            this.id = id;
            this.name = name;
            this.model = model;
            this.cost = cost;
            this.price = price;
        }
    }

    /// <summary>Üretim tarifi: hammaddeden makinede (fırın, kahve makinesi) parti halinde ürün yapılır.</summary>
    public class Recipe
    {
        public string product, ingredient, machine;
        public int ingredientAmount, output;
        public double seconds;

        public Recipe(string product, string ingredient, int ingredientAmount, int output, double seconds, string machine)
        {
            this.product = product;
            this.ingredient = ingredient;
            this.ingredientAmount = ingredientAmount;
            this.output = output;
            this.seconds = seconds;
            this.machine = machine;
        }
    }

    /// <summary>Makinede pişen/hazırlanan bir parti.</summary>
    [Serializable]
    public class ProductionSlot
    {
        public string product;
        public int output;
        public long startTicks, endTicks;
    }

    [Serializable]
    public class StockItem
    {
        public string id;
        public int count;
    }

    /// <summary>Yoldaki tedarik siparişi: varış zamanı (UTC tick) gelince depoya girer.</summary>
    [Serializable]
    public class Delivery
    {
        public string id;
        public int amount;
        public long arriveTicks;
        public long orderTicks;
        public bool express;
    }

    /// <summary>
    /// Ürün listesi: her işletme türü gerçek ürünler satar. Ürün raftaysa müşteriler alır, satış parası kasaya girer;
    /// stok biterse satış durur, oyuncu tedarikçiden yeniden sipariş verir (alış fiyatını öder).
    /// Fiyatlar tasarım için buradan değiştirilir.
    /// </summary>
    public static class Products
    {
        static readonly Dictionary<string, Product[]> table = new Dictionary<string, Product[]>
        {
            ["simit_bufesi"] = new[]
            {
                new Product("simit", "Simit", "food/donut", 1, 3),
                new Product("pogaca", "Poğaça", "ph/hamburger_buns", 1, 4),
                new Product("kruvasan", "Kruvasan", "ph/croissant", 2, 6),
            },
            ["firin"] = new[]
            {
                new Product("ekmek", "Ekmek", "food/loaf", 1, 3),
                new Product("baget", "Baget", "food/loaf-baguette", 2, 5),
                new Product("kruvasan", "Kruvasan", "ph/croissant", 2, 6),
                new Product("pasta", "Çilekli Pasta", "ph/strawberry_chocolate_cake", 12, 30),
                new Product("havuclu_kek", "Havuçlu Kek", "ph/carrot_cake", 8, 20),
            },
            ["manav"] = new[]
            {
                new Product("elma", "Elma", "ph/food_apple_01", 1, 3),
                new Product("armut", "Armut", "ph/food_pears_asian_01", 1, 3),
                new Product("muz", "Muz", "ph/bananas", 2, 5),
                new Product("limon", "Limon", "ph/lemon", 1, 2),
                new Product("nar", "Nar", "ph/food_pomegranate_01", 2, 5),
                new Product("sogan", "Soğan", "ph/yellow_onion", 1, 2),
                new Product("domates", "Domates", "food/tomato", 1, 3),
                new Product("karpuz", "Karpuz", "food/watermelon", 4, 10),
            },
            ["bakkal"] = new[]
            {
                new Product("konserve", "Konserve", "ph/russian_food_cans_01", 2, 5),
                new Product("kuru_gida", "Makarna & Pirinç", "ph/long_life_food", 2, 5),
                new Product("su", "Damacana Su", "ph/plastic_bottle_gallon", 3, 7),
                new Product("deterjan", "Deterjan", "ph/multi_cleaner_bottle", 3, 8),
                new Product("peynir", "Peynir", "food/cheese", 4, 10),
                new Product("ekmek", "Ekmek", "food/loaf", 1, 3),
            },
            ["kasap"] = new[]
            {
                new Product("kaburga", "Kaburga", "food/meat-ribs", 10, 24),
                new Product("sucuk", "Sucuk", "food/meat-sausage", 6, 15),
                new Product("kiyma", "Kıyma", "food/meat-raw", 8, 19),
                new Product("but", "Kuzu But", "food/whole-ham", 18, 42),
            },
            ["kafe"] = new[]
            {
                new Product("kahve", "Kahve", "food/cup-coffee", 1, 5),
                new Product("cay", "Çay", "food/cup-saucer", 1, 3),
                new Product("kruvasan", "Kruvasan", "ph/croissant", 2, 6),
                new Product("pasta", "Çilekli Pasta", "ph/strawberry_chocolate_cake", 12, 30),
                new Product("havuclu_kek", "Havuçlu Kek", "ph/carrot_cake", 8, 20),
            },
            ["eczane"] = new[]
            {
                new Product("ilac", "İlaç", "ph/medical_box", 6, 15),
                new Product("kolonya", "Kolonya", "ph/multi_cleaner_bottle", 3, 8),
                new Product("vitamin", "Vitamin Şurubu", "ph/jug_01", 5, 12),
            },
            ["berber"] = new[]
            {
                new Product("sampuan", "Şampuan", "ph/multi_cleaner_bottle", 3, 9),
                new Product("tiras_kopugu", "Tıraş Köpüğü", "ph/jug_01", 4, 10),
                new Product("sac_kesimi", "Saç Kesimi Malzemesi", "ph/cardboard_box_01", 5, 18),
            },
            ["market"] = new[]
            {
                new Product("elma", "Elma", "ph/food_apple_01", 1, 3),
                new Product("muz", "Muz", "ph/bananas", 2, 5),
                new Product("ekmek", "Ekmek", "food/loaf", 1, 3),
                new Product("peynir", "Peynir", "food/cheese", 4, 10),
                new Product("konserve", "Konserve", "ph/russian_food_cans_01", 2, 5),
                new Product("su", "Damacana Su", "ph/plastic_bottle_gallon", 3, 7),
                new Product("deterjan", "Deterjan", "ph/multi_cleaner_bottle", 3, 8),
                new Product("sarap", "Meyve Suyu", "ph/wine_bottles_01", 5, 12),
            },
        };

        static Product[] Supermarket()
        {
            if (table.TryGetValue("supermarket", out var s)) return s;
            var list = new List<Product>(table["market"]);
            list.Add(new Product("pasta", "Çilekli Pasta", "ph/strawberry_chocolate_cake", 12, 30));
            list.Add(new Product("sucuk", "Sucuk", "food/meat-sausage", 6, 15));
            list.Add(new Product("karpuz", "Karpuz", "food/watermelon", 4, 10));
            list.Add(new Product("ilac", "Vitamin", "ph/jug_01", 5, 12));
            return table["supermarket"] = list.ToArray();
        }

        // ------------------------------------------------------------ Üretim

        /// <summary>Hammaddeler: tedarikçiden alınır, depoda durur, satılmaz.</summary>
        static readonly Product[] Ingredients =
        {
            new Product("un", "Un", "ph/cardboard_box_01", 3, 0),
            new Product("kahve_cekirdegi", "Çekirdek", "ph/jug_01", 4, 0),
            new Product("cay_yapragi", "Kuru Çay", "ph/long_life_food", 3, 0),
        };

        static readonly Dictionary<string, Recipe[]> recipes = new Dictionary<string, Recipe[]>
        {
            ["simit_bufesi"] = new[]
            {
                new Recipe("simit", "un", 2, 10, 45, "Fırın"),
                new Recipe("pogaca", "un", 2, 8, 50, "Fırın"),
                new Recipe("kruvasan", "un", 3, 6, 70, "Fırın"),
            },
            ["firin"] = new[]
            {
                new Recipe("ekmek", "un", 2, 10, 40, "Fırın"),
                new Recipe("baget", "un", 2, 6, 50, "Fırın"),
                new Recipe("kruvasan", "un", 3, 6, 70, "Fırın"),
                new Recipe("havuclu_kek", "un", 3, 3, 90, "Fırın"),
                new Recipe("pasta", "un", 4, 2, 120, "Fırın"),
            },
            ["kafe"] = new[]
            {
                new Recipe("kahve", "kahve_cekirdegi", 1, 8, 20, "Kahve makinesi"),
                new Recipe("cay", "cay_yapragi", 1, 10, 15, "Kahve makinesi"),
            },
        };

        public static Recipe RecipeFor(string businessId, string productId)
        {
            if (!recipes.TryGetValue(businessId, out var list)) return null;
            foreach (var r in list) if (r.product == productId) return r;
            return null;
        }

        public static bool Produces(string businessId) => recipes.ContainsKey(businessId);

        /// <summary>Bu dükkanın kullandığı hammaddeler.</summary>
        public static Product[] IngredientsFor(string businessId)
        {
            if (!recipes.TryGetValue(businessId, out var list)) return new Product[0];
            var result = new List<Product>();
            foreach (var r in list)
                foreach (var i in Ingredients)
                    if (i.id == r.ingredient && !result.Contains(i)) result.Add(i);
            return result.ToArray();
        }

        public static Product Ingredient(string id)
        {
            foreach (var i in Ingredients) if (i.id == id) return i;
            return null;
        }

        /// <summary>Makine gözü sayısı: dükkan seviyesiyle artar (1-4).</summary>
        public static int SlotCount(int level) => Math.Min(4, 1 + level / 2);

        public static Product[] For(string businessId)
        {
            if (businessId == "supermarket") return Supermarket();
            return table.TryGetValue(businessId, out var p) ? p : new Product[0];
        }

        /// <summary>Ürün ya da hammadde.</summary>
        public static Product Find(string businessId, string productId)
        {
            foreach (var p in For(businessId)) if (p.id == productId) return p;
            return Ingredient(productId);
        }

        /// <summary>Ortalama kâr (satış başına); satış hızı bundan hesaplanır.</summary>
        public static double AverageProfit(string businessId)
        {
            var list = For(businessId);
            if (list.Length == 0) return 1;
            double sum = 0;
            foreach (var p in list) sum += p.Profit;
            return Math.Max(0.5, sum / list.Length);
        }

        /// <summary>Bir ürün için raf kapasitesi (seviye arttıkça büyür).</summary>
        public static int Capacity(int level) => 20 + 10 * level;

        /// <summary>Tek siparişte gelen miktar.</summary>
        public const int Batch = 20;

        /// <summary>Depo kapasitesi (toplam ürün): depo seviyesiyle büyür.</summary>
        public static int DepotCapacity(int depotLevel) => 60 + 40 * depotLevel;
        public static long DepotUpgradeCost(int depotLevel) => 400L * (depotLevel + 1) * (depotLevel + 1);
        public const int MaxDepotLevel = 8;

        /// <summary>Normal kamyonla teslimat süresi (sn) ve hızlı teslimat süresi/ek ücreti.</summary>
        public const double NormalDeliverySeconds = 90;
        public const double ExpressDeliverySeconds = 15;
        public const double ExpressMultiplier = 1.5;

        /// <summary>Çalışanın depodan rafa taşıma hızı (ürün/sn).</summary>
        public const double RefillPerSecond = 0.5;
    }
}
