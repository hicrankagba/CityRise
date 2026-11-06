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
                ["interior.buy"] = "Satın Al  {0}",
                ["interior.owner"] = "Sahibi: komşu  ·  saatte {0}",
                ["interior.civic"] = "Kamu binası  ·  satılık değil",
                ["interior.bought"] = "{0} artık senin!",
                ["business.belediye"] = "Belediye",

                ["edit.open"] = "Düzenle",
                ["edit.title"] = "Dükkanı Düzenle",
                ["edit.tab.Floor"] = "Zemin",
                ["edit.tab.Wall"] = "Duvar",
                ["edit.tab.Wainscot"] = "Lambri",
                ["edit.tab.Lamp"] = "Lamba",
                ["edit.tab.Decor"] = "Dekor",
                ["edit.tab.Sign"] = "Tabela",
                ["edit.tab.Awning"] = "Tente",
                ["edit.default"] = "Varsayılan",
                ["edit.none"] = "Tente yok",
                ["edit.selected"] = "Seçili",
                ["edit.bought"] = "Alındı",
                ["edit.free"] = "Ücretsiz",
                ["edit.on"] = "Açık",
                ["edit.off"] = "Kapalı",
                ["edit.nocash"] = "Para yetmiyor",
                ["edit.hint"] = "Bir kez aldığın seçeneğe sonra ücretsiz dönebilirsin.",
                ["edit.hint.decor"] = "Dokununca satın alınır; sonra açıp kapatabilirsin.",
                ["event.banner"] = "{0}!",
                ["event.sub"] = "{0}x gelir  ·  {1} kaldı",
                ["event.days"] = "{0} gün {1} sa",
                ["event.hours"] = "{0} sa {1} dk",
                ["event.short"] = "Etkinlik {0}x",
                ["event.toast"] = "Bu hafta {0} dükkanları {1} kat kazanıyor. Hafta bitmeden bir tane aç ya da yükselt!",
                ["event.name.simit_bufesi"] = "Simit Haftası",
                ["event.name.bakkal"] = "Bakkal Haftası",
                ["event.name.manav"] = "Manav Haftası",
                ["event.name.firin"] = "Ekmek Haftası",
                ["event.name.kafe"] = "Kahve Haftası",
                ["event.name.kasap"] = "Kasap Haftası",
                ["event.name.berber"] = "Berber Haftası",
                ["event.name.market"] = "Market Haftası",
                ["event.name.eczane"] = "Sağlık Haftası",
                ["event.name.supermarket"] = "Süpermarket Haftası",
                ["clock.day"] = "Gündüz {0}",
                ["clock.night"] = "Gece {0}",
                ["stats.button"] = "İstatistik",
                ["stats.title"] = "İstatistikler",
                ["stats.worth"] = "Toplam servet",
                ["stats.worth.sub"] = "Nakit + kasalar + işletme değeri",
                ["stats.hourly"] = "Saatlik gelir",
                ["stats.hourly.sub"] = "{0} işletmeden",
                ["stats.earned"] = "Toplam kazanç",
                ["stats.earned.sub"] = "{0} kez toplandı",
                ["stats.spent"] = "Toplam harcama",
                ["stats.spent.sub"] = "Kurma, yükseltme, süsleme",
                ["stats.chart.worth"] = "Servet (5 dakikada bir kaydedilir)",
                ["stats.chart.income"] = "İşletmeler: saatlik gelir",
                ["stats.chart.empty"] = "Grafik için biraz oyna; servetin 5 dakikada bir kaydediliyor.",
                ["stats.bars.empty"] = "Henüz işletmen yok. Satılık bir arsaya dokun!",
                ["stats.perhour"] = "{0}/sa",
                ["stats.more"] = "+{0} işletme daha",
                ["guide.name"] = "Muhtar Rıza",
                ["guide.next"] = "İleri",
                ["guide.finish"] = "Bitir",
                ["guide.skip"] = "Atla",
                ["guide.0"] = "Hoş geldin evlat! Ben bu kasabanın muhtarıyım. Seyyar satıcılıktan holding sahipliğine giden yolda sana yol göstereceğim.",
                ["guide.1"] = "Kasabayı gezmek için ekranı sürükle, yakınlaşmak için iki parmakla (bilgisayarda fare tekerleğiyle) yaklaş.",
                ["guide.2"] = "Önündeki \"SATILIK\" tabelalı bir arsaya dokun ve ilk dükkanını kur. Simit büfesi iyi bir başlangıç!",
                ["guide.3"] = "Müşteriler raftaki ürünleri satın aldıkça kasaya para girer. Binanın üstünde sarı $ çıkınca dokun ve kazancını topla. Kırmızı ! görürsen stok bitmiştir.",
                ["guide.4"] = "Soldaki Görevler listesi sıradaki adımı söyler. Biten görevin yanındaki \"Ödülü Al\" düğmesine dokun.",
                ["guide.5"] = "Şimdi dükkanının binasına dokunup içine gir. İçeride müşterilerini görürsün.",
                ["guide.6"] = "İçeride \"Ürünler\" ile raflarını doldur: tedarikçiden ucuza alır, müşteriye kârla satarsın. Düzenle ile dükkanını süsleyebilirsin.",
                ["guide.7"] = "Sağ üstte saat ve haftalık etkinlik var; etkinlikteki dükkanlar 2 kat kazanır. Servetini İstatistik'ten izle. Kolay gelsin!",
                ["prod.open"] = "Ürünler",
                ["prod.title"] = "Raflar ve Sipariş",
                ["prod.fillall"] = "Hepsini Sipariş Et",
                ["prod.hint"] = "Müşteriler raftaki ürünleri alır, satış parası kasaya girer. Saatte ~{0} satış · raf kapasitesi ürün başına {1}.",
                ["prod.price"] = "Satış {0} · Alış {1}",
                ["prod.order"] = "+{0} al  {1}",
                ["prod.full"] = "Raf dolu",
                ["prod.full.or.cash"] = "Raflar dolu ya da para yetmiyor",
                ["prod.sold"] = "+{0}  {1}",
                ["prod.soldout"] = "{0}: stok bitti! Ürün sipariş et.",
                ["prod.info"] = "Rafta {0} ürün · saatte ~{1} satış",
                ["prod.empty.short"] = "Stok bitti! Ürünler'den sipariş ver",
                ["prod.shelve"] = "Rafa diz",
                ["prod.shelve.none"] = "Depoda rafa dizilecek ürün yok",
                ["prod.depot"] = "Depo {0}/{1} · yolda {2}\nSaatte ~{3} satış. Raf boşaldıkça çalışan depodan doldurur.",
                ["prod.depot.up"] = "Depo büyüt {0}",
                ["prod.depot.max"] = "Depo en büyük",
                ["prod.shelf"] = "Raf {0}/{1}",
                ["prod.depotone"] = "Depo {0}",
                ["prod.depotway"] = "Depo {0} · yolda {1} ({2})",
                ["prod.depotfull"] = "Depo dolu",
                ["prod.fast"] = "Hızlı",
                ["prod.ordered"] = "Sipariş verildi: kamyon yaklaşık {0} saniyede gelir",
                ["prod.ordered.fast"] = "Hızlı teslimat ({0}): kamyon 15 saniyede gelir",
                ["prod.arrived"] = "{0}: kamyon geldi, {1} {2} depoya indi",
                ["mission.header"] = "Görevler",
                ["mission.Build"] = "{0} işletme kur",
                ["mission.OwnBusiness"] = "{1} aç",
                ["mission.CollectTimes"] = "Kasadan {0} kez para topla",
                ["mission.CollectCash"] = "Toplam {0} kazanç topla",
                ["mission.Upgrade"] = "{0} kez yükseltme yap",
                ["mission.UpgradeTo"] = "Bir işletmeyi {0}. seviyeye çıkar",
                ["mission.Acquire"] = "Komşudan {0} işletme satın al",
                ["mission.OwnCount"] = "{0} işletmen olsun",
                ["mission.ReachLevel"] = "{0}. seviyeye ulaş",
                ["mission.id.ilk_isletme"] = "İlk işletmeni kur",
                ["mission.id.ilk_yukseltme"] = "İlk yükseltmeni yap",
                ["mission.id.komsu_al"] = "Bir komşu işletmesini satın al",
                ["mission.reward"] = "Ödül: {0}",
                ["mission.rewardXp"] = "Ödül: {0} + {1} XP",
                ["mission.claim"] = "Ödülü Al",
                ["mission.done"] = "Görev tamam: {0}. Ödülünü al!",
                ["mission.claimed"] = "+{0}  ·  +{1} XP",

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
