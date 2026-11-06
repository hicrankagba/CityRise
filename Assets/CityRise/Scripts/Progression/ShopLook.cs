using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Progression
{
    /// <summary>
    /// Bir dükkanın oyuncu tarafından seçilen görünümü: zemin, duvar ve lambri rengi, lamba,
    /// tabela rengi, tente ve dekorlar. -1 = işletme türünün varsayılanı.
    /// Satın alınan seçenekler "grup:sıra" anahtarıyla saklanır; bir kez alınan seçenek sonra bedava seçilir.
    /// </summary>
    [Serializable]
    public class ShopLook
    {
        public int floor = -1;
        public int wall = -1;
        public int wainscot = -1;
        public int lamp = -1;
        public int sign = -1;
        /// <summary>-1 = tente yok.</summary>
        public int awning = -1;
        /// <summary>Açık dekorlar (ShopOptions.Decor kimlikleri).</summary>
        public List<string> decor = new List<string>();
        /// <summary>Satın alınmış seçenekler ("floor:2", "decor:hali" ...).</summary>
        public List<string> bought = new List<string>();

        public bool Has(string decorId) => decor.Contains(decorId);
        public bool Owns(string key) => bought.Contains(key);

        public int Get(ShopOptions.Group g)
        {
            switch (g)
            {
                case ShopOptions.Group.Floor: return floor;
                case ShopOptions.Group.Wall: return wall;
                case ShopOptions.Group.Wainscot: return wainscot;
                case ShopOptions.Group.Lamp: return lamp;
                case ShopOptions.Group.Sign: return sign;
                case ShopOptions.Group.Awning: return awning;
                default: return -1;
            }
        }

        public void Set(ShopOptions.Group g, int value)
        {
            switch (g)
            {
                case ShopOptions.Group.Floor: floor = value; break;
                case ShopOptions.Group.Wall: wall = value; break;
                case ShopOptions.Group.Wainscot: wainscot = value; break;
                case ShopOptions.Group.Lamp: lamp = value; break;
                case ShopOptions.Group.Sign: sign = value; break;
                case ShopOptions.Group.Awning: awning = value; break;
            }
        }

        /// <summary>Komşudan alınan dükkan: içindeki dekorlar dükkanla birlikte gelir.</summary>
        public void GiveAllDecor()
        {
            foreach (var d in ShopOptions.Decor)
            {
                if (!decor.Contains(d.id)) decor.Add(d.id);
                string key = ShopOptions.Key(ShopOptions.Group.Decor, d.id);
                if (!bought.Contains(key)) bought.Add(key);
            }
        }
    }

    /// <summary>Özelleştirme seçenekleri ve fiyatları (tasarım için buradan değiştirilir).</summary>
    public static class ShopOptions
    {
        public enum Group { Floor, Wall, Wainscot, Lamp, Decor, Sign, Awning }

        public struct Option
        {
            public string name;
            public Color color;
            public Color color2;
            public long price;
            public Option(string name, Color color, long price, Color? color2 = null)
            {
                this.name = name;
                this.color = color;
                this.color2 = color2 ?? Color.white;
                this.price = price;
            }
        }

        public struct DecorItem
        {
            public string id, name;
            public long price;
            /// <summary>Yalnızca yeme-içme dükkanlarında anlamlı (kara tahta menü).</summary>
            public bool foodOnly;
        }

        public static readonly Option[] Floors =
        {
            new Option("Karo", new Color(0.8f, 0.78f, 0.74f), 300),
            new Option("Parke", new Color(0.62f, 0.42f, 0.25f), 600),
            new Option("Ahşap", new Color(0.55f, 0.36f, 0.2f), 500),
            new Option("Mermer", new Color(0.86f, 0.82f, 0.74f), 1500),
        };

        public static readonly Option[] WallColors =
        {
            new Option("Krem", new Color(1f, 0.95f, 0.86f), 150),
            new Option("Kum", new Color(0.96f, 0.86f, 0.7f), 150),
            new Option("Beyaz", new Color(0.97f, 0.97f, 0.96f), 150),
            new Option("Gri", new Color(0.78f, 0.79f, 0.8f), 150),
            new Option("Mavi", new Color(0.72f, 0.82f, 0.9f), 200),
            new Option("Mint", new Color(0.76f, 0.9f, 0.82f), 200),
            new Option("Pembe", new Color(0.96f, 0.8f, 0.8f), 200),
            new Option("Hardal", new Color(0.93f, 0.8f, 0.45f), 200),
        };

        public static readonly Option[] WainscotColors =
        {
            new Option("Ceviz", new Color(0.36f, 0.24f, 0.16f), 150),
            new Option("Meşe", new Color(0.62f, 0.42f, 0.26f), 150),
            new Option("Beyaz", new Color(0.95f, 0.95f, 0.93f), 150),
            new Option("Lacivert", new Color(0.2f, 0.28f, 0.45f), 200),
            new Option("Yeşil", new Color(0.3f, 0.48f, 0.34f), 200),
            new Option("Bordo", new Color(0.5f, 0.15f, 0.17f), 200),
        };

        public static readonly Option[] Lamps =
        {
            new Option("Küre", new Color(0.95f, 0.93f, 0.85f), 300),
            new Option("Endüstriyel", new Color(0.45f, 0.5f, 0.4f), 400),
        };

        /// <summary>Tabela: color = zemin, color2 = yazı.</summary>
        public static readonly Option[] Signs =
        {
            new Option("Lacivert", new Color(0.12f, 0.2f, 0.32f), 300, new Color(1f, 0.93f, 0.75f)),
            new Option("Bordo", new Color(0.42f, 0.08f, 0.1f), 300, new Color(1f, 0.84f, 0.45f)),
            new Option("Yeşil", new Color(0.1f, 0.35f, 0.2f), 300, new Color(1f, 1f, 1f)),
            new Option("Siyah", new Color(0.08f, 0.08f, 0.09f), 300, new Color(0.95f, 0.78f, 0.35f)),
            new Option("Turuncu", new Color(0.9f, 0.45f, 0.1f), 300, new Color(1f, 1f, 1f)),
            new Option("Beyaz", new Color(0.97f, 0.96f, 0.93f), 300, new Color(0.12f, 0.2f, 0.32f)),
        };

        /// <summary>Tente: color = şerit rengi, color2 = ara şerit.</summary>
        public static readonly Option[] Awnings =
        {
            new Option("Kırmızı", new Color(0.78f, 0.14f, 0.12f), 400),
            new Option("Yeşil", new Color(0.15f, 0.5f, 0.28f), 400),
            new Option("Mavi", new Color(0.15f, 0.35f, 0.7f), 400),
            new Option("Turuncu", new Color(0.95f, 0.5f, 0.12f), 400),
            new Option("Lacivert", new Color(0.12f, 0.18f, 0.35f), 400),
            new Option("Sarı", new Color(0.98f, 0.8f, 0.2f), 400),
        };

        public static readonly DecorItem[] Decor =
        {
            new DecorItem { id = "hali", name = "Kilim", price = 350 },
            new DecorItem { id = "bitki", name = "Saksı bitkileri", price = 250 },
            new DecorItem { id = "tablo", name = "Tablolar", price = 300 },
            new DecorItem { id = "saat", name = "Duvar saati", price = 150 },
            new DecorItem { id = "aplik", name = "Duvar aplikleri", price = 350 },
            new DecorItem { id = "menu", name = "Kara tahta menü", price = 200, foodOnly = true },
        };

        public static readonly string[] FoodShops = { "kafe", "firin", "simit_bufesi", "manav" };

        public static Option[] Options(Group g)
        {
            switch (g)
            {
                case Group.Floor: return Floors;
                case Group.Wall: return WallColors;
                case Group.Wainscot: return WainscotColors;
                case Group.Lamp: return Lamps;
                case Group.Sign: return Signs;
                case Group.Awning: return Awnings;
                default: return new Option[0];
            }
        }

        public static string Key(Group g, int index) => $"{g}:{index}";
        public static string Key(Group g, string id) => $"{g}:{id}";
    }
}
