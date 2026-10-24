using System.Collections.Generic;
using CityRise.Core;
using UnityEngine;

namespace CityRise.Progression
{
    /// <summary>Oyunda satın alınabilen işletmelerin listesi.</summary>
    [CreateAssetMenu(menuName = "CityRise/İşletme Kataloğu", fileName = "IsletmeKatalogu")]
    public class BusinessCatalog : ScriptableObject
    {
        public List<BusinessDefinition> businesses = new List<BusinessDefinition>();

        public BusinessDefinition Find(string id) => businesses.Find(b => b != null && b.id == id);

        /// <summary>Seviye sırasına göre (aynı seviyede ucuzdan pahalıya).</summary>
        public List<BusinessDefinition> Sorted()
        {
            var list = businesses.FindAll(b => b != null);
            list.Sort((a, b) => a.requiredLevel != b.requiredLevel ? a.requiredLevel.CompareTo(b.requiredLevel) : a.buildCost.CompareTo(b.buildCost));
            return list;
        }

        /// <summary>Tam olarak bu seviyede açılan işletmeler (seviye atlama penceresi için).</summary>
        public List<BusinessDefinition> UnlockedAt(int level) => businesses.FindAll(b => b != null && b.requiredLevel == level);
    }
}
