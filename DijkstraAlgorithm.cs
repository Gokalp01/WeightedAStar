using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp3
{
    /// Bir ba�lang�� d���m�nden graf'taki di�er t�m d���mlere en k�sa yollar�
    /// hesaplamak i�in Dijkstra'n�n algoritmas�n� uygular.
    public class DijkstraAlgoritmasi
    {
        // S�n�f�n temel de�i�kenleri
        private readonly int dugumSayisi;      // Graf'taki toplam d���m say�s�.
        private double[] mesafeler;           // Ba�lang��tan her d���me olan hesaplanm�� en k�sa mesafe.
        private int[] oncekiDugumler;         // En k�sa yolu izlemek i�in her d���m�n kendisinden �nceki d���m�.
        private int baslangicDugumu;          // Algoritman�n ba�layaca�� kaynak d���m.

        /// Dijkstra algoritmas� i�in gerekli olan temel parametreleri ayarlar.
        public DijkstraAlgoritmasi(int dugumSayisi)
        {
            this.dugumSayisi = dugumSayisi;
        }

        /// Algoritmay� �al��t�rarak en k�sa yollar� hesaplar.
        public void Calistir(double[,] komsulukMatrisi, int baslangicDugumu)
        {
            this.baslangicDugumu = baslangicDugumu;
            mesafeler = new double[dugumSayisi];
            oncekiDugumler = new int[dugumSayisi];
            bool[] ziyaretEdildi = new bool[dugumSayisi];

            // 1. Ba�latma: T�m mesafeleri 'sonsuz' olarak ayarla ve ba�lang�� d���m�n�n
            // mesafesini 0 yap. Bu, algoritman�n ba�lang�� noktas�n� belirler.
            for (int i = 0; i < dugumSayisi; i++)
            {
                mesafeler[i] = double.MaxValue;
                ziyaretEdildi[i] = false;
                oncekiDugumler[i] = -1;
            }
            mesafeler[baslangicDugumu] = 0;

            // 2. Ana D�ng�: T�m d���mler i�in en k�sa yol bulunana kadar devam et.
            for (int sayac = 0; sayac < dugumSayisi - 1; sayac++)
            {
                // Hen�z ziyaret edilmemi� d���mler aras�ndan en k�sa mesafeye sahip olan� se�.
                int u = EnKisaMesafeDugumuBul(mesafeler, ziyaretEdildi);
                if (u == -1) break; // Ula��lacak d���m kalmad�ysa d�ng�y� sonland�r.

                ziyaretEdildi[u] = true;

                // 3. Gev�etme (Relaxation): Se�ilen d���m (u) �zerinden kom�u d���mlere (v)
                // daha k�sa bir yol olup olmad���n� kontrol et.
                for (int v = 0; v < dugumSayisi; v++)
                {
                    double agirlik = komsulukMatrisi[u, v];
                    if (!ziyaretEdildi[v] && agirlik != double.PositiveInfinity && mesafeler[u] != double.MaxValue &&
                        mesafeler[u] + agirlik < mesafeler[v])
                    {
                        // E�er daha k�sa bir yol bulunduysa, mesafeyi g�ncelle ve yolu kaydet.
                        mesafeler[v] = mesafeler[u] + agirlik;
                        oncekiDugumler[v] = u;
                    }
                }
            }
        }

        /// Ziyaret edilmemi� d���mler aras�ndan en d���k mesafeye sahip olan� bulur.
        /// Bu, algoritman�n her ad�mda hangi d���m� i�leyece�ini belirler.
        private int EnKisaMesafeDugumuBul(double[] mesafeler, bool[] ziyaretEdildi)
        {
            double min = double.MaxValue;
            int minIndex = -1;
            for (int v = 0; v < dugumSayisi; v++)
            {
                if (!ziyaretEdildi[v] && mesafeler[v] <= min)
                {
                    min = mesafeler[v];
                    minIndex = v;
                }
            }
            return minIndex;
        }

        /// Algoritma �al��t�ktan sonra hesaplanan en k�sa yollar� ve mesafeleri konsola yazd�r�r.
        public void SonuclariYazdir()
        {
            if (mesafeler == null || oncekiDugumler == null)
            {
                Console.WriteLine("Calistir() metodunu �a��rmay� unutma.");
                return;
            }

            Console.WriteLine("\nDijkstra Algoritmas� Sonu�lar�");
            Console.WriteLine("=================================");
            Console.WriteLine($"Ba�lang�� D���m�: {baslangicDugumu + 1}\n");

            for (int i = 0; i < dugumSayisi; i++)
            {
                Console.Write($"Hedef D���m: {i + 1} \t Maliyet: ");
                if (mesafeler[i] == double.MaxValue)
                {
                    Console.WriteLine("Ula��lam�yor");
                    continue;
                }
                Console.Write($"{mesafeler[i]} \t\t Yol: ");

                // Yolu yeniden olu�turmak i�in hedef d���mden geriye do�ru gidilir.
                Stack<int> yol = new Stack<int>();
                int mevcutDugum = i;
                while (mevcutDugum != -1)
                {
                    yol.Push(mevcutDugum);
                    mevcutDugum = oncekiDugumler[mevcutDugum];
                }

                // Y���n (Stack) sayesinde yol, ba�lang��tan hedefe do�ru yazd�r�l�r.
                bool ilk = true;
                while (yol.Count > 0)
                {
                    if (!ilk) Console.Write(" -> ");
                    Console.Write(yol.Pop() + 1);
                    ilk = false;
                }
                Console.WriteLine();
            }
        }

        /// Sadece belirli bir hedef d���m i�in sonucu yazd�r�r
        public void SonuclariYazdir(int hedefDugum)
        {
            if (mesafeler == null || oncekiDugumler == null)
            {
                Console.WriteLine("Calistir() metodunu �a��rmay� unutma.");
                return;
            }
            Console.WriteLine($"\nDijkstra Algoritmas� Sonucu (Ba�lang��: {baslangicDugumu + 1}, Hedef: {hedefDugum + 1})");
            Console.WriteLine("=================================");
            Console.Write($"Maliyet: ");
            if (mesafeler[hedefDugum] == double.MaxValue)
            {
                Console.WriteLine("Ula��lam�yor");
                return;
            }
            Console.Write($"{mesafeler[hedefDugum]} \t Yol: ");
            Stack<int> yol = new Stack<int>();
            int mevcutDugum = hedefDugum;
            while (mevcutDugum != -1)
            {
                yol.Push(mevcutDugum);
                mevcutDugum = oncekiDugumler[mevcutDugum];
            }
            bool ilk = true;
            while (yol.Count > 0)
            {
                if (!ilk) Console.Write(" -> ");
                Console.Write(yol.Pop() + 1);
                ilk = false;
            }
            Console.WriteLine();
        }
    }
}