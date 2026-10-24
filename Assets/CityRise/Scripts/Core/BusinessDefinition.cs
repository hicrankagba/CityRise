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

        [Header("Para")]
        public long buildCost = 1_500;
        public double incomePerHour = 60;
        public double expensePerHour = 15;

        [Header("Görünüm")]
        public GameObject prefab;
        public Vector2Int footprint = new Vector2Int(1, 1);

        public double NetPerHour => incomePerHour - expensePerHour;
    }
}
