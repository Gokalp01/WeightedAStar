using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms; // Dosya diyaloğu için eklendi
using ConsoleApp3.Parsers;

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

    /// <summary>
    /// A* algoritması için isimlendirilmiş bir ağırlık yapılandırmasını temsil eder.
    /// </summary>
    public class AgirlikAyari
    {
        public string Isim { get; }
        public double Deger { get; }

        public AgirlikAyari(string isim, double deger)
        {
            Isim = isim;
            Deger = deger;
        }
    }

    internal class AnaProgram
    {
        // [STAThread] özniteliği, OpenFileDialog gibi Windows Formları
        // bileşenlerinin doğru çalışması için gereklidir.
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            while (true)
            {
                Console.WriteLine("\nHarita dosyası yüklemek için 'dy' yazıp Enter'a basın.");
                Console.WriteLine("Programdan çıkmak için 'exit' yazıp Enter'a basın.");
                string userInput = Console.ReadLine().Trim().ToLower();

                if (userInput == "exit")
                {
                    break; // Döngüyü sonlandır ve programdan çık.
                }
                
                if (userInput == "dy")
                {
                    string mapFilePath = SelectMapFile();

                    if (string.IsNullOrEmpty(mapFilePath))
                    {
                        Console.WriteLine("Dosya seçilmedi. Lütfen tekrar deneyin.");
                        continue; // Döngünün başına dön.
                    }

                    // Dosya seçildiyse, haritayı işle.
                    ProcessMapFile(mapFilePath);
                }
                else
                {
                    Console.WriteLine("Geçersiz komut. Lütfen 'dy' veya 'exit' yazın.");
                }
            }

            Console.WriteLine("\nProgram sonlandırıldı.");
        }

        /// <summary>
        /// Kullanıcının bir harita dosyası seçmesi için bir dosya seçim diyaloğu açar.
        /// </summary>
        /// <returns>Seçilen dosyanın tam yolu veya seçim iptal edilirse null.</returns>
        private static string SelectMapFile()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Harita Dosyası Seçin (.osm, .xodr)";
                openFileDialog.Filter = "Harita Dosyaları (*.osm;*.xodr)|*.osm;*.xodr|Tüm Dosyalar (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                var result = openFileDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }

        /// <summary>
        /// Seçilen harita dosyasını işler, grafı oluşturur ve algoritmaları çalıştırır.
        /// </summary>
        /// <param name="mapFilePath">İşlenecek harita dosyasının yolu.</param>
        private static void ProcessMapFile(string mapFilePath)
        {
            try
            {
                Console.WriteLine($"\n'{Path.GetFileName(mapFilePath)}' dosyasından graf verisi okunuyor...");
                GraphData graph = ParseMap(mapFilePath);
                Console.WriteLine($"Graf başarıyla oluşturuldu. Düğüm sayısı: {graph.NodeCount}");

                if (graph.NodeCount < 2)
                {
                    Console.WriteLine("Algoritmayı çalıştırmak için graf yeterli düğüme sahip değil.");
                    return;
                }

                // Kullanıcıdan başlangıç ve bitiş düğümleri (1-based) alınır
                int baslangicDugumu = GetNodeInput($"Başlangıç düğümünü girin (1 - {graph.NodeCount}): ", graph.NodeCount) - 1;
                int hedefDugumu = GetNodeInput($"Bitiş düğümünü girin (1 - {graph.NodeCount}): ", graph.NodeCount) - 1;

                Console.WriteLine($"\n--- Algoritmalar Çalıştırılıyor (Başlangıç: {baslangicDugumu + 1}, Hedef: {hedefDugumu + 1}) ---");

                // Dijkstra Algoritması (sadece seçilen hedefe odaklı çıktı)
                DijkstraAlgoritmasi dijkstra = new DijkstraAlgoritmasi(graph.NodeCount);
                dijkstra.Calistir(graph.AdjacencyMatrix, baslangicDugumu);
                int[] dijkstraPath = GetPathFromDijkstra(dijkstra, hedefDugumu);
                dijkstra.SonuclariYazdir(hedefDugumu);

                // Ağırlıklı A* Algoritması Testleri
                List<AgirlikAyari> testSenaryolari = new List<AgirlikAyari>
                {
                    new AgirlikAyari("Hızlı Rota (Heuristic Odaklı)", 0.5),
                    new AgirlikAyari("Dengeli Rota (Standart A*)", 1.0),
                    new AgirlikAyari("Güvenli Rota (Maliyet Odaklı)", 2.0)
                };

                foreach (var senaryo in testSenaryolari)
                {
                    Console.WriteLine($"\n--- Test Senaryosu: '{senaryo.Isim}' ---");
                    AStarAlgorithm astar = new AStarAlgorithm(graph.NodeCount, graph.NodeCoordinates, senaryo.Deger);
                    astar.Calistir(graph.AdjacencyMatrix, baslangicDugumu, hedefDugumu);
                    astar.SonuclariYazdir();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bir hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Verilen dosya yolundaki harita dosyasını uzantısına göre ayrıştırır.
        /// </summary>
        private static GraphData ParseMap(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".osm":
                    var osmParser = new OsmParser();
                    return osmParser.Parse(filePath);
                case ".xodr":
                    var xodrParser = new XodrParser();
                    return xodrParser.Parse(filePath);
                default:
                    throw new NotSupportedException($"Desteklenmeyen dosya uzantısı: {extension}. Yalnızca .osm ve .xodr desteklenir.");
            }
        }

        // Dijkstra yolunu çıkar
        private static int[] GetPathFromDijkstra(DijkstraAlgoritmasi dijkstra, int hedef)
        {
            var path = new List<int>();
            var onceki = typeof(DijkstraAlgoritmasi).GetField("oncekiDugumler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(dijkstra) as int[];
            int node = hedef;
            while (node != -1)
            {
                path.Insert(0, node);
                node = onceki[node];
            }
            return path.ToArray();
        }
        // A* yolunu çıkar
        private static int[] GetPathFromAStar(AStarAlgorithm astar, int hedef)
        {
            var path = new List<int>();
            var onceki = typeof(AStarAlgorithm).GetField("oncekiDugumler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(astar) as int[];
            int node = hedef;
            while (node != -1)
            {
                path.Insert(0, node);
                node = onceki[node];
            }
            return path.ToArray();
        }

        private static int GetNodeInput(string prompt, int maxNode)
        {
            int node;
            do
            {
                Console.Write(prompt);
                string input = Console.ReadLine();
                if (int.TryParse(input, out node) && node >= 1 && node <= maxNode)
                    return node;
                Console.WriteLine($"Geçersiz giriş. 1 ile {maxNode} arasında bir sayı girin.");
            } while (true);
        }
    }
}
