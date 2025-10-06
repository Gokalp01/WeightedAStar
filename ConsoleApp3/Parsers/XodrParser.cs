using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// OpenDRIVE (.xodr) dosyalar�n� ayr��t�rarak graf modeli olu�turur.
    /// Yol ba�lang��/biti� noktalar� ve kav�ak ba�lant�lar� d���m olarak modellenir.
    /// </summary>
    public class XodrParser
    {
        public GraphData Parse(string filePath)
        {
            Console.WriteLine("XODR dosyas� ayr��t�r�l�yor...");
            XDocument doc = XDocument.Load(filePath);
            var roads = doc.Descendants("road").ToList();
            var junctions = doc.Descendants("junction").ToList();

            Console.WriteLine($"Bulunan yol say�s�: {roads.Count}, kav�ak say�s�: {junctions.Count}");

            // Her yolun ba�lang�� ve biti� d���m ID'lerini belirle
            var roadEndpoints = new Dictionary<string, (string startId, string endId)>();
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId)) continue;

                // Her yola �zel iki d���m (ba�lang��/biti�) olu�tur
                string startId = $"road:{roadId}:start";
                string endId = $"road:{roadId}:end";
                roadEndpoints[roadId] = (startId, endId);
            }

            // D���m koordinatlar�
            var nodeCoords = new Dictionary<string, Point>();

            // Junction d���m koordinatlar�
            var junctionPoints = new Dictionary<string, Point>();
            foreach (var junc in junctions)
            {
                string juncId = junc.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(juncId)) continue;

                // Ge�ici olarak (0,0,0) atama
                junctionPoints[juncId] = new Point(0, 0, 0);
            }

            // Yollar�n ba�lang��/biti� koordinatlar�n� belirle
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;

                var (startId, endId) = roadEndpoints[roadId];
                
                // planView geometri ba�lang�� noktas�
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

                // Link bilgisi: �nceki ve sonraki yol/kav�aklar
                var link = road.Element("link");
                string predElementType = link?.Element("predecessor")?.Attribute("elementType")?.Value;
                string predElementId = link?.Element("predecessor")?.Attribute("elementId")?.Value;
                string succElementType = link?.Element("successor")?.Attribute("elementType")?.Value;
                string succElementId = link?.Element("successor")?.Attribute("elementId")?.Value;

                // Kav�ak koordinatlar� i�in noktalar� topla
                if (predElementType == "junction" && !string.IsNullOrEmpty(predElementId) && junctionPoints.ContainsKey(predElementId))
                {
                    // Ba�lang�� noktas�n� kav�a��n konumuna ekle (sonra ortalamas� al�nacak)
                    junctionPoints[predElementId] = new Point(
                        (junctionPoints[predElementId].X + startX) / 2,
                        (junctionPoints[predElementId].Y + startY) / 2, 0);
                }
                if (succElementType == "junction" && !string.IsNullOrEmpty(succElementId) && junctionPoints.ContainsKey(succElementId))
                {
                    // Biti� noktas�n� kav�a��n konumuna ekle
                    junctionPoints[succElementId] = new Point(
                        (junctionPoints[succElementId].X + endX) / 2,
                        (junctionPoints[succElementId].Y + endY) / 2, 0);
                }
            }

            // Junction koordinatlar�n� nodeCoords'a ekle
            foreach (var kvp in junctionPoints)
            {
                string juncId = $"junction:{kvp.Key}";
                nodeCoords[juncId] = kvp.Value;
            }

            // Ba�lant�lar
            var connections = new List<Tuple<string, string, double>>();

            // Yollar�n kendi i�indeki ba�lant�lar (start -> end)
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;

                double length = double.Parse(road.Attribute("length")?.Value ?? "0");
                string junctionId = road.Attribute("junction")?.Value;

                var (startId, endId) = roadEndpoints[roadId];

                // E�er yol bir kav�ak i�indeyse (junction != "-1"), o zaman kendi i�inde ba�lant� kurmayaca��z
                // ��nk� bunlar kav�ak i�i connecting yollar ve bunlar�n ba�lant�s� <junction> i�inden al�nacak
                if (junctionId == "-1" || junctionId == null)
                {
                    // �ki y�nl� kontrol (default: �ift y�n, herhangi bir lane direction:driving varsa)
                    bool bidirectional = true;
                    
                    // Lane (�erit) bilgisi: tek y�nl� m�?
                    var lanes = road.Element("lanes");
                    if (lanes != null)
                    {
                        var laneSection = lanes.Element("laneSection");
                        if (laneSection != null)
                        {
                            // Hem sa� hem de sol tarafta driving lane var m�?
                            bool hasRightDriving = laneSection.Element("right")?.Elements("lane")
                                                   .Any(l => l.Attribute("type")?.Value == "driving") ?? false;
                            bool hasLeftDriving = laneSection.Element("left")?.Elements("lane")
                                                   .Any(l => l.Attribute("type")?.Value == "driving") ?? false;
                            
                            // Tek tarafta driving lane varsa, tek y�nl�
                            bidirectional = hasRightDriving && hasLeftDriving;
                        }
                    }
                    
                    // Kenarlar� ekle
                    connections.Add(Tuple.Create(startId, endId, length)); // ileri y�n her zaman var
                    
                    if (bidirectional)
                    {
                        connections.Add(Tuple.Create(endId, startId, length)); // iki y�nl�yse geri y�n ekle
                    }
                }
            }

            // Yol ba�lant�lar� (predecessor/successor)
            foreach (var road in roads)
            {
                string roadId = road.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(roadId) || !roadEndpoints.ContainsKey(roadId)) continue;
                
                var link = road.Element("link");
                if (link == null) continue;
                
                double length = double.Parse(road.Attribute("length")?.Value ?? "0");
                var (roadStartId, roadEndId) = roadEndpoints[roadId];

                // Predecessor (�nceki) yol/kav�ak ba�lant�s�
                var predecessor = link.Element("predecessor");
                if (predecessor != null)
                {
                    string predType = predecessor.Attribute("elementType")?.Value;
                    string predId = predecessor.Attribute("elementId")?.Value;
                    string predContact = predecessor.Attribute("contactPoint")?.Value;
                    
                    if (predType == "road" && !string.IsNullOrEmpty(predId) && roadEndpoints.ContainsKey(predId))
                    {
                        var (predStartId, predEndId) = roadEndpoints[predId];
                        // Ba�lanma noktas� start ise, �nceki yolun ba�lang�c�na ba�lan�yor
                        string fromId = predContact == "start" ? predStartId : predEndId;
                        
                        // Bu yolun ba�lang�c�, �nceki yolun contactPoint'ine ba�lan�r
                        connections.Add(Tuple.Create(fromId, roadStartId, length));
                    }
                    else if (predType == "junction" && !string.IsNullOrEmpty(predId))
                    {
                        // Kav�ak -> yol ba�lang�c�
                        string junctionNodeId = $"junction:{predId}";
                        if (nodeCoords.ContainsKey(junctionNodeId))
                        {
                            connections.Add(Tuple.Create(junctionNodeId, roadStartId, length));
                        }
                    }
                }

                // Successor (sonraki) yol/kav�ak ba�lant�s�
                var successor = link.Element("successor");
                if (successor != null)
                {
                    string succType = successor.Attribute("elementType")?.Value;
                    string succId = successor.Attribute("elementId")?.Value;
                    string succContact = successor.Attribute("contactPoint")?.Value;
                    
                    if (succType == "road" && !string.IsNullOrEmpty(succId) && roadEndpoints.ContainsKey(succId))
                    {
                        var (succStartId, succEndId) = roadEndpoints[succId];
                        // Ba�lanma noktas� start ise, sonraki yolun ba�lang�c�na ba�lan�yor
                        string toId = succContact == "start" ? succStartId : succEndId;
                        
                        // Bu yolun biti�i, sonraki yolun contactPoint'ine ba�lan�r
                        connections.Add(Tuple.Create(roadEndId, toId, length));
                    }
                    else if (succType == "junction" && !string.IsNullOrEmpty(succId))
                    {
                        // Yol biti�i -> kav�ak
                        string junctionNodeId = $"junction:{succId}";
                        if (nodeCoords.ContainsKey(junctionNodeId))
                        {
                            connections.Add(Tuple.Create(roadEndId, junctionNodeId, length));
                        }
                    }
                }
            }

            // Kav�ak i�i ba�lant�lar
            int junctionConnectionCount = 0;
            foreach (var junction in junctions)
            {
                string junctionId = junction.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(junctionId)) continue;
                
                // Her <connection> i�in gelen yol ile connecting yol aras�ndaki ba�lant�
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
                    
                    // Gelen yolun son noktas� -> Ba�lant� yolunun ba�lang��/biti� noktas�
                    string fromId = inEndId; // Genellikle incomingRoad'un son noktas�
                    string toId = contactPoint == "start" ? connStartId : connEndId;
                    
                    connections.Add(Tuple.Create(fromId, toId, connectingLength));
                    junctionConnectionCount++;
                }
            }

            // �statistik
            int roadNodeCount = roadEndpoints.Count * 2; // Her yol i�in start ve end
            int junctionNodeCount = junctionPoints.Count;
            Console.WriteLine($"Olu�turulan d���m say�s�: {nodeCoords.Count} (yol: {roadNodeCount}, kav�ak: {junctionNodeCount})");
            Console.WriteLine($"Olu�turulan ba�lant� say�s�: {connections.Count} (kav�ak i�i: {junctionConnectionCount})");
            
            // Minimum, maksimum ve ortalama uzunluk de�erlerini g�ster
            if (connections.Count > 0)
            {
                double minLength = connections.Min(c => c.Item3);
                double maxLength = connections.Max(c => c.Item3);
                double avgLength = connections.Average(c => c.Item3);
                Console.WriteLine($"Kenar uzunluk istatistikleri: Min: {minLength:F2}, Maks: {maxLength:F2}, Ort: {avgLength:F2}");
            }
            
            // GraphData i�in haz�rl�k
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

            // Kom�uluk matrisi
            var adjMatrix = new double[allNodeIds.Count, allNodeIds.Count];
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                for (int j = 0; j < allNodeIds.Count; j++)
                {
                    adjMatrix[i, j] = double.PositiveInfinity;
                }
            }

            // Kenarlar� matrise ekle
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
