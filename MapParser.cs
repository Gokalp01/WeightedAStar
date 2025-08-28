using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ConsoleApp3
{
    /// <summary>
    /// .osm ve .xodr harita dosyalarını ayrıştırıp bir graf modeli oluşturan sınıf.
    /// </summary>
    public static class MapParser
    {
        /// <summary>
        /// Verilen dosya yolundaki harita dosyasını uzantısına göre ayrıştırır.
        /// </summary>
        public static GraphData Parse(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".osm":
                    return ParseOsm(filePath);
                case ".xodr":
                    return ParseXodr(filePath);
                default:
                    throw new NotSupportedException($"Desteklenmeyen dosya uzantısı: {extension}. Yalnızca .osm ve .xodr desteklenir.");
            }
        }

        #region OSM Parser Logic

        /// <summary>
        /// Bir .osm dosyasını ayrıştırır ve graf verisi oluşturur.
        /// </summary>
        private static GraphData ParseOsm(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);
            var ns = doc.Root.Name.Namespace;

            // 1. Adım: Tüm düğümleri (node) ve koordinatlarını oku.
            var nodes = doc.Descendants(ns + "node")
                .ToDictionary(
                    n => (string)n.Attribute("id"),
                    n => new Point(
                        (double)n.Attribute("lon"),
                        (double)n.Attribute("lat"),
                        n.Attribute("ele") != null ? (double)n.Attribute("ele") : 0
                    )
                );

            // 2. Adım: Yolları (way) oku.
            var ways = doc.Descendants(ns + "way")
                .Where(w => w.Elements(ns + "tag").Any(t => (string)t.Attribute("k") == "highway"));

            // 3. Adım: Graf yapısını oluştur.
            var nodeIdsInWays = new HashSet<string>();
            foreach (var way in ways)
            {
                foreach (var nd in way.Descendants(ns + "nd"))
                {
                    nodeIdsInWays.Add((string)nd.Attribute("ref"));
                }
            }

            var relevantNodeIds = nodeIdsInWays.ToList();
            var nodeIdToIndexMap = relevantNodeIds.Select((id, index) => new { id, index })
                                                  .ToDictionary(x => x.id, x => x.index);

            int nodeCount = relevantNodeIds.Count;
            var adjacencyMatrix = new double[nodeCount, nodeCount];
            var nodeCoordinates = new Point[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                nodeCoordinates[i] = nodes[relevantNodeIds[i]];
            }

            // 4. Adım: Kenarları (mesafeleri) hesapla.
            foreach (var way in ways)
            {
                var wayNodeRefs = way.Descendants(ns + "nd").Select(nd => (string)nd.Attribute("ref")).ToList();
                for (int i = 0; i < wayNodeRefs.Count - 1; i++)
                {
                    string id1 = wayNodeRefs[i];
                    string id2 = wayNodeRefs[i + 1];

                    if (nodeIdToIndexMap.ContainsKey(id1) && nodeIdToIndexMap.ContainsKey(id2))
                    {
                        int index1 = nodeIdToIndexMap[id1];
                        int index2 = nodeIdToIndexMap[id2];
                        Point p1 = nodeCoordinates[index1];
                        Point p2 = nodeCoordinates[index2];

                        // Basit Öklid mesafesi. Gerçek dünya için Haversine formülü daha doğrudur.
                        double distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

                        adjacencyMatrix[index1, index2] = distance;
                        adjacencyMatrix[index2, index1] = distance; // Yolu çift yönlü kabul et.
                    }
                }
            }

            return new GraphData(adjacencyMatrix, nodeCoordinates, nodeIdToIndexMap);
        }

        #endregion

        #region XODR Parser Logic

        /// <summary>
        /// Bir .xodr dosyasını ayrıştırır ve graf verisi oluşturur.
        /// Bu, temel bir ayrıştırıcıdır ve sadece düz yolları ve kavşakları dikkate alır.
        /// </summary>
        private static GraphData ParseXodr(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);

            var roads = doc.Descendants("road").ToList();
            var junctions = doc.Descendants("junction").ToList();

            // Düğümleri (nodes) kavşaklar ve yol bağlantı noktaları olarak tanımlayalım.
            // Her kavşak bir düğümdür. Her yolun başlangıç/bitişi de potansiyel bir düğümdür.
            var nodePoints = new Dictionary<string, Point>(); // Key: junctionId veya roadId_contactPoint
            var connections = new List<Tuple<string, string, double>>(); // fromNode, toNode, distance

            // 1. Adım: Yolları işle ve bağlantıları çıkar.
            foreach (var road in roads)
            {
                string roadId = (string)road.Attribute("id");
                double roadLength = (double)road.Attribute("length");
                string junctionId = (string)road.Attribute("junction");

                // Eğer yol bir kavşağa bağlı değilse, başlangıç ve bitiş noktaları oluştur.
                if (junctionId == "-1") // -1, kavşak yok demek.
                {
                    var planView = road.Element("planView").Element("geometry");
                    double startX = (double)planView.Attribute("x");
                    double startY = (double)planView.Attribute("y");
                    double hdg = (double)planView.Attribute("hdg"); // Yön (radyan)

                    string startNodeId = $"{roadId}_start";
                    string endNodeId = $"{roadId}_end";

                    nodePoints[startNodeId] = new Point(startX, startY, 0);
                    // Bitiş noktasını başlangıç, yön ve uzunluktan hesapla
                    nodePoints[endNodeId] = new Point(startX + roadLength * Math.Cos(hdg), startY + roadLength * Math.Sin(hdg), 0);
                    connections.Add(Tuple.Create(startNodeId, endNodeId, roadLength));
                }
            }

            // 2. Adım: Kavşakları işle.
            foreach (var junction in junctions)
            {
                string junctionId = (string)junction.Attribute("id");
                // Kavşağın merkezini bir düğüm olarak kabul edelim (basitleştirme).
                // Gerçekte, kavşak geometrisi daha karmaşıktır. Şimdilik (0,0) kabul edelim.
                // Daha doğru bir yaklaşım, bağlantılı yolların koordinatlarından bir merkez bulmaktır.
                nodePoints[junctionId] = new Point(0, 0, 0); // TODO: Kavşak koordinatını daha doğru hesapla.

                foreach (var connection in junction.Descendants("connection"))
                {
                    string incomingRoadId = (string)connection.Attribute("incomingRoad");
                    string connectingRoadId = (string)connection.Attribute("connectingRoad");
                    // Bu bağlantı, gelen yoldan çıkan yola bir geçiş olduğunu belirtir.
                    // Grafı oluştururken bu bilgiyi kullanırız.
                    var connectingRoad = roads.FirstOrDefault(r => (string)r.Attribute("id") == connectingRoadId);
                    if (connectingRoad != null)
                    {
                        double roadLength = (double)connectingRoad.Attribute("length");
                        connections.Add(Tuple.Create(junctionId, junctionId, roadLength)); // Basitleştirilmiş bağlantı
                    }
                }
            }

            // 3. Adım: Graf yapısını oluştur.
            var nodeIdList = nodePoints.Keys.ToList();
            var nodeIdToIndexMap = nodeIdList.Select((id, index) => new { id, index })
                                             .ToDictionary(x => x.id, x => x.index);

            int nodeCount = nodeIdList.Count;
            var adjacencyMatrix = new double[nodeCount, nodeCount];
            var nodeCoordinates = new Point[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                nodeCoordinates[i] = nodePoints[nodeIdList[i]];
            }

            foreach (var conn in connections)
            {
                if (nodeIdToIndexMap.ContainsKey(conn.Item1) && nodeIdToIndexMap.ContainsKey(conn.Item2))
                {
                    int index1 = nodeIdToIndexMap[conn.Item1];
                    int index2 = nodeIdToIndexMap[conn.Item2];
                    adjacencyMatrix[index1, index2] = conn.Item3;
                    adjacencyMatrix[index2, index1] = conn.Item3; // Çift yönlü kabul et
                }
            }

            return new GraphData(adjacencyMatrix, nodeCoordinates, nodeIdToIndexMap);
        }

        #endregion
    }
}