using System;
using System.Collections.Generic;
using System.IO;
using CityRise.Core;
using UnityEngine;

namespace CityRise.Progression
{
    [Serializable]
    public class OwnedBusiness
    {
        public int plotId;
        public string businessId;
        public int level = 1;
        /// <summary>Toplanmayı bekleyen gelir ($).</summary>
        public double stored;
        /// <summary>Komşudan hazır satın alındı (bina sahnede zaten var, yeniden kurulmaz).</summary>
        public bool acquired;
        /// <summary>Oyuncunun seçtiği iç ve dış görünüm (zemin, renkler, dekor, tabela, tente).</summary>
        public ShopLook look = new ShopLook();
    }

    /// <summary>
    /// Oyuncunun kaydı: para, XP, seviye, aşama ve sahip olduğu işletmeler.
    /// Şimdilik cihazda JSON olarak tutulur; sunucu gelince aynı yapı oraya taşınır.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = 1;
        public double cash;
        /// <summary>Mevcut seviyede biriken XP (seviye atlayınca sıfırdan başlar).</summary>
        public long xp;
        public int level = 1;
        public CityStage stage = CityStage.Seyyar;
        public List<OwnedBusiness> businesses = new List<OwnedBusiness>();
        /// <summary>Açık görevler ve ilerlemeleri.</summary>
        public List<MissionProgress> missions = new List<MissionProgress>();
        /// <summary>Görev sırasında kaçıncı göreve gelindi (sıradaki açılacak görev).</summary>
        public int missionCursor;
        public int missionsClaimed;
        public long lastSaveUtcTicks;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "cityrise_kayit.json");

        public long XpToNextLevel => EconomyRules.XpForLevel(level);

        public OwnedBusiness Find(int plotId) => businesses.Find(b => b.plotId == plotId);

        public static PlayerProfile New(ProgressionConfig config) => new PlayerProfile
        {
            cash = config.startingCash,
            lastSaveUtcTicks = DateTime.UtcNow.Ticks,
        };

        public void Save()
        {
            lastSaveUtcTicks = DateTime.UtcNow.Ticks;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CityRise: Kayıt yazılamadı: {e.Message}");
            }
        }

        public static PlayerProfile LoadOrNew(ProgressionConfig config)
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(SavePath));
                    if (p != null) return p;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CityRise: Kayıt okunamadı, yeni oyun başlıyor: {e.Message}");
            }
            return New(config);
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
