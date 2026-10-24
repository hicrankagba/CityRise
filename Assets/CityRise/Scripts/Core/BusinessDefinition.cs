using UnityEngine;

namespace CityRise.Core
{
    public enum CityStage
    {
        Seyyar = 1,
        Esnaf = 2,
        Sirket = 3,
        Holding = 4,
        Metropol = 5
    }

    /// <summary>
    /// Bir işletme türünün sabit bilgileri (simit büfesi, market, fabrika...).
    /// Proje penceresinde sağ tık > Create > CityRise > İşletme ile oluşturulur.
    /// </summary>
    [CreateAssetMenu(menuName = "CityRise/İşletme", fileName = "Isletme")]
    public class BusinessDefinition : ScriptableObject
    {
        public string id = "simit_bufesi";
        public string displayNameKey = "business.simit_bufesi";
        public CityStage requiredStage = CityStage.Seyyar;
        public int requiredLevel = 1;

        [Header("Para ($)")]
        public long buildCost = 1_500;
        public double incomePerHour = 60;
        public double expensePerHour = 15;

        [Header("Yükseltme")]
        [Tooltip("Her yükseltmede net gelir bu oranda artar.")]
        public float upgradeIncomeBonus = 0.25f;
        [Tooltip("Yükseltme bedeli = kurulum bedeli × bu oran × mevcut seviye.")]
        public float upgradeCostFactor = 0.6f;
        public int maxLevel = 5;

        [Header("Görünüm")]
        public GameObject prefab;
        public Vector2Int footprint = new Vector2Int(1, 1);
        /// <summary>Bina modeli, ör. "commercial/building-a" (bkz. TownBuilder).</summary>
        public string styleId = "commercial/building-a";
        /// <summary>Dükkan tabelasındaki yazı; boşsa tabela konmaz.</summary>
        public string signText = "";

        [HideInInspector] public int dataVersion;

        public double NetPerHour => incomePerHour - expensePerHour;

        /// <summary>Belirli seviyedeki net saatlik gelir.</summary>
        public double NetPerHourAt(int level) => NetPerHour * (1 + upgradeIncomeBonus * (level - 1));

        /// <summary>level'dan level+1'e yükseltme bedeli.</summary>
        public long UpgradeCost(int level) => (long)Mathf.Round(buildCost * upgradeCostFactor * level);
    }
}
