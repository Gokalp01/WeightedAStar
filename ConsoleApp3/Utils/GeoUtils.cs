using System;

namespace ConsoleApp3.Utils
{
    /// <summary>
    /// Coðrafi hesaplamalar için yardýmcý metotlar içerir.
    /// </summary>
    public static class GeoUtils
    {
        private const double EarthRadiusMeters = 6371000.0;

        /// <summary>
        /// Ýki coðrafi koordinat arasýndaki mesafeyi Haversine formülünü kullanarak metre cinsinden hesaplar.
        /// </summary>
        /// <param name="lat1">Birinci noktanýn enlemi (derece).</param>
        /// <param name="lon1">Birinci noktanýn boylamý (derece).</param>
        /// <param name="lat2">Ýkinci noktanýn enlemi (derece).</param>
        /// <param name="lon2">Ýkinci noktanýn boylamý (derece).</param>
        /// <returns>Ýki nokta arasýndaki mesafe (metre).</returns>
        public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = EarthRadiusMeters * c;

            return distance;
        }

        /// <summary>
        /// Dereceyi radyana çevirir.
        /// </summary>
        private static double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
