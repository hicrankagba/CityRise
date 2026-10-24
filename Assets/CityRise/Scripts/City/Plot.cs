using System;
using CityRise.Core;
using UnityEngine;

namespace CityRise.City
{
    public enum PlotType
    {
        /// <summary>Kilitli halkada; aşama açılınca bölünüp satışa çıkar.</summary>
        Locked = 0,
        /// <summary>Boş ve satılık.</summary>
        ForSale = 1,
        Business = 2,
        House = 3,
        Park = 4,
        /// <summary>Belediye, okul, hastane gibi kamu binaları.</summary>
        Civic = 5,
        /// <summary>Meydan: yayalara açık taş zemin.</summary>
        Plaza = 6,
    }

    /// <summary>Arsanın önü hangi yola bakıyor. Kuzey = +Z, Doğu = +X.</summary>
    public enum Facing
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// Şehirdeki bir arsa. Konum ve boyut hücre cinsindendir (bkz. <see cref="CityGrid"/>).
    /// Bina görünümü <see cref="styleId"/> ile ayrı tutulur; aynı arsaya sonradan
    /// daha büyük bir bina konabilir ya da komşu arsalarla birleştirilebilir.
    /// </summary>
    [Serializable]
    public class Plot
    {
        public int id;
        public RectInt cells;
        public PlotType type;
        public Facing facing;
        /// <summary>Bu arsanın satın alınabildiği en erken aşama.</summary>
        public CityStage unlockStage = CityStage.Seyyar;
        /// <summary>Hangi blokta olduğu; birleştirme yalnızca aynı blok içinde yapılır.</summary>
        public Vector2Int block;
        /// <summary>İşletme ya da yapı kimliği (örn. BusinessDefinition.id). Boşsa arsa boştur.</summary>
        public string occupantId = "";
        /// <summary>Görünüm anahtarı (örn. "shop/building-a"). Sanat seti değişse de veri aynı kalır.</summary>
        public string styleId = "";
        public int level = 1;

        public Vector2Int Size => cells.size;
        public int Area => cells.width * cells.height;
        public bool IsEmpty => string.IsNullOrEmpty(occupantId);

        public bool Contains(Vector2Int cell) => cells.Contains(cell);

        /// <summary>İki arsa aynı blokta, yan yana ve birleşince dikdörtgen oluyorsa birleştirilebilir.</summary>
        public bool CanMergeWith(Plot other)
        {
            if (other == null || other == this || other.block != block) return false;
            if (type == PlotType.Locked || other.type == PlotType.Locked) return false;
            var a = cells;
            var b = other.cells;
            bool sideBySide = a.yMin == b.yMin && a.height == b.height && (a.xMax == b.xMin || b.xMax == a.xMin);
            bool stacked = a.xMin == b.xMin && a.width == b.width && (a.yMax == b.yMin || b.yMax == a.yMin);
            return sideBySide || stacked;
        }

        public static Vector2Int FacingDirection(Facing f)
        {
            switch (f)
            {
                case Facing.North: return Vector2Int.up;
                case Facing.East: return Vector2Int.right;
                case Facing.South: return Vector2Int.down;
                default: return Vector2Int.left;
            }
        }

        /// <summary>Bu yöne bakan Y açısı (Kuzey 0°, Doğu 90°...). Modele özgü düzeltme görünüm tarafında yapılır.</summary>
        public static float FacingYaw(Facing f) => (int)f * 90f;
    }

    [Serializable]
    public class RoadCell
    {
        public Vector2Int cell;
        /// <summary>Ana cadde: ileride genişletilebilecek, şehir dışına uzanan yollar.</summary>
        public bool main;
        public CityStage unlockStage = CityStage.Seyyar;
    }
}
