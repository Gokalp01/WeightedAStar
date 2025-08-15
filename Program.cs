using System;
using System.Collections.Generic;

namespace ConsoleApp3
{
    public struct Point
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    internal class AnaProgram
    {
        static void Main(string[] args)
        {
            
            int dugumSayisi = 6;
            int baslangicDugumu = 0; // Başlangıç düğümü
            int hedefDugumu = 4;     // Hedef düğümü

            double[,] komsulukMatrisi = new double[,]
            {
               //komşuluk matrisi (Maliyet)
                 { 0,   8,   3,   6,   0,   0 },
                 { 8,   0,   4,   5,   5,   7 },
                 { 3,   4,   0,   0,   0,   0 },
                 { 6,   5,   0,   0,   0,   6 },
                 { 0,   5,   0,   0,   0,   0 },
                 { 0,   7,   0,   6,   0,   0 }
            };

            // Düğüm koordinatları 
            Point[] dugumKoordinatlari = new Point[]
            {
                new Point(28.7, 41.2, 0), new Point(33.0, 40.1, 0), new Point(27.1, 38.2, 0),
                new Point(30.8, 36.9, 0), new Point(39.7, 40.9, 0), new Point(40.2, 37.9, 0)
            };

            // Dijkstra algoritması testi
            Console.WriteLine("--- Dijkstra Algoritması ---");
            DijkstraAlgoritmasi dijkstra = new DijkstraAlgoritmasi(dugumSayisi);
            dijkstra.Calistir(komsulukMatrisi, baslangicDugumu);
            dijkstra.SonuclariYazdir();

            Console.WriteLine("\n--- Ağırlıklı A* Algoritma Testleri ---");

            // Ağırlıklı A* algoritması için w değerleri
            List<double> testAgirliklari = new List<double> { 0.5, 1.0, 2.0 };

            // Bir döngü ile her bir ağırlık için algoritmayı çalıştırıyoruz.
            foreach (var agirlik in testAgirliklari)
            {
                AStarAlgorithm astar = new AStarAlgorithm(dugumSayisi, dugumKoordinatlari, agirlik);
                astar.Calistir(komsulukMatrisi, baslangicDugumu, hedefDugumu);
                astar.SonuclariYazdir();
            }

            // Programın sonlandırılması için kullanıcıdan giriş bekleniyor.
            Console.WriteLine("\nProgramı sonlandırmak için bir tuşa basın");
            Console.ReadKey();
        }
    }
}
