using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace ConsoleApp3.Parsers
{
    /// <summary>
    /// OpenDRIVE (.xodr) dosyalarýný ayrýþtýrarak graf modeli oluþturur.
    /// Yol baþlangýç/bitiþ noktalarý ve kavþak baðlantýlarý düðüm olarak modellenir.
    /// </summary>
    public class XodrParser
    {
        // Sabitler - Magic string'leri önlemek için
        private const string ROAD_PREFIX = "road:";
        private const string JUNCTION_PREFIX = "junction:";
        private const string START_SUFFIX = ":start";
        private const string END_SUFFIX = ":end";
        private const string JUNCTION_ID_DEFAULT = "-1";
        private const double LINK_CONNECTION_DISTANCE = 0.1; // Düðümler arasý minimal mesafe

        private readonly CultureInfo culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Yol uç noktalarýný ve koordinatlarýný temsil eder
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
        /// Yol baðlantý bilgilerini saklar
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
            // 1. Adým: Tüm yol bilgilerini tek geçiþte topla
            var roadInfos = ParseRoadInformation(roads);




            }

            foreach (var road in roads)
            {

                var (startId, endId) = roadEndpoints[roadId];
                
                // planView geometri baþlangýç noktasý
                var planView = road.Element("planView");
                if (planView == null) continue;

                if (geometries.Count == 0) continue;







                {
                }
                {
                }

            return linkInfo;
            }

            {
            }
            else if (lastGeom.Element("arc") != null)
            {
                // Arc için eðri hesaplama
                var arc = lastGeom.Element("arc");
                double curvature = ParseDoubleFromAttribute(arc, "curvature");


            {



                {
                    
                    {
                        {
                            
                        }
                    }
            }
                    
                    
                    {
                    }

            // Kavþak düðümlerini ekle
            foreach (var kvp in junctionCoords)
            {
                nodeCoords[$"{JUNCTION_PREFIX}{kvp.Key}"] = kvp.Value;
                }

            return nodeCoords;
            }

            {
                
                

                {
                    
                    {
                        
                    }
                        {
                        }
                    }
                }

                {
                    
                    {
                        
                    }
                    {
                        if (nodeCoords.ContainsKey(junctionNodeId))
                        {
                        }
                    }
                }
            }

            int junctionConnectionCount = 0;
            int skippedConnections = 0;

            foreach (var junction in junctions)
            {
                if (string.IsNullOrEmpty(junctionId)) continue;
                
                {
                    
                        continue;
                    }

                    var incomingRoadInfo = roadInfoMap[incomingRoadId];
                    var connectingRoadInfo = roadInfoMap[connectingRoadId];

                    // Gelen yolun bitiþi -> Baðlantý yoluna
                    string fromId = incomingRoadInfo.EndNodeId;
                    
                    // contactPoint, connecting road'un hangi ucunun kullanýlacaðýný belirtir
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
                    
                    // Connecting road içi
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


                    
                    
                    connections.Add(Tuple.Create(fromId, toId, connectingLength));
                    junctionConnectionCount++;
                }
            }

            Console.WriteLine($"Kavþak içi baðlantý sayýsý: {junctionConnectionCount}");
            if (skippedConnections > 0)
                Console.WriteLine($"  ?  Atlanan baðlantý: {skippedConnections}");
            }

            
            // Minimum, maksimum ve ortalama uzunluk deðerlerini göster
            if (connections.Count > 0)
            {
                double minLength = connections.Min(c => c.Item3);
                double maxLength = connections.Max(c => c.Item3);
                double avgLength = connections.Average(c => c.Item3);
                Console.WriteLine($"Kenar uzunluk istatistikleri: Min: {minLength:F2}, Maks: {maxLength:F2}, Ort: {avgLength:F2}");
            }
        }
            
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

        #region XML Parsing Helper Methods

        /// <summary>
        /// XML attribute deðerini güvenli þekilde okur
        /// </summary>
        private string GetAttributeValue(XElement element, string attributeName)
        {
            return element?.Attribute(attributeName)?.Value ?? string.Empty;
        }

        /// <summary>
        /// XML attribute deðerini double olarak parse eder
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
        /// XAttribute'tan double deðer parse eder
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
