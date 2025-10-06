using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ConsoleApp3.Utils;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// .osm (OpenStreetMap) dosyalarýný ayrýþtýrarak bir graf modeli oluþturur.
    /// Gerçek OSM node'larý düðüm olarak alýnýr, ardýþýk node'lar arasý mesafe kenar olarak eklenir.
    /// </summary>
    public class OsmParser
    {
        /// <summary>
        /// Bir .osm dosyasýný ayrýþtýrýr ve graf verisi oluþturur.
        /// </summary>
        public GraphData Parse(string filePath)
        {
            Console.WriteLine("OSM dosyasý ayrýþtýrýlýyor...");
            XDocument doc = XDocument.Load(filePath);
            var ns = doc.Root.Name.Namespace;
            
            // 1. Adým: Tüm düðümleri (node) ve koordinatlarýný oku
            Console.WriteLine("OSM node'larý okunuyor...");
            var nodeElements = doc.Descendants(ns + "node").ToList();
            Console.WriteLine($"Bulunan toplam OSM node sayýsý: {nodeElements.Count}");
            
            var nodeCoords = new Dictionary<string, Point>();
            foreach (var node in nodeElements)
            {
                string nodeId = node.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(nodeId)) continue;
                
                double lon = double.Parse(node.Attribute("lon")?.Value ?? "0");
                double lat = double.Parse(node.Attribute("lat")?.Value ?? "0");
                double ele = node.Attribute("ele") != null ? double.Parse(node.Attribute("ele").Value) : 0;
                
                nodeCoords[nodeId] = new Point(lon, lat, ele);
            }
            Console.WriteLine($"Koordinat çýkarýlan node sayýsý: {nodeCoords.Count}");

            // 2. Adým: Sadece "highway" olarak etiketlenmiþ yollarý (way) bul
            var highways = doc.Descendants(ns + "way")
                .Where(w => w.Elements(ns + "tag").Any(t => (string)t.Attribute("k") == "highway"))
                .ToList();
            Console.WriteLine($"Bulunan highway sayýsý: {highways.Count}");

            // 3. Adým: Graf için gerekli düðüm kümesini bul (way'lerde kullanýlan node'lar)
            var usedNodeIds = new HashSet<string>();
            foreach (var way in highways)
            {
                foreach (var nd in way.Elements(ns + "nd"))
                {
                    string refId = nd.Attribute("ref")?.Value;
                    if (!string.IsNullOrEmpty(refId) && nodeCoords.ContainsKey(refId))
                    {
                        usedNodeIds.Add(refId);
                    }
                }
            }
            Console.WriteLine($"Highway'lerde kullanýlan benzersiz node sayýsý: {usedNodeIds.Count}");

            // 4. Adým: Kullanýlan node'larý düðüm listesine ekle
            var nodeList = usedNodeIds.ToList();
            var nodeIdToIndex = new Dictionary<string, int>();
            for (int i = 0; i < nodeList.Count; i++)
            {
                nodeIdToIndex[nodeList[i]] = i;
            }

            // 5. Adým: Komþuluk matrisini oluþtur (Infinity tabanlý)
            var adjacencyMatrix = new double[nodeList.Count, nodeList.Count];
            for (int i = 0; i < nodeList.Count; i++)
                for (int j = 0; j < nodeList.Count; j++)
                    adjacencyMatrix[i, j] = double.PositiveInfinity;

            var coordinates = new Point[nodeList.Count];
            for (int i = 0; i < nodeList.Count; i++)
            {
                coordinates[i] = nodeCoords[nodeList[i]];
            }

            // 6. Adým: Yollarý iþle ve baðlantýlarý ekle
            int oneWayCount = 0;
            int twoWayCount = 0;
            int reverseOnlyCount = 0;
            int totalEdgeCount = 0;

            foreach (var way in highways)
            {
                // Tek yönlü yol kontrolü
                var onewayTag = way.Elements(ns + "tag").FirstOrDefault(t => (string)t.Attribute("k") == "oneway");
                string onewayValue = onewayTag?.Attribute("v")?.Value;
                bool isOneWayForward = onewayValue == "yes" || onewayValue == "true" || onewayValue == "1";
                bool isOneWayReverse = onewayValue == "-1";
                bool isTwoWay = !isOneWayForward && !isOneWayReverse;
                
                // Ýstatistik
                if (isOneWayForward) oneWayCount++;
                else if (isOneWayReverse) reverseOnlyCount++;
                else twoWayCount++;

                // Way içindeki ardýþýk node'larý baðla
                var nodeRefs = way.Elements(ns + "nd")
                    .Select(nd => nd.Attribute("ref")?.Value)
                    .Where(id => !string.IsNullOrEmpty(id) && nodeCoords.ContainsKey(id))
                    .ToList();

                for (int i = 0; i < nodeRefs.Count - 1; i++)
                {
                    string fromId = nodeRefs[i];
                    string toId = nodeRefs[i + 1];

                    if (!nodeIdToIndex.TryGetValue(fromId, out int fromIdx) || 
                        !nodeIdToIndex.TryGetValue(toId, out int toIdx))
                        continue;

                    // Ýki node arasý mesafeyi Haversine ile hesapla (metre)
                    double distance = GeoUtils.HaversineDistance(
                        coordinates[fromIdx].Y, coordinates[fromIdx].X,  // lat, lon
                        coordinates[toIdx].Y, coordinates[toIdx].X);

                    if (isOneWayReverse)
                    {
                        // Sadece ters yön
                        adjacencyMatrix[toIdx, fromIdx] = distance;
                        totalEdgeCount++;
                    }
                    else if (isOneWayForward)
                    {
                        // Sadece ileri yön
                        adjacencyMatrix[fromIdx, toIdx] = distance;
                        totalEdgeCount++;
                    }
                    else // isTwoWay
                    {
                        // Çift yön
                        adjacencyMatrix[fromIdx, toIdx] = distance;
                        adjacencyMatrix[toIdx, fromIdx] = distance;
                        totalEdgeCount += 2;
                    }
                }
            }
            
            Console.WriteLine($"OSM graf oluþturuldu. Düðüm sayýsý: {nodeList.Count}, kenar sayýsý: {totalEdgeCount}");
            Console.WriteLine($"Yol tipi daðýlýmý: Ýki yönlü: {twoWayCount}, Tek yönlü ileri: {oneWayCount}, Tek yönlü geri: {reverseOnlyCount}");

            return new GraphData(adjacencyMatrix, coordinates, nodeIdToIndex);
        }
    }
}
