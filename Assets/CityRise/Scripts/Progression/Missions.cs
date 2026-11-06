using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Progression
{
    public enum MissionKind
    {
        /// <summary>Herhangi bir işletme kur (sayı).</summary>
        Build,
        /// <summary>Belirli bir işletmeye sahip ol (param = işletme kimliği).</summary>
        OwnBusiness,
        /// <summary>Kasadan para topla (kez).</summary>
        CollectTimes,
        /// <summary>Toplam şu kadar $ topla.</summary>
        CollectCash,
        /// <summary>Yükseltme yap (kez).</summary>
        Upgrade,
        /// <summary>Bir işletmeyi şu seviyeye çıkar.</summary>
        UpgradeTo,
        /// <summary>Komşunun işletmesini satın al (kez).</summary>
        Acquire,
        /// <summary>Şu kadar işletmeye sahip ol.</summary>
        OwnCount,
        /// <summary>Şu seviyeye ulaş.</summary>
        ReachLevel,
    }

    [Serializable]
    public class MissionDef
    {
        public string id;
        public MissionKind kind;
        public long target;
        public string param = "";
        public long rewardCash;
        public long rewardXp;

        public MissionDef(string id, MissionKind kind, long target, long cash, long xp, string param = "")
        {
            this.id = id;
            this.kind = kind;
            this.target = target;
            rewardCash = cash;
            rewardXp = xp;
            this.param = param;
        }
    }

    /// <summary>Etkin bir görevin ilerlemesi (oyuncu kaydında tutulur).</summary>
    [Serializable]
    public class MissionProgress
    {
        public string id;
        public long value;
    }

    /// <summary>
    /// Görevler: oyuncuya sıradaki adımı gösteren kısa hedefler. Aynı anda en fazla üç görev açıktır;
    /// tamamlanan görevin ödülü (para ve XP) dokununca alınır, yerine sıradaki gelir.
    /// Önce oyunu öğreten sabit bir zincir, bittikten sonra giderek büyüyen tekrar görevleri.
    /// Sayaç görevleri yalnızca görev açıkken sayar; durum görevleri (seviye, işletme sayısı) o anki duruma bakar.
    /// </summary>
    public class MissionSystem
    {
        public const int ActiveCount = 3;

        /// <summary>Oyunu öğreten görev zinciri (sırayla açılır).</summary>
        public static readonly MissionDef[] Chain =
        {
            new MissionDef("ilk_isletme", MissionKind.OwnCount, 1, 300, 40),
            new MissionDef("topla_3", MissionKind.CollectTimes, 3, 200, 30),
            new MissionDef("topla_500", MissionKind.CollectCash, 500, 300, 40),
            new MissionDef("ilk_yukseltme", MissionKind.Upgrade, 1, 400, 50),
            new MissionDef("seviye_3", MissionKind.ReachLevel, 3, 500, 0),
            new MissionDef("bakkal", MissionKind.OwnBusiness, 1, 800, 80, "bakkal"),
            new MissionDef("komsu_al", MissionKind.Acquire, 1, 1_000, 100),
            new MissionDef("uc_isletme", MissionKind.OwnCount, 3, 1_000, 100),
            new MissionDef("seviye3_isletme", MissionKind.UpgradeTo, 3, 1_200, 120),
            new MissionDef("seviye_5", MissionKind.ReachLevel, 5, 1_500, 0),
            new MissionDef("topla_5000", MissionKind.CollectCash, 5_000, 1_500, 150),
            new MissionDef("firin", MissionKind.OwnBusiness, 1, 2_000, 150, "firin"),
            new MissionDef("bes_isletme", MissionKind.OwnCount, 5, 2_500, 200),
            new MissionDef("kafe", MissionKind.OwnBusiness, 1, 3_000, 250, "kafe"),
            new MissionDef("market", MissionKind.OwnBusiness, 1, 4_000, 300, "market"),
            new MissionDef("seviye_10", MissionKind.ReachLevel, 10, 5_000, 0),
            new MissionDef("eczane", MissionKind.OwnBusiness, 1, 6_000, 400, "eczane"),
            new MissionDef("supermarket", MissionKind.OwnBusiness, 1, 15_000, 800, "supermarket"),
        };

        readonly CityEconomy eco;
        PlayerProfile Profile => eco.profile;

        /// <summary>Bir görev tamamlandı (ödül alınmayı bekliyor).</summary>
        public event Action<MissionDef> Completed;
        /// <summary>Ödül alındı.</summary>
        public event Action<MissionDef> Claimed;
        /// <summary>Görev listesi ya da ilerleme değişti.</summary>
        public event Action Changed;

        readonly HashSet<string> announced = new HashSet<string>();

        public MissionSystem(CityEconomy eco)
        {
            this.eco = eco;
            eco.BusinessBuilt += (plot, owned) => { Count(MissionKind.Build, 1); Evaluate(); };
            eco.BusinessAcquired += (plot, owned) => { Count(MissionKind.Acquire, 1); Evaluate(); };
            eco.BusinessUpgraded += (plot, owned) => { Count(MissionKind.Upgrade, 1); Evaluate(); };
            eco.Collected += (plot, amount, xp) =>
            {
                Count(MissionKind.CollectTimes, 1);
                Count(MissionKind.CollectCash, (long)amount);
                Evaluate();
            };
            eco.LeveledUp += (level, unlocked) => Evaluate();
            Fill();
            // Kayıttan dönüşte zaten tamamlanmış olanlar yeniden duyurulmasın.
            foreach (var p in Profile.missions) if (IsDone(p)) announced.Add(p.id);
        }

        /// <summary>Açık görevler (en fazla üç).</summary>
        public IReadOnlyList<MissionProgress> Active => Profile.missions;

        public MissionDef Def(string id)
        {
            foreach (var d in Chain) if (d.id == id) return d;
            // Tekrar görevi: "tekrar_<n>".
            if (id.StartsWith("tekrar_") && int.TryParse(id.Substring(7), out int n)) return Repeat(n);
            return null;
        }

        /// <summary>
        /// Zincir bitince gelen tekrar görevleri: sırayla para toplama, yükseltme ve işletme sahibi olma;
        /// hedef ve ödül her turda büyür.
        /// </summary>
        static MissionDef Repeat(int n)
        {
            int round = n / 3 + 1;
            switch (n % 3)
            {
                case 0:
                    long cash = 10_000L * round * round;
                    return new MissionDef("tekrar_" + n, MissionKind.CollectCash, cash, cash / 4, 100 * round);
                case 1:
                    return new MissionDef("tekrar_" + n, MissionKind.Upgrade, 2 + round, 2_000L * round, 120 * round);
                default:
                    return new MissionDef("tekrar_" + n, MissionKind.OwnCount, 5 + round, 4_000L * round, 200 * round);
            }
        }

        /// <summary>Görevin şu anki ilerlemesi (hedefe göre kırpılmış).</summary>
        public long Progress(MissionProgress p)
        {
            var d = Def(p.id);
            if (d == null) return 0;
            long v;
            switch (d.kind)
            {
                case MissionKind.ReachLevel: v = Profile.level; break;
                case MissionKind.OwnCount: v = Profile.businesses.Count; break;
                case MissionKind.OwnBusiness: v = Profile.businesses.Exists(b => b.businessId == d.param) ? 1 : 0; break;
                case MissionKind.UpgradeTo:
                    v = 0;
                    foreach (var b in Profile.businesses) v = Math.Max(v, b.level);
                    break;
                default: v = p.value; break;
            }
            return Math.Min(v, d.target);
        }

        public bool IsDone(MissionProgress p)
        {
            var d = Def(p.id);
            return d != null && Progress(p) >= d.target;
        }

        public bool Claim(string id)
        {
            var p = Profile.missions.Find(m => m.id == id);
            if (p == null || !IsDone(p)) return false;
            var d = Def(id);
            Profile.missions.Remove(p);
            Profile.missionsClaimed++;
            Profile.cash += d.rewardCash;
            Claimed?.Invoke(d);
            Fill();
            // Para ve XP değişince arayüz CityEconomy.Changed ile yenilenir; seviye atlama da oradan gelir.
            eco.AddXp(d.rewardXp);
            Evaluate();
            return true;
        }

        void Count(MissionKind kind, long amount)
        {
            foreach (var p in Profile.missions)
            {
                var d = Def(p.id);
                if (d != null && d.kind == kind) p.value += amount;
            }
        }

        /// <summary>Boşalan yerlere sıradaki görevleri koyar.</summary>
        void Fill()
        {
            while (Profile.missions.Count < ActiveCount)
            {
                string next = NextId();
                if (next == null) break;
                Profile.missions.Add(new MissionProgress { id = next });
                Profile.missionCursor++;
            }
        }

        string NextId()
        {
            int c = Profile.missionCursor;
            if (c < Chain.Length) return Chain[c].id;
            return "tekrar_" + (c - Chain.Length);
        }

        void Evaluate()
        {
            foreach (var p in Profile.missions)
            {
                if (!IsDone(p) || announced.Contains(p.id)) continue;
                announced.Add(p.id);
                Completed?.Invoke(Def(p.id));
            }
            Changed?.Invoke();
        }
    }
}
