using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// OpenDRIVE (.xodr) dosyalarýný ayrýþtýrarak graf modeli oluþturur.
    /// Yol baþlangýç/bitiþ noktalarý ve kavþak baðlantýlarý düðüm olarak modellenir.
    /// </summary>
    public class XodrParser
    {
        public GraphData Parse(string filePath)
        {
            Console.WriteLine("XODR dosyasý ayrýþtýrýlýyor...");
            XDocument doc = XDocument.Load(filePath);
            var roads = doc.Descendants("road").ToList();
            var junctions = doc.Descendants("junction").ToList();

            Console.WriteLine($"Bulunan yol sayýsý: {roads.Count}, kavþak sayýsý: {junctions.Count}");

            // Her yolun baþlangýç ve bitiþ düðüm ID'lerini belirle
            var roadEndpoints = new Dictionary<string, (string startId, string endId)>();
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId)) continue;

                // Her yola özel iki düðüm (baþlangýç/bitiþ) oluþtur
                string startId = $"road:{roadId}:start";
                string endId = $"road:{roadId}:end";
                roadEndpoints[roadId] = (startId, endId);
            }

            // Düðüm koordinatlarý
            var nodeCoords = new Dictionary<string, Point>();

            // Junction düðüm koordinatlarý
            var junctionPoints = new Dictionary<string, Point>();
            foreach (var junc in junctions)
            {
                string juncId = junc.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(juncId)) continue;

                // Geçici olarak (0,0,0) atama
                junctionPoints[juncId] = new Point(0, 0, 0);
            }

            // Yollarýn baþlangýç/bitiþ koordinatlarýný belirle
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;

                var (startId, endId) = roadEndpoints[roadId];
                
                // planView geometri baþlangýç noktasý
                var planView = road.Element("planView");
                if (planView == null) continue;

                var geometries = planView.Elements("geometry").OrderBy(g => Double.Parse(g.Attribute("s")?.Value ?? "0")).ToList();
                if (geometries.Count == 0) continue;

                var firstGeom = geometries.First();
                var lastGeom = geometries.Last();

                double startX = double.Parse(firstGeom.Attribute("x")?.Value ?? "0");
                double startY = double.Parse(firstGeom.Attribute("y")?.Value ?? "0");
                double startHdg = double.Parse(firstGeom.Attribute("hdg")?.Value ?? "0");
                double startLen = double.Parse(firstGeom.Attribute("length")?.Value ?? "0");

                double lastS = double.Parse(lastGeom.Attribute("s")?.Value ?? "0");
                double lastLen = double.Parse(lastGeom.Attribute("length")?.Value ?? "0");
                double lastHdg = double.Parse(lastGeom.Attribute("hdg")?.Value ?? "0");
                double lastX = double.Parse(lastGeom.Attribute("x")?.Value ?? "0");
                double lastY = double.Parse(lastGeom.Attribute("y")?.Value ?? "0");

                double endX = lastX + lastLen * Math.Cos(lastHdg);
                double endY = lastY + lastLen * Math.Sin(lastHdg);

                nodeCoords[startId] = new Point(startX, startY, 0);
                nodeCoords[endId] = new Point(endX, endY, 0);

                // Link bilgisi: önceki ve sonraki yol/kavþaklar
                var link = road.Element("link");
                string predElementType = link?.Element("predecessor")?.Attribute("elementType")?.Value;
                string predElementId = link?.Element("predecessor")?.Attribute("elementId")?.Value;
                string succElementType = link?.Element("successor")?.Attribute("elementType")?.Value;
                string succElementId = link?.Element("successor")?.Attribute("elementId")?.Value;

                // Kavþak koordinatlarý için noktalarý topla
                if (predElementType == "junction" && !string.IsNullOrEmpty(predElementId) && junctionPoints.ContainsKey(predElementId))
                {
                    // Baþlangýç noktasýný kavþaðýn konumuna ekle (sonra ortalamasý alýnacak)
                    junctionPoints[predElementId] = new Point(
                        (junctionPoints[predElementId].X + startX) / 2,
                        (junctionPoints[predElementId].Y + startY) / 2, 0);
                }
                if (succElementType == "junction" && !string.IsNullOrEmpty(succElementId) && junctionPoints.ContainsKey(succElementId))
                {
                    // Bitiþ noktasýný kavþaðýn konumuna ekle
                    junctionPoints[succElementId] = new Point(
                        (junctionPoints[succElementId].X + endX) / 2,
                        (junctionPoints[succElementId].Y + endY) / 2, 0);
                }
            }

            // Junction koordinatlarýný nodeCoords'a ekle
            foreach (var kvp in junctionPoints)
            {
                string juncId = $"junction:{kvp.Key}";
                nodeCoords[juncId] = kvp.Value;
            }

            // Baðlantýlar
            var connections = new List<Tuple<string, string, double>>();

            // Yollarýn kendi içindeki baðlantýlar (start -> end)
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;

                double length = double.Parse(road.Attribute("length")?.Value ?? "0");
                string junctionId = road.Attribute("junction")?.Value;

                var (startId, endId) = roadEndpoints[roadId];

                // Eðer yol bir kavþak içindeyse (junction != "-1"), o zaman kendi içinde baðlantý kurmayacaðýz
                // Çünkü bunlar kavþak içi connecting yollar ve bunlarýn baðlantýsý <junction> içinden alýnacak
                if (junctionId == "-1" || junctionId == null)
                {
                    // Ýki yönlü kontrol (default: çift yön, herhangi bir lane direction:driving varsa)
                    bool bidirectional = true;
                    
                    // Lane (þerit) bilgisi: tek yönlü mü?
                    var lanes = road.Element("lanes");
                    if (lanes != null)
                    {
                        var laneSection = lanes.Element("laneSection");
                        if (laneSection != null)
                        {
                            // Hem sað hem de sol tarafta driving lane var mý?
                            bool hasRightDriving = laneSection.Element("right")?.Elements("lane")
                                                   .Any(l => l.Attribute("type")?.Value == "driving") ?? false;
                            bool hasLeftDriving = laneSection.Element("left")?.Elements("lane")
                                                   .Any(l => l.Attribute("type")?.Value == "driving") ?? false;
                            
                            // Tek tarafta driving lane varsa, tek yönlü
                            bidirectional = hasRightDriving && hasLeftDriving;
                        }
                    }
                    
                    // Kenarlarý ekle
                    connections.Add(Tuple.Create(startId, endId, length)); // ileri yön her zaman var
                    
                    if (bidirectional)
                    {
                        connections.Add(Tuple.Create(endId, startId, length)); // iki yönlüyse geri yön ekle
                    }
                }
            }

            // Yol baðlantýlarý (predecessor/successor)
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;
                
                var link = road.Element("link");
                if (link == null) continue;
                
                double length = double.Parse(road.Attribute("length")?.Value ?? "0");
                var (roadStartId, roadEndId) = roadEndpoints[roadId];

                // Predecessor (önceki) yol/kavþak baðlantýsý
                var predecessor = link.Element("predecessor");
                if (predecessor != null)
                {
                    string predType = predecessor.Attribute("elementType")?.Value;
                    string predId = predecessor.Attribute("elementId")?.Value;
                    string predContact = predecessor.Attribute("contactPoint")?.Value;
                    
                    if (predType == "road" && !string.IsNullOrEmpty(predId) && roadEndpoints.ContainsKey(predId))
                    {
                        var (predStartId, predEndId) = roadEndpoints[predId];
                        // Baðlanma noktasý start ise, önceki yolun baþlangýcýna baðlanýyor
                        string fromId = predContact == "start" ? predStartId : predEndId;
                        
                        // Bu yolun baþlangýcý, önceki yolun contactPoint'ine baðlanýr
                        connections.Add(Tuple.Create(fromId, roadStartId, length));
                    }
                    else if (predType == "junction" && !string.IsNullOrEmpty(predId))
                    {
                        // Kavþak -> yol baþlangýcý
                        string junctionNodeId = $"junction:{predId}";
                        if (nodeCoords.ContainsKey(junctionNodeId))
                        {
                            connections.Add(Tuple.Create(junctionNodeId, roadStartId, length));
                        }
                    }
                }

                // Successor (sonraki) yol/kavþak baðlantýsý
                var successor = link.Element("successor");
                if (successor != null)
                {
                    string succType = successor.Attribute("elementType")?.Value;
                    string succId = successor.Attribute("elementId")?.Value;
                    string succContact = successor.Attribute("contactPoint")?.Value;
                    
                    if (succType == "road" && !string.IsNullOrEmpty(succId) && roadEndpoints.ContainsKey(succId))
                    {
                        var (succStartId, succEndId) = roadEndpoints[succId];
                        // Baðlanma noktasý start ise, sonraki yolun baþlangýcýna baðlanýyor
                        string toId = succContact == "start" ? succStartId : succEndId;
                        
                        // Bu yolun bitiþi, sonraki yolun contactPoint'ine baðlanýr
                        connections.Add(Tuple.Create(roadEndId, toId, length));
                    }
                    else if (succType == "junction" && !string.IsNullOrEmpty(succId))
                    {
                        // Yol bitiþi -> kavþak
                        string junctionNodeId = $"junction:{succId}";
                        if (nodeCoords.ContainsKey(junctionNodeId))
                        {
                            connections.Add(Tuple.Create(roadEndId, junctionNodeId, length));
                        }
                    }
                }
            }

            // Kavþak içi baðlantýlar
            int junctionConnectionCount = 0;
            foreach (var junction in junctions)
            {
                string junctionId = junction.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(junctionId)) continue;
                
                // Her <connection> için gelen yol ile connecting yol arasýndaki baðlantý
                foreach (var connection in junction.Elements("connection"))
                {
                    string incomingRoadId = connection.Attribute("incomingRoad")?.Value;
                    string connectingRoadId = connection.Attribute("connectingRoad")?.Value;
                    string contactPoint = connection.Attribute("contactPoint")?.Value;
                    
                    if (string.IsNullOrEmpty(incomingRoadId) || string.IsNullOrEmpty(connectingRoadId) ||
                        !roadEndpoints.ContainsKey(incomingRoadId) || !roadEndpoints.ContainsKey(connectingRoadId))
                        continue;

                    var connectingRoad = roads.FirstOrDefault(r => r.Attribute("id")?.Value == connectingRoadId);
                    if (connectingRoad == null) continue;
                    double connectingLength = double.Parse(connectingRoad.Attribute("length")?.Value ?? "0");

                    var (inStartId, inEndId) = roadEndpoints[incomingRoadId];
                    var (connStartId, connEndId) = roadEndpoints[connectingRoadId];
                    
                    // Gelen yolun son noktasý -> Baðlantý yolunun baþlangýç/bitiþ noktasý
                    string fromId = inEndId; // Genellikle incomingRoad'un son noktasý
                    string toId = contactPoint == "start" ? connStartId : connEndId;
                    
                    connections.Add(Tuple.Create(fromId, toId, connectingLength));
                    junctionConnectionCount++;
                }
            }

            // Ýstatistik
            int roadNodeCount = roadEndpoints.Count * 2; // Her yol için start ve end
            int junctionNodeCount = junctionPoints.Count;
            Console.WriteLine($"Oluþturulan düðüm sayýsý: {nodeCoords.Count} (yol: {roadNodeCount}, kavþak: {junctionNodeCount})");
            Console.WriteLine($"Oluþturulan baðlantý sayýsý: {connections.Count} (kavþak içi: {junctionConnectionCount})");
            
            // Minimum, maksimum ve ortalama uzunluk deðerlerini göster
            if (connections.Count > 0)
            {
                double minLength = connections.Min(c => c.Item3);
                double maxLength = connections.Max(c => c.Item3);
                double avgLength = connections.Average(c => c.Item3);
                Console.WriteLine($"Kenar uzunluk istatistikleri: Min: {minLength:F2}, Maks: {maxLength:F2}, Ort: {avgLength:F2}");
            }
            
            // GraphData için hazýrlýk
            var allNodeIds = nodeCoords.Keys.ToList();
            var nodeIdToIndex = new Dictionary<string, int>();
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                nodeIdToIndex[allNodeIds[i]] = i;
            }

            var nodeCoordinates = new Point[allNodeIds.Count];
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                nodeCoordinates[i] = nodeCoords[allNodeIds[i]];
            }

            // Komþuluk matrisi
            var adjMatrix = new double[allNodeIds.Count, allNodeIds.Count];
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                for (int j = 0; j < allNodeIds.Count; j++)
                {
                    adjMatrix[i, j] = double.PositiveInfinity;
                }
            }

            // Kenarlarý matrise ekle
            foreach (var conn in connections)
            {
                if (nodeIdToIndex.TryGetValue(conn.Item1, out int fromIdx) && 
                    nodeIdToIndex.TryGetValue(conn.Item2, out int toIdx))
                {
                    adjMatrix[fromIdx, toIdx] = conn.Item3;
                }
            }

            return new GraphData(adjMatrix, nodeCoordinates, nodeIdToIndex);
        }
    }
}
