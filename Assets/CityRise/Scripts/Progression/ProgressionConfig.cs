using System;
using System.Collections.Generic;
using CityRise.Core;
using UnityEngine;

namespace CityRise.Progression
{
    /// <summary>
    /// İlerleme ayarları: hangi seviyede hangi aşamaya geçilir, XP ve ödüller.
    /// Değerler tasarım için bu dosyadan değiştirilir; kod sabit değer içermez.
    /// Proje penceresinde sağ tık > Create > CityRise > İlerleme Ayarları.
    /// </summary>
    [CreateAssetMenu(menuName = "CityRise/İlerleme Ayarları", fileName = "IlerlemeAyarlari")]
    public class ProgressionConfig : ScriptableObject
    {
        [Serializable]
        public struct StageGate
        {
            public CityStage stage;
            public int minLevel;
        }

        [Tooltip("Her aşamanın başladığı seviye (küçükten büyüğe).")]
        public List<StageGate> stages = new List<StageGate>
        {
            new StageGate { stage = CityStage.Seyyar, minLevel = 1 },
            new StageGate { stage = CityStage.Esnaf, minLevel = 5 },
            new StageGate { stage = CityStage.Sirket, minLevel = 10 },
            new StageGate { stage = CityStage.Holding, minLevel = 20 },
            new StageGate { stage = CityStage.Metropol, minLevel = 35 },
        };

        [Header("Başlangıç")]
        public long startingCash = EconomyRules.StartingCash;

        [Header("XP")]
        [Tooltip("İşletme kurarken harcanan her $ için XP.")]
        public float xpPerBuildDollar = 0.05f;
        [Tooltip("Toplanan her $ için XP.")]
        public float xpPerCollectDollar = 0.1f;
        public int minCollectXp = 2;
        [Tooltip("Yükseltmeye harcanan her $ için XP.")]
        public float xpPerUpgradeDollar = 0.04f;

        [Header("Para")]
        [Tooltip("Arsa bedeli: hücre (8×8 m) başına $.")]
        public long landPricePerCell = 250;
        [Tooltip("Seviye atlayınca verilen ödül: yeni seviye × bu değer.")]
        public long levelUpCashPerLevel = 150;
        [Tooltip("Toplanmayan gelir en fazla bu kadar saatlik birikir.")]
        public double storageHours = EconomyRules.OfflineIncomeCapHours;

        public CityStage StageForLevel(int level)
        {
            var result = CityStage.Seyyar;
            foreach (var g in stages)
                if (level >= g.minLevel && g.stage > result) result = g.stage;
            return result;
        }

        public int LevelForStage(CityStage stage)
        {
            foreach (var g in stages)
                if (g.stage == stage) return g.minLevel;
            return 1;
        }

        public static ProgressionConfig CreateDefault() => CreateInstance<ProgressionConfig>();
    }
}
