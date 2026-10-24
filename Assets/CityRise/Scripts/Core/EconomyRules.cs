using UnityEngine;

namespace CityRise.Core
{
    /// <summary>
    /// Tasarım belgesindeki sabit kurallar. Sunucu gelince aynı değerler oradan gelecek.
    /// </summary>
    public static class EconomyRules
    {
        public const long StartingCash = 3_250;
        public const long StartingNetWorth = 15_000;

        /// <summary>Oyun kapalıyken gelirin biriktiği en fazla süre.</summary>
        public const double OfflineIncomeCapHours = 8;

        /// <summary>İşletme değeri = günlük net kâr × 120 × K × M + arsa değeri.</summary>
        public const double ValueDaysMultiplier = 120;

        /// <summary>Oyuncular arası satışta kesilen komisyon.</summary>
        public const double TradeCommission = 0.05;

        /// <summary>İlan fiyatı, işletme değerinin %50'si ile %300'ü arasında olmalı.</summary>
        public const double MinListingRatio = 0.5;
        public const double MaxListingRatio = 3.0;

        /// <summary>Hisselerin bu oranını toplayan, şirketin tamamını devralır.</summary>
        public const double TakeoverShareRatio = 0.51;
        public const double TakeoverDefenseHours = 24;

        /// <summary>Bir sonraki seviye için gereken XP = 100 × seviye^1.6.</summary>
        public static long XpForLevel(int level)
        {
            return (long)Mathf.Round(100f * Mathf.Pow(level, 1.6f));
        }

        public static double BusinessValue(double dailyNetProfit, double k, double m, double landValue)
        {
            return dailyNetProfit * ValueDaysMultiplier * k * m + landValue;
        }

        /// <summary>Oyuncu dönünce eklenecek çevrimdışı gelir; 8 saatle sınırlıdır.</summary>
        public static double OfflineIncome(double incomePerHour, double hoursAway)
        {
            return incomePerHour * Mathf.Clamp((float)hoursAway, 0f, (float)OfflineIncomeCapHours);
        }
    }
}
