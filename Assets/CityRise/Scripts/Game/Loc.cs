using System.Collections.Generic;
using System.Globalization;
using CityRise.Core;

namespace CityRise.Game
{
    /// <summary>
    /// Basit çeviri tablosu. Arayüz metinleri anahtarla istenir; ileride başka diller
    /// aynı anahtarlarla eklenir. Bulunmayan anahtar olduğu gibi gösterilir.
    /// </summary>
    public static class Loc
    {
        public static string Language = "tr";

        static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>
        {
            ["tr"] = new Dictionary<string, string>
            {
                ["hud.level"] = "Seviye {0}",
                ["hud.xp"] = "{0} / {1} XP",
                ["stage.Seyyar"] = "Seyyar",
                ["stage.Esnaf"] = "Esnaf",
                ["stage.Sirket"] = "Şirket",
                ["stage.Holding"] = "Holding",
                ["stage.Metropol"] = "Metropol",

                ["buy.title"] = "Bu arsaya ne kuralım?",
                ["buy.land"] = "Arsa bedeli {0}",
                ["buy.row"] = "{0}  ·  saatte {1}",
                ["buy.button"] = "Kur  {0}",
                ["buy.locked"] = "Seviye {0} gerekli",
                ["buy.small"] = "Daha büyük arsa gerekir",
                ["buy.stage"] = "{0} aşamasında açılır",
                ["buy.nocash"] = "Para yetmiyor  {0}",
                ["buy.done"] = "{0} kuruldu!",

                ["biz.title"] = "{0}  ·  Seviye {1}",
                ["biz.income"] = "Saatte {0} net gelir",
                ["biz.stored"] = "Kasada {0}  (en fazla {1})",
                ["biz.collect"] = "Topla",
                ["biz.upgrade"] = "Yükselt  {0}",
                ["biz.max"] = "En yüksek seviye",
                ["biz.upgraded"] = "{0} seviye {1} oldu!",
                ["collect.done"] = "+{0}  ·  +{1} XP",

                ["plot.locked"] = "Bu bölge {0} aşamasında (Seviye {1}) açılır",
                ["plot.neighbor"] = "{0}: komşu işletme",
                ["plot.house"] = "Komşu evi",
                ["plot.park"] = "Park: herkesin",
                ["plot.plaza"] = "Kasaba meydanı",
                ["plot.civic"] = "Belediye binası",

                ["level.title"] = "Tebrikler! Seviye {0}",
                ["level.reward"] = "Ödül: {0}",
                ["level.unlocked"] = "Yeni açılanlar: {0}",
                ["level.stage"] = "Yeni aşama: {0}! Şehrin yeni bir halkası satışa açıldı.",
                ["level.ok"] = "Harika!",
                ["offline.title"] = "Hoş geldin!",
                ["offline.body"] = "Yokken işletmelerin {0} kazandı. Toplamak için binaların üstündeki paralara dokun.",
                ["common.close"] = "Kapat",
                ["interior.back"] = "◀  Şehre Dön",
                ["interior.collect"] = "Topla  {0}",

                ["business.simit_bufesi"] = "Simit Büfesi",
                ["business.bakkal"] = "Bakkal",
                ["business.manav"] = "Manav",
                ["business.kasap"] = "Kasap",
                ["business.firin"] = "Fırın",
                ["business.berber"] = "Berber",
                ["business.market"] = "Market",
                ["business.kafe"] = "Kafe",
                ["business.eczane"] = "Eczane",
                ["business.supermarket"] = "Süpermarket",
            },
        };

        public static string T(string key)
        {
            if (Tables.TryGetValue(Language, out var t) && t.TryGetValue(key, out var v)) return v;
            return key;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        /// <summary>Para: $3.250 biçiminde.</summary>
        public static string Money(double amount) => "$" + System.Math.Floor(amount).ToString("N0", Turkish);

        public static string Stage(CityStage stage) => T("stage." + stage);
    }
}
