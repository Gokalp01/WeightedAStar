using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// OpenDRIVE (.xodr) dosyalar�n� ayr��t�rarak graf modeli olu�turur.
    /// Yol ba�lang��/biti� noktalar� ve kav�ak ba�lant�lar� d���m olarak modellenir.
    /// </summary>
    public class XodrParser
    {
        // Sabitler - Magic string'leri �nlemek i�in
        private const string ROAD_PREFIX = "road:";
        private const string JUNCTION_PREFIX = "junction:";
        private const string START_SUFFIX = ":start";
        private const string END_SUFFIX = ":end";
        private const string JUNCTION_ID_DEFAULT = "-1";
        private const double LINK_CONNECTION_DISTANCE = 0.1; // D���mler aras� minimal mesafe

        private readonly CultureInfo culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Yol u� noktalar�n� ve koordinatlar�n� temsil eder
        /// </summary>
        private class RoadInfo
        {
            public string RoadId { get; set; }
            public string StartNodeId { get; set; }
            public string EndNodeId { get; set; }
            public Point StartPoint { get; set; }
            public Point EndPoint { get; set; }
            public double Length { get; set; }
            public string JunctionId { get; set; }
            public bool IsBidirectional { get; set; }
            public RoadLinkInfo LinkInfo { get; set; }
        }

        /// <summary>
        /// Yol ba�lant� bilgilerini saklar
        /// </summary>
        private class RoadLinkInfo
        {
            public LinkElementInfo Predecessor { get; set; }
            public LinkElementInfo Successor { get; set; }
        }

        private class LinkElementInfo
        {
            public string ElementType { get; set; }
            public String ElementId { get; set; }
            public string ContactPoint { get; set; }
        }

        public GraphData Parse(string filePath)
        {
            Console.WriteLine("XODR dosyas� ayr��t�r�l�yor...");
            XDocument doc = XDocument.Load(filePath);
            var roads = doc.Descendants("road").ToList();
            var junctions = doc.Descendants("junction").ToList();

            Console.WriteLine($"Bulunan yol say�s�: {roads.Count}, kav�ak say�s�: {junctions.Count}");

            // 1. Ad�m: T�m yol bilgilerini tek ge�i�te topla
            var roadInfos = ParseRoadInformation(roads);
            
            // 2. Ad�m: Kav�ak koordinatlar�n� hesapla
            var junctionCoords = CalculateJunctionCoordinates(roadInfos, junctions, roads);
            
            // 3. Ad�m: T�m d���m koordinatlar�n� birle�tir
            var nodeCoords = BuildNodeCoordinatesMap(roadInfos, junctionCoords);
            
            // 4. Ad�m: Ba�lant�lar� olu�tur
            var connections = BuildConnections(roadInfos, roads, junctions, nodeCoords);
            
            // 5. Ad�m: �statistikleri yazd�r
            PrintStatistics(roadInfos.Count, junctionCoords.Count, connections);
            
            // 6. Ad�m: GraphData nesnesini olu�tur ve d�nd�r
            return CreateGraphData(nodeCoords, connections);
        }

        /// <summary>
        /// T�m yol bilgilerini tek ge�i�te ayr��t�r�r (performans optimizasyonu)
        /// </summary>
        private List<RoadInfo> ParseRoadInformation(List<XElement> roads)
        {
            var roadInfos = new List<RoadInfo>(roads.Count);

            foreach (var road in roads)
            {
                string roadId = GetAttributeValue(road, "id");
                if (string.IsNullOrEmpty(roadId)) continue;

                var planView = road.Element("planView");
                if (planView == null) continue;

                var geometries = planView.Elements("geometry")
                    .OrderBy(g => ParseDouble(g, "s"))
                    .ToList();
                
                if (geometries.Count == 0) continue;

                var roadInfo = new RoadInfo
                {
                    RoadId = roadId,
                    StartNodeId = $"{ROAD_PREFIX}{roadId}{START_SUFFIX}",
                    EndNodeId = $"{ROAD_PREFIX}{roadId}{END_SUFFIX}",
                    Length = ParseDouble(road, "length"),
                    JunctionId = GetAttributeValue(road, "junction"),
                    IsBidirectional = IsBidirectionalRoad(road)
                };

                // Ba�lang�� ve biti� koordinatlar�n� hesapla
                Point startPt, endPt;
                CalculateRoadEndpoints(geometries, out startPt, out endPt);
                roadInfo.StartPoint = startPt;
                roadInfo.EndPoint = endPt;

                // Link bilgilerini parse et
                var link = road.Element("link");
                if (link != null)
                {
                    roadInfo.LinkInfo = ParseRoadLinkInfo(link);
                }
                
                roadInfos.Add(roadInfo);
            }

            return roadInfos;
        }

        /// <summary>
        /// Yol ba�lant� bilgilerini parse eder
        /// </summary>
        private RoadLinkInfo ParseRoadLinkInfo(XElement linkElement)
        {
            var linkInfo = new RoadLinkInfo();

            var pred = linkElement.Element("predecessor");
            if (pred != null)
            {
                linkInfo.Predecessor = new LinkElementInfo
                {
                    ElementType = GetAttributeValue(pred, "elementType"),
                    ElementId = GetAttributeValue(pred, "elementId"),
                    ContactPoint = GetAttributeValue(pred, "contactPoint")
                };
            }

            var succ = linkElement.Element("successor");
            if (succ != null)
            {
                linkInfo.Successor = new LinkElementInfo
                {
                    ElementType = GetAttributeValue(succ, "elementType"),
                    ElementId = GetAttributeValue(succ, "elementId"),
                    ContactPoint = GetAttributeValue(succ, "contactPoint")
                };
            }

            return linkInfo;
        }

        /// <summary>
        /// Geometrilerden yolun ba�lang�� ve biti� noktalar�n� hesaplar
        /// </summary>
        private void CalculateRoadEndpoints(List<XElement> geometries, out Point startPoint, out Point endPoint)
        {
            var firstGeom = geometries.First();
            double startX = ParseDouble(firstGeom, "x");
            double startY = ParseDouble(firstGeom, "y");
            startPoint = new Point(startX, startY, 0);

            var lastGeom = geometries.Last();
            double lastX = ParseDouble(lastGeom, "x");
            double lastY = ParseDouble(lastGeom, "y");
            double lastLen = ParseDouble(lastGeom, "length");
            double lastHdg = ParseDouble(lastGeom, "hdg");

            // Geometri tipine g�re biti� noktas�n� hesapla
            double endX, endY;
            
            if (lastGeom.Element("line") != null)
            {
                // D�z �izgi i�in basit hesaplama
                endX = lastX + lastLen * Math.Cos(lastHdg);
                endY = lastY + lastLen * Math.Sin(lastHdg);
            }
            else if (lastGeom.Element("arc") != null)
            {
                // Arc i�in e�ri hesaplama
                var arc = lastGeom.Element("arc");
                double curvature = ParseDoubleFromAttribute(arc, "curvature");
                
                if (Math.Abs(curvature) > 1e-10)
                {
                    double radius = 1.0 / curvature;
                    double angle = lastLen * curvature;
                    
                    endX = lastX + radius * (Math.Sin(lastHdg + angle) - Math.Sin(lastHdg));
                    endY = lastY - radius * (Math.Cos(lastHdg + angle) - Math.Cos(lastHdg));
                }
                else
                {
                    // Curvature s�f�ra yak�nsa d�z �izgi gibi davran
                    endX = lastX + lastLen * Math.Cos(lastHdg);
                    endY = lastY + lastLen * Math.Sin(lastHdg);
                }
            }
            else
            {
                // Di�er geometri tipleri i�in basit tahmin
                endX = lastX + lastLen * Math.Cos(lastHdg);
                endY = lastY + lastLen * Math.Sin(lastHdg);
            }

            endPoint = new Point(endX, endY, 0);
        }

        /// <summary>
        /// Yolun �ift y�nl� olup olmad���n� kontrol eder
        /// </summary>
        private bool IsBidirectionalRoad(XElement road)
        {
            var lanes = road.Element("lanes");
            if (lanes == null) return true; // Default: �ift y�nl�

            var laneSection = lanes.Element("laneSection");
            if (laneSection == null) return true;

            // Hem sa� hem de sol tarafta driving lane var m�?
            bool hasRightDriving = laneSection.Element("right")?
                .Elements("lane")
                .Any(l => GetAttributeValue(l, "type") == "driving") ?? false;
            
            bool hasLeftDriving = laneSection.Element("left")?
                .Elements("lane")
                .Any(l => GetAttributeValue(l, "type") == "driving") ?? false;

            return hasRightDriving && hasLeftDriving;
        }

        /// <summary>
        /// Kav�ak koordinatlar�n� ba�l� yollar�n ortalama konumlar�ndan hesaplar
        /// </summary>
        private Dictionary<string, Point> CalculateJunctionCoordinates(
            List<RoadInfo> roadInfos, 
            List<XElement> junctions,
            List<XElement> roads)
        {
            var junctionPoints = new Dictionary<string, List<Point>>();
            var roadInfoMap = roadInfos.ToDictionary(r => r.RoadId);

            // Her kav�ak i�in ba�l� yollar�n noktalar�n� topla
            foreach (var junc in junctions)
            {
                string juncId = GetAttributeValue(junc, "id");
                if (string.IsNullOrEmpty(juncId)) continue;
                junctionPoints[juncId] = new List<Point>();
            }

            // RoadInfo'dan link bilgilerini kullanarak kav�ak noktalar�n� topla
            foreach (var roadInfo in roadInfos)
            {
                if (roadInfo.LinkInfo != null)
                {
                    // Predecessor kav�ak m�?
                    if (roadInfo.LinkInfo.Predecessor != null && 
                        roadInfo.LinkInfo.Predecessor.ElementType == "junction")
                    {
                        string juncId = roadInfo.LinkInfo.Predecessor.ElementId;
                        if (junctionPoints.ContainsKey(juncId))
                            junctionPoints[juncId].Add(roadInfo.StartPoint);
                    }

                    // Successor kav�ak m�?
                    if (roadInfo.LinkInfo.Successor != null && 
                        roadInfo.LinkInfo.Successor.ElementType == "junction")
                    {
                        string juncId = roadInfo.LinkInfo.Successor.ElementId;
                        if (junctionPoints.ContainsKey(juncId))
                            junctionPoints[juncId].Add(roadInfo.EndPoint);
                    }
                }
            }

            // Her kav�ak i�in ortalama koordinat hesapla
            var result = new Dictionary<string, Point>();
            foreach (var kvp in junctionPoints)
            {
                if (kvp.Value.Count > 0)
                {
                    double avgX = kvp.Value.Average(p => p.X);
                    double avgY = kvp.Value.Average(p => p.Y);
                    result[kvp.Key] = new Point(avgX, avgY, 0);
                }
                else
                {
                    // Hi� ba�lant� yoksa varsay�lan de�er
                    result[kvp.Key] = new Point(0, 0, 0);
                }
            }

            return result;
        }

        /// <summary>
        /// T�m d���m koordinatlar�n� tek bir dictionary'de birle�tirir
        /// </summary>
        private Dictionary<string, Point> BuildNodeCoordinatesMap(List<RoadInfo> roadInfos, Dictionary<string, Point> junctionCoords)
        {
            // Toplam d���m say�s�n� �nceden bil (memory allocation optimizasyonu)
            int totalNodes = (roadInfos.Count * 2) + junctionCoords.Count;
            var nodeCoords = new Dictionary<string, Point>(totalNodes);

            // Yol d���mlerini ekle
            foreach (var roadInfo in roadInfos)
            {
                nodeCoords[roadInfo.StartNodeId] = roadInfo.StartPoint;
                nodeCoords[roadInfo.EndNodeId] = roadInfo.EndPoint;
            }

            // Kav�ak d���mlerini ekle
            foreach (var kvp in junctionCoords)
            {
                nodeCoords[$"{JUNCTION_PREFIX}{kvp.Key}"] = kvp.Value;
            }

            return nodeCoords;
        }

        /// <summary>
        /// T�m ba�lant�lar� olu�turur (yol i�i, yollar aras�, kav�ak i�i)
        /// </summary>
        private List<Tuple<string, string, double>> BuildConnections(
            List<RoadInfo> roadInfos, 
            List<XElement> roads,
            List<XElement> junctions,
            Dictionary<string, Point> nodeCoords)
        {
            var connections = new List<Tuple<string, string, double>>();
            var roadInfoMap = roadInfos.ToDictionary(r => r.RoadId);

            // 1. Yol i�i ba�lant�lar (start -> end)
            AddIntraRoadConnections(connections, roadInfos);

            // 2. Yollar aras� ba�lant�lar (predecessor/successor)
            AddInterRoadConnections(connections, roadInfos, roadInfoMap, nodeCoords);

            // 3. Kav�ak i�i ba�lant�lar
            AddJunctionConnections(connections, junctions, roadInfoMap, roads);

            return connections;
        }

        /// <summary>
        /// Yol i�i ba�lant�lar� ekler (start -> end)
        /// </summary>
        private void AddIntraRoadConnections(List<Tuple<string, string, double>> connections, List<RoadInfo> roadInfos)
        {
            foreach (var roadInfo in roadInfos)
            {
                // Kav�ak i�indeki ba�lant� yollar�n� atla (bunlar junction'dan i�lenecek)
                if (!string.IsNullOrEmpty(roadInfo.JunctionId) && roadInfo.JunctionId != JUNCTION_ID_DEFAULT)
                {
                    continue;
                }

                // �leri y�n
                connections.Add(Tuple.Create(roadInfo.StartNodeId, roadInfo.EndNodeId, roadInfo.Length));

                // Geri y�n (�ift y�nl�yse)
                if (roadInfo.IsBidirectional)
                {
                    connections.Add(Tuple.Create(roadInfo.EndNodeId, roadInfo.StartNodeId, roadInfo.Length));
                }
            }
        }

        /// <summary>
        /// Yollar aras� ba�lant�lar� ekler (link predecessor/successor)
        /// </summary>
        private void AddInterRoadConnections(
            List<Tuple<string, string, double>> connections,
            List<RoadInfo> roadInfos,
            Dictionary<string, RoadInfo> roadInfoMap,
            Dictionary<string, Point> nodeCoords)
        {
            foreach (var roadInfo in roadInfos)
            {
                if (roadInfo.LinkInfo == null) continue;

                if (roadInfo.LinkInfo.Predecessor != null)
                {
                    ProcessLinkElementInfo(
                        roadInfo.LinkInfo.Predecessor,
                        roadInfo.StartNodeId,
                        LINK_CONNECTION_DISTANCE,
                        roadInfoMap,
                        nodeCoords,
                        connections,
                        true);
                }

                if (roadInfo.LinkInfo.Successor != null)
                {
                    ProcessLinkElementInfo(
                        roadInfo.LinkInfo.Successor,
                        roadInfo.EndNodeId,
                        LINK_CONNECTION_DISTANCE,
                        roadInfoMap,
                        nodeCoords,
                        connections,
                        false);
                }
            }
        }

        /// <summary>
        /// Link element'ini i�ler ve uygun ba�lant�y� ekler
        /// </summary>
        private void ProcessLinkElementInfo(
            LinkElementInfo linkInfo,
            string currentNodeId,
            double connectionDistance,
            Dictionary<string, RoadInfo> roadInfoMap,
            Dictionary<string, Point> nodeCoords,
            List<Tuple<string, string, double>> connections,
            bool isPredecessor)
        {
            string elementType = linkInfo.ElementType;
            string elementId = linkInfo.ElementId;
            string contactPoint = linkInfo.ContactPoint;

            if (string.IsNullOrEmpty(elementId)) return;

            if (elementType == "road" && roadInfoMap.ContainsKey(elementId))
            {
                var linkedRoadInfo = roadInfoMap[elementId];
                
                string linkedNodeId;
                if (string.IsNullOrEmpty(contactPoint))
                {
                    linkedNodeId = isPredecessor ? linkedRoadInfo.EndNodeId : linkedRoadInfo.StartNodeId;
                }
                else
                {
                    linkedNodeId = contactPoint == "start" ? linkedRoadInfo.StartNodeId : linkedRoadInfo.EndNodeId;
                }
                
                if (isPredecessor)
                {
                    connections.Add(Tuple.Create(linkedNodeId, currentNodeId, connectionDistance));
                }
                else
                {
                    connections.Add(Tuple.Create(currentNodeId, linkedNodeId, connectionDistance));
                }
            }
            else if (elementType == "junction")
            {
                string junctionNodeId = $"{JUNCTION_PREFIX}{elementId}";
                if (nodeCoords.ContainsKey(junctionNodeId))
                {
                    if (isPredecessor)
                    {
                        connections.Add(Tuple.Create(junctionNodeId, currentNodeId, connectionDistance));
                    }
                    else
                    {
                        connections.Add(Tuple.Create(currentNodeId, junctionNodeId, connectionDistance));
                    }
                }
            }
        }

        /// <summary>
        /// Kav�ak i�i ba�lant�lar� ekler
        /// </summary>
        private void AddJunctionConnections(
            List<Tuple<string, string, double>> connections,
            List<XElement> junctions,
            Dictionary<string, RoadInfo> roadInfoMap,
            List<XElement> roads)
        {
            int junctionConnectionCount = 0;
            int skippedConnections = 0;

            foreach (var junction in junctions)
            {
                string junctionId = GetAttributeValue(junction, "id");
                if (string.IsNullOrEmpty(junctionId)) continue;

                var junctionConnections = junction.Elements("connection").ToList();
                
                foreach (var connection in junctionConnections)
                {
                    string incomingRoadId = GetAttributeValue(connection, "incomingRoad");
                    string connectingRoadId = GetAttributeValue(connection, "connectingRoad");
                    string contactPoint = GetAttributeValue(connection, "contactPoint");

                    if (string.IsNullOrEmpty(incomingRoadId) || string.IsNullOrEmpty(connectingRoadId))
                    {
                        skippedConnections++;
                        continue;
                    }

                    if (!roadInfoMap.ContainsKey(incomingRoadId))
                    {
                        skippedConnections++;
                        continue;
                    }
                    
                    if (!roadInfoMap.ContainsKey(connectingRoadId))
                    {
                        skippedConnections++;
                        continue;
                    }

                    var incomingRoadInfo = roadInfoMap[incomingRoadId];
                    var connectingRoadInfo = roadInfoMap[connectingRoadId];

                    // Gelen yolun biti�i -> Ba�lant� yoluna
                    string fromId = incomingRoadInfo.EndNodeId;
                    
                    // contactPoint, connecting road'un hangi ucunun kullan�laca��n� belirtir
                    string toId;
                    if (string.IsNullOrEmpty(contactPoint) || contactPoint == "start")
                    {
                        toId = connectingRoadInfo.StartNodeId;
                    }
                    else
                    {
                        toId = connectingRoadInfo.EndNodeId;
                    }

                    // Incoming road -> Connecting road
                    connections.Add(Tuple.Create(fromId, toId, LINK_CONNECTION_DISTANCE));
                    
                    // Connecting road i�i
                    connections.Add(Tuple.Create(
                        connectingRoadInfo.StartNodeId, 
                        connectingRoadInfo.EndNodeId, 
                        connectingRoadInfo.Length));
                    
                    // Connecting road predecessor
                    if (connectingRoadInfo.LinkInfo != null && connectingRoadInfo.LinkInfo.Predecessor != null)
                    {
                        var predecessor = connectingRoadInfo.LinkInfo.Predecessor;
                        
                        if (predecessor.ElementType == "road" && roadInfoMap.ContainsKey(predecessor.ElementId))
                        {
                            var prevRoadInfo = roadInfoMap[predecessor.ElementId];
                            
                            string prevNodeId;
                            if (string.IsNullOrEmpty(predecessor.ContactPoint))
                            {
                                prevNodeId = prevRoadInfo.EndNodeId;
                            }
                            else
                            {
                                prevNodeId = predecessor.ContactPoint == "start" ? 
                                    prevRoadInfo.StartNodeId : prevRoadInfo.EndNodeId;
                            }
                            
                            connections.Add(Tuple.Create(
                                prevNodeId, 
                                connectingRoadInfo.StartNodeId, 
                                LINK_CONNECTION_DISTANCE));
                        }
                    }
                    
                    // Connecting road successor
                    if (connectingRoadInfo.LinkInfo != null && connectingRoadInfo.LinkInfo.Successor != null)
                    {
                        var successor = connectingRoadInfo.LinkInfo.Successor;
                        
                        if (successor.ElementType == "road" && roadInfoMap.ContainsKey(successor.ElementId))
                        {
                            var nextRoadInfo = roadInfoMap[successor.ElementId];
                            
                            string nextNodeId;
                            if (string.IsNullOrEmpty(successor.ContactPoint))
                            {
                                nextNodeId = nextRoadInfo.StartNodeId;
                            }
                            else
                            {
                                nextNodeId = successor.ContactPoint == "start" ? 
                                    nextRoadInfo.StartNodeId : nextRoadInfo.EndNodeId;
                            }
                            
                            connections.Add(Tuple.Create(
                                connectingRoadInfo.EndNodeId, 
                                nextNodeId, 
                                LINK_CONNECTION_DISTANCE));
                        }
                    }
                    
                    junctionConnectionCount++;
                }
            }

            Console.WriteLine($"Kav�ak i�i ba�lant� say�s�: {junctionConnectionCount}");
            if (skippedConnections > 0)
                Console.WriteLine($"  ?  Atlanan ba�lant�: {skippedConnections}");
        }

        /// <summary>
        /// �statistikleri yazd�r�r
        /// </summary>
        private void PrintStatistics(int roadCount, int junctionCount, List<Tuple<string, string, double>> connections)
        {
            int roadNodeCount = roadCount * 2;
            int totalNodeCount = roadNodeCount + junctionCount;
            
            Console.WriteLine($"Olu�turulan d���m say�s�: {totalNodeCount} (yol: {roadNodeCount}, kav�ak: {junctionCount})");
            Console.WriteLine($"Olu�turulan ba�lant� say�s�: {connections.Count}");

            if (connections.Count > 0)
            {
                double minLength = connections.Min(c => c.Item3);
                double maxLength = connections.Max(c => c.Item3);
                double avgLength = connections.Average(c => c.Item3);
                Console.WriteLine($"Kenar uzunluk istatistikleri: Min: {minLength:F2}, Maks: {maxLength:F2}, Ort: {avgLength:F2}");
            }
        }

        /// <summary>
        /// GraphData nesnesini olu�turur
        /// </summary>
        private GraphData CreateGraphData(Dictionary<string, Point> nodeCoords, List<Tuple<string, string, double>> connections)
        {
            var allNodeIds = nodeCoords.Keys.OrderBy(id => id).ToList();
            var nodeIdToIndex = new Dictionary<string, int>(allNodeIds.Count);
            
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                nodeIdToIndex[allNodeIds[i]] = i;
            }

            var nodeCoordinates = new Point[allNodeIds.Count];
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                nodeCoordinates[i] = nodeCoords[allNodeIds[i]];
            }

            var adjMatrix = new double[allNodeIds.Count, allNodeIds.Count];
            
            for (int i = 0; i < allNodeIds.Count; i++)
            {
                for (int j = 0; j < allNodeIds.Count; j++)
                {
                    adjMatrix[i, j] = double.PositiveInfinity;
                }
            }

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

        #region XML Parsing Helper Methods

        /// <summary>
        /// XML attribute de�erini g�venli �ekilde okur
        /// </summary>
        private string GetAttributeValue(XElement element, string attributeName)
        {
            return element?.Attribute(attributeName)?.Value ?? string.Empty;
        }

        /// <summary>
        /// XML attribute de�erini double olarak parse eder
        /// </summary>
        private double ParseDouble(XElement element, string attributeName, double defaultValue = 0.0)
        {
            string value = GetAttributeValue(element, attributeName);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            
            if (double.TryParse(value, NumberStyles.Any, culture, out double result))
                return result;
            
            return defaultValue;
        }

        /// <summary>
        /// XAttribute'tan double de�er parse eder
        /// </summary>
        private double ParseDoubleFromAttribute(XElement element, string attributeName, double defaultValue = 0.0)
        {
            var attr = element?.Attribute(attributeName);
            if (attr == null) return defaultValue;

            if (double.TryParse(attr.Value, NumberStyles.Any, culture, out double result))
                return result;

            return defaultValue;
        }

        #endregion
    }
}
