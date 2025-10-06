using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ConsoleApp3.Utils;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// .osm (OpenStreetMap) dosyalar�n� ayr��t�rarak bir graf modeli olu�turur.
    /// Ger�ek OSM node'lar� d���m olarak al�n�r, ard���k node'lar aras� mesafe kenar olarak eklenir.
    /// </summary>
    public class OsmParser
    {
        /// <summary>
        /// Bir .osm dosyas�n� ayr��t�r�r ve graf verisi olu�turur.
        /// </summary>
        public GraphData Parse(string filePath)
        {
            Console.WriteLine("OSM dosyas� ayr��t�r�l�yor...");
            XDocument doc = XDocument.Load(filePath);
            var ns = doc.Root.Name.Namespace;
            
            // 1. Ad�m: T�m d���mleri (node) ve koordinatlar�n� oku
            Console.WriteLine("OSM node'lar� okunuyor...");
            var nodeElements = doc.Descendants(ns + "node").ToList();
            Console.WriteLine($"Bulunan toplam OSM node say�s�: {nodeElements.Count}");
            
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
            Console.WriteLine($"Koordinat ��kar�lan node say�s�: {nodeCoords.Count}");

            // 2. Ad�m: Sadece "highway" olarak etiketlenmi� yollar� (way) bul
            var highways = doc.Descendants(ns + "way")
                .Where(w => w.Elements(ns + "tag").Any(t => (string)t.Attribute("k") == "highway"))
                .ToList();
            Console.WriteLine($"Bulunan highway say�s�: {highways.Count}");

            // 3. Ad�m: Graf i�in gerekli d���m k�mesini bul (way'lerde kullan�lan node'lar)
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
            Console.WriteLine($"Highway'lerde kullan�lan benzersiz node say�s�: {usedNodeIds.Count}");

            // 4. Ad�m: Kullan�lan node'lar� d���m listesine ekle
            var nodeList = usedNodeIds.ToList();
            var nodeIdToIndex = new Dictionary<string, int>();
            for (int i = 0; i < nodeList.Count; i++)
            {
                nodeIdToIndex[nodeList[i]] = i;
            }

            // 5. Ad�m: Kom�uluk matrisini olu�tur (Infinity tabanl�)
            var adjacencyMatrix = new double[nodeList.Count, nodeList.Count];
            for (int i = 0; i < nodeList.Count; i++)
                for (int j = 0; j < nodeList.Count; j++)
                    adjacencyMatrix[i, j] = double.PositiveInfinity;

            var coordinates = new Point[nodeList.Count];
            for (int i = 0; i < nodeList.Count; i++)
            {
                coordinates[i] = nodeCoords[nodeList[i]];
            }

            // 6. Ad�m: Yollar� i�le ve ba�lant�lar� ekle
            int oneWayCount = 0;
            int twoWayCount = 0;
            int reverseOnlyCount = 0;
            int totalEdgeCount = 0;

            foreach (var way in highways)
            {
                // Tek y�nl� yol kontrol�
                var onewayTag = way.Elements(ns + "tag").FirstOrDefault(t => (string)t.Attribute("k") == "oneway");
                string onewayValue = onewayTag?.Attribute("v")?.Value;
                bool isOneWayForward = onewayValue == "yes" || onewayValue == "true" || onewayValue == "1";
                bool isOneWayReverse = onewayValue == "-1";
                bool isTwoWay = !isOneWayForward && !isOneWayReverse;
                
                // �statistik
                if (isOneWayForward) oneWayCount++;
                else if (isOneWayReverse) reverseOnlyCount++;
                else twoWayCount++;

                // Way i�indeki ard���k node'lar� ba�la
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

                    // �ki node aras� mesafeyi Haversine ile hesapla (metre)
                    double distance = GeoUtils.HaversineDistance(
                        coordinates[fromIdx].Y, coordinates[fromIdx].X,  // lat, lon
                        coordinates[toIdx].Y, coordinates[toIdx].X);

                    if (isOneWayReverse)
                    {
                        // Sadece ters y�n
                        adjacencyMatrix[toIdx, fromIdx] = distance;
                        totalEdgeCount++;
                    }
                    else if (isOneWayForward)
                    {
                        // Sadece ileri y�n
                        adjacencyMatrix[fromIdx, toIdx] = distance;
                        totalEdgeCount++;
                    }
                    else // isTwoWay
                    {
                        // �ift y�n
                        adjacencyMatrix[fromIdx, toIdx] = distance;
                        adjacencyMatrix[toIdx, fromIdx] = distance;
                        totalEdgeCount += 2;
                    }
                }
            }
            
            Console.WriteLine($"OSM graf olu�turuldu. D���m say�s�: {nodeList.Count}, kenar say�s�: {totalEdgeCount}");
            Console.WriteLine($"Yol tipi da��l�m�: �ki y�nl�: {twoWayCount}, Tek y�nl� ileri: {oneWayCount}, Tek y�nl� geri: {reverseOnlyCount}");

            return new GraphData(adjacencyMatrix, coordinates, nodeIdToIndex);
        }
    }
}
