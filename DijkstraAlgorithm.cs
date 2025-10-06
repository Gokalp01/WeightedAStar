using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp3
{
    /// Bir baþlangýç düðümünden graf'taki diðer tüm düðümlere en kýsa yollarý
    /// hesaplamak için Dijkstra'nýn algoritmasýný uygular.
    public class DijkstraAlgoritmasi
    {
        // Sýnýfýn temel deðiþkenleri
        private readonly int dugumSayisi;      // Graf'taki toplam düðüm sayýsý.
        private double[] mesafeler;           // Baþlangýçtan her düðüme olan hesaplanmýþ en kýsa mesafe.
        private int[] oncekiDugumler;         // En kýsa yolu izlemek için her düðümün kendisinden önceki düðümü.
        private int baslangicDugumu;          // Algoritmanýn baþlayacaðý kaynak düðüm.

        /// Dijkstra algoritmasý için gerekli olan temel parametreleri ayarlar.
        public DijkstraAlgoritmasi(int dugumSayisi)
        {
            this.dugumSayisi = dugumSayisi;
        }

        /// Algoritmayý çalýþtýrarak en kýsa yollarý hesaplar.
        public void Calistir(double[,] komsulukMatrisi, int baslangicDugumu)
        {
            this.baslangicDugumu = baslangicDugumu;
            mesafeler = new double[dugumSayisi];
            oncekiDugumler = new int[dugumSayisi];
            bool[] ziyaretEdildi = new bool[dugumSayisi];

            // 1. Baþlatma: Tüm mesafeleri 'sonsuz' olarak ayarla ve baþlangýç düðümünün
            // mesafesini 0 yap. Bu, algoritmanýn baþlangýç noktasýný belirler.
            for (int i = 0; i < dugumSayisi; i++)
            {
                mesafeler[i] = double.MaxValue;
                ziyaretEdildi[i] = false;
                oncekiDugumler[i] = -1;
            }
            mesafeler[baslangicDugumu] = 0;

            // 2. Ana Döngü: Tüm düðümler için en kýsa yol bulunana kadar devam et.
            for (int sayac = 0; sayac < dugumSayisi - 1; sayac++)
            {
                // Henüz ziyaret edilmemiþ düðümler arasýndan en kýsa mesafeye sahip olaný seç.
                int u = EnKisaMesafeDugumuBul(mesafeler, ziyaretEdildi);
                if (u == -1) break; // Ulaþýlacak düðüm kalmadýysa döngüyü sonlandýr.

                ziyaretEdildi[u] = true;

                // 3. Gevþetme (Relaxation): Seçilen düðüm (u) üzerinden komþu düðümlere (v)
                // daha kýsa bir yol olup olmadýðýný kontrol et.
                for (int v = 0; v < dugumSayisi; v++)
                {
                    double agirlik = komsulukMatrisi[u, v];
                    if (!ziyaretEdildi[v] && agirlik != double.PositiveInfinity && mesafeler[u] != double.MaxValue &&
                        mesafeler[u] + agirlik < mesafeler[v])
                    {
                        // Eðer daha kýsa bir yol bulunduysa, mesafeyi güncelle ve yolu kaydet.
                        mesafeler[v] = mesafeler[u] + agirlik;
                        oncekiDugumler[v] = u;
                    }
                }
            }
        }

        /// Ziyaret edilmemiþ düðümler arasýndan en düþük mesafeye sahip olaný bulur.
        /// Bu, algoritmanýn her adýmda hangi düðümü iþleyeceðini belirler.
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

        /// Algoritma çalýþtýktan sonra hesaplanan en kýsa yollarý ve mesafeleri konsola yazdýrýr.
        public void SonuclariYazdir()
        {
            if (mesafeler == null || oncekiDugumler == null)
            {
                Console.WriteLine("Calistir() metodunu çaðýrmayý unutma.");
                return;
            }

            Console.WriteLine("\nDijkstra Algoritmasý Sonuçlarý");
            Console.WriteLine("=================================");
            Console.WriteLine($"Baþlangýç Düðümü: {baslangicDugumu + 1}\n");

            for (int i = 0; i < dugumSayisi; i++)
            {
                Console.Write($"Hedef Düðüm: {i + 1} \t Maliyet: ");
                if (mesafeler[i] == double.MaxValue)
                {
                    Console.WriteLine("Ulaþýlamýyor");
                    continue;
                }
                Console.Write($"{mesafeler[i]} \t\t Yol: ");

                // Yolu yeniden oluþturmak için hedef düðümden geriye doðru gidilir.
                Stack<int> yol = new Stack<int>();
                int mevcutDugum = i;
                while (mevcutDugum != -1)
                {
                    yol.Push(mevcutDugum);
                    mevcutDugum = oncekiDugumler[mevcutDugum];
                }

                // Yýðýn (Stack) sayesinde yol, baþlangýçtan hedefe doðru yazdýrýlýr.
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

        /// Sadece belirli bir hedef düðüm için sonucu yazdýrýr
        public void SonuclariYazdir(int hedefDugum)
        {
            if (mesafeler == null || oncekiDugumler == null)
            {
                Console.WriteLine("Calistir() metodunu çaðýrmayý unutma.");
                return;
            }
            Console.WriteLine($"\nDijkstra Algoritmasý Sonucu (Baþlangýç: {baslangicDugumu + 1}, Hedef: {hedefDugum + 1})");
            Console.WriteLine("=================================");
            Console.Write($"Maliyet: ");
            if (mesafeler[hedefDugum] == double.MaxValue)
            {
                Console.WriteLine("Ulaþýlamýyor");
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