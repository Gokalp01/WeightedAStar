using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp3
{
    // Sezgisel fonksiyonun etkisini bir aðýrlýk parametresi ile ayarlayan
    // Aðýrlýklý A* (Weighted A*) algoritmasýný uygular.
    public class AStarAlgorithm
    {
        private readonly int dugumSayisi;
        private readonly Point[] dugumKoordinatlari;
        private readonly double agirlik; // Sezgisel fonksiyon için aðýrlýk (w)

        private double[] gScore;
        private double[] fScore;
        private int[] oncekiDugumler;
        private int baslangicDugumu;
        private int hedefDugumu;

        //Aðýrlýklý A* algoritmasýný baþlatýr.
        //name="dugumSayisi">Graf'taki toplam düðüm sayýsý
        //name="koordinatlar">Düðümlerin fiziksel konumlarý
        //name="agirlik">Sezgisel fonksiyonun aðýrlýðý. w > 1 ise hýz öncelikli, w = 1 ise standart A*, w < 1 ise maliyet öncelikli
        public AStarAlgorithm(int dugumSayisi, Point[] koordinatlar, double agirlik = 1.0)
        {
            if (agirlik < 0) throw new ArgumentOutOfRangeException(nameof(agirlik), "Aðýrlýk negatif olamaz.");

            this.dugumSayisi = dugumSayisi;
            this.dugumKoordinatlari = koordinatlar;
            this.agirlik = agirlik;
        }

        // Heuristic fonksiyonu, iki düðüm arasýndaki coðrafi uzaklýðý hesaplar.
        private double Heuristic(int from, int to)
        {
            if (dugumKoordinatlari == null) return 0;

            Point fromPoint = dugumKoordinatlari[from];
            Point toPoint = dugumKoordinatlari[to];

            double dx = fromPoint.X - toPoint.X;
            double dy = fromPoint.Y - toPoint.Y;
            double dz = fromPoint.Z - toPoint.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // Algoritmayý çalýþtýrarak en kýsa yollarý hesaplar.
        public void Calistir(double[,] komsulukMatrisi, int baslangic, int hedef)
        {
            this.baslangicDugumu = baslangic;
            this.hedefDugumu = hedef;

            gScore = new double[dugumSayisi];
            fScore = new double[dugumSayisi];
            oncekiDugumler = new int[dugumSayisi];

            var openSet = new List<int> { baslangicDugumu };
            var closedSet = new bool[dugumSayisi];

            for (int i = 0; i < dugumSayisi; i++)
            {
                gScore[i] = double.MaxValue;
                fScore[i] = double.MaxValue;
                oncekiDugumler[i] = -1;
            }

            gScore[baslangicDugumu] = 0;
            // f skoru hesaplanýrken sezgisel deðer aðýrlýk ile çarpýlýr.
            fScore[baslangicDugumu] = gScore[baslangicDugumu] + (agirlik * Heuristic(baslangicDugumu, hedefDugumu));

            while (openSet.Count > 0)
            {
                int current = -1;
                double minFScore = double.MaxValue;
                foreach (var node in openSet)
                {
                    if (fScore[node] < minFScore)
                    {
                        minFScore = fScore[node];
                        current = node;
                    }
                }

                if (current == -1 || current == hedefDugumu)
                {
                    return;
                }
                // En düþük f skoru olan düðümü seçtikten sonra onu açýk kümeden çýkar ve kapalý kümeye ekle.
                openSet.Remove(current);
                closedSet[current] = true;
                // Þimdi bu düðümün komþularýný kontrol ediyoruz.
                for (int neighbor = 0; neighbor < dugumSayisi; neighbor++)
                {
                    double yolMaliyeti = komsulukMatrisi[current, neighbor];
                    if (yolMaliyeti != double.PositiveInfinity && yolMaliyeti > 0)
                    {
                        if (closedSet[neighbor]) continue;

                        double tentativeGScore = gScore[current] + yolMaliyeti;

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                        else if (tentativeGScore >= gScore[neighbor])
                        {
                            continue;
                        }
                        oncekiDugumler[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + (agirlik * Heuristic(neighbor, hedefDugumu));
                    }
                }
            }
        }

        // Sonuçlarý ekrana yazdýrýr.
        public void SonuclariYazdir()
        {
            if (gScore == null) return;

            Console.WriteLine($"\nA* Testi (Aðýrlýk: {this.agirlik})");
            Console.WriteLine("=================================");

            if (gScore[hedefDugumu] == double.MaxValue)
            {
                Console.WriteLine($"Hedef Düðüm: {hedefDugumu + 1} -> Ulaþýlamýyor");
                return;
            }
            // Eðer hedef düðüme ulaþýldýysa, yolun maliyetini ve fiziksel uzunluðunu hesaplayalým.
            Stack<int> yol = new Stack<int>();
            int mevcutDugum = hedefDugumu;
            while (mevcutDugum != -1)
            {
                yol.Push(mevcutDugum);
                mevcutDugum = oncekiDugumler[mevcutDugum];
            }

            int[] yolDizisi = yol.ToArray();

            // Fiziksel yol uzunluðunu hesapla
            double fizikselUzunluk = 0;
            for (int i = 0; i < yolDizisi.Length - 1; i++)
            {
                fizikselUzunluk += Heuristic(yolDizisi[i], yolDizisi[i + 1]);
            }

            // Sonuçlarý yazdýr
            Console.WriteLine($"Toplam Maliyet: {gScore[hedefDugumu]}");
            Console.WriteLine($"Fiziksel Yol Uzunluðu: {fizikselUzunluk:F2}");
            Console.WriteLine($"Adým Sayýsý: {yolDizisi.Length}");
            Console.Write("Yol: ");
            Console.WriteLine(string.Join(" -> ", yolDizisi.Select(dugum => (dugum + 1).ToString())));
        }
    }
}
