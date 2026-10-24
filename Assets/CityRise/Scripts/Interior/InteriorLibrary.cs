using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Interior
{
    /// <summary>
    /// İç görünüm için gereken modeller ve malzemeler. TownBuilder doldurur;
    /// modeller Kenney (CC0) Mini Market, Food, Furniture ve Mini Characters kitlerinden gelir.
    /// </summary>
    public class InteriorLibrary : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public GameObject prefab;
            /// <summary>Modelin 1 biriminin metre karşılığı (kitler farklı ölçekte).</summary>
            public float scale = 1f;
        }

        public List<Entry> models = new List<Entry>();

        [Header("Malzemeler")]
        public Material floorTiles;
        public Material wallInside;
        public Material wallBrick;
        public Material wallCap;
        public Material concrete;
        public Material soil;
        public Material glass;
        public Material lightPanel;

        readonly Dictionary<string, Entry> lookup = new Dictionary<string, Entry>();

        public Entry Get(string key)
        {
            if (lookup.Count != models.Count)
            {
                lookup.Clear();
                foreach (var e in models) if (e.prefab != null) lookup[e.key] = e;
            }
            return lookup.TryGetValue(key, out var entry) ? entry : null;
        }
    }
}
