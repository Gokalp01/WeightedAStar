using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp3
{
    // Sezgisel fonksiyonun etkisini bir a��rl�k parametresi ile ayarlayan
    // A��rl�kl� A* (Weighted A*) algoritmas�n� uygular.
    public class AStarAlgorithm
    {
        private readonly int dugumSayisi;
        private readonly Point[] dugumKoordinatlari;
        private readonly double agirlik; // Sezgisel fonksiyon i�in a��rl�k (w)

        private double[] gScore;
        private double[] fScore;
        private int[] oncekiDugumler;
        private int baslangicDugumu;
        private int hedefDugumu;

        //A��rl�kl� A* algoritmas�n� ba�lat�r.
        //name="dugumSayisi">Graf'taki toplam d���m say�s�
        //name="koordinatlar">D���mlerin fiziksel konumlar�
        //name="agirlik">Sezgisel fonksiyonun a��rl���. w > 1 ise h�z �ncelikli, w = 1 ise standart A*, w < 1 ise maliyet �ncelikli
        public AStarAlgorithm(int dugumSayisi, Point[] koordinatlar, double agirlik = 1.0)
        {
            if (agirlik < 0) throw new ArgumentOutOfRangeException(nameof(agirlik), "A��rl�k negatif olamaz.");

            this.dugumSayisi = dugumSayisi;
            this.dugumKoordinatlari = koordinatlar;
            this.agirlik = agirlik;
        }

        // Heuristic fonksiyonu, iki d���m aras�ndaki co�rafi uzakl��� hesaplar.
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

        // Algoritmay� �al��t�rarak en k�sa yollar� hesaplar.
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
            // f skoru hesaplan�rken sezgisel de�er a��rl�k ile �arp�l�r.
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
                // En d���k f skoru olan d���m� se�tikten sonra onu a��k k�meden ��kar ve kapal� k�meye ekle.
                openSet.Remove(current);
                closedSet[current] = true;
                // �imdi bu d���m�n kom�ular�n� kontrol ediyoruz.
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

        // Sonu�lar� ekrana yazd�r�r.
        public void SonuclariYazdir()
        {
            if (gScore == null) return;

            Console.WriteLine($"\nA* Testi (A��rl�k: {this.agirlik})");
            Console.WriteLine("=================================");

            if (gScore[hedefDugumu] == double.MaxValue)
            {
                Console.WriteLine($"Hedef D���m: {hedefDugumu + 1} -> Ula��lam�yor");
                return;
            }
            // E�er hedef d���me ula��ld�ysa, yolun maliyetini ve fiziksel uzunlu�unu hesaplayal�m.
            Stack<int> yol = new Stack<int>();
            int mevcutDugum = hedefDugumu;
            while (mevcutDugum != -1)
            {
                yol.Push(mevcutDugum);
                mevcutDugum = oncekiDugumler[mevcutDugum];
            }

            int[] yolDizisi = yol.ToArray();

            // Fiziksel yol uzunlu�unu hesapla
            double fizikselUzunluk = 0;
            for (int i = 0; i < yolDizisi.Length - 1; i++)
            {
                fizikselUzunluk += Heuristic(yolDizisi[i], yolDizisi[i + 1]);
            }

            // Sonu�lar� yazd�r
            Console.WriteLine($"Toplam Maliyet: {gScore[hedefDugumu]}");
            Console.WriteLine($"Fiziksel Yol Uzunlu�u: {fizikselUzunluk:F2}");
            Console.WriteLine($"Ad�m Say�s�: {yolDizisi.Length}");
            Console.Write("Yol: ");
            Console.WriteLine(string.Join(" -> ", yolDizisi.Select(dugum => (dugum + 1).ToString())));
        }
    }
}
