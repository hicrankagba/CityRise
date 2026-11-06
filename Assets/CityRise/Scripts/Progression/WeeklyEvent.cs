using System;
using System.Globalization;

namespace CityRise.Progression
{
    /// <summary>
    /// Haftalık etkinlik: her hafta bir işletme türü öne çıkar ("Simit Haftası") ve o türdeki
    /// dükkanların geliri katlanır. Hafta pazartesi 00:00'da (cihaz saati) değişir; sıra sabittir,
    /// böylece herkes aynı hafta aynı etkinliği görür.
    /// </summary>
    public static class WeeklyEvent
    {
        public const double Multiplier = 2.0;

        static readonly string[] Rotation =
        {
            "simit_bufesi", "bakkal", "manav", "firin", "kafe", "kasap", "berber", "market", "eczane", "supermarket",
        };

        /// <summary>Bu haftanın öne çıkan işletmesi.</summary>
        public static string FeaturedBusiness(DateTime now)
        {
            int week = ISOWeek.GetWeekOfYear(now) + ISOWeek.GetYear(now) * 53;
            return Rotation[week % Rotation.Length];
        }

        public static string FeaturedBusiness() => FeaturedBusiness(DateTime.Now);

        /// <summary>Etkinliğin bitmesine kalan süre (gelecek pazartesi 00:00'a kadar).</summary>
        public static TimeSpan TimeLeft(DateTime now)
        {
            int daysToMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysToMonday == 0) daysToMonday = 7;
            return now.Date.AddDays(daysToMonday) - now;
        }

        public static double IncomeMultiplier(string businessId) => businessId == FeaturedBusiness() ? Multiplier : 1.0;
    }
}
