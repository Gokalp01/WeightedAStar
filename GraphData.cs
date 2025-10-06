using System.Collections.Generic;

namespace ConsoleApp3
{
    /// <summary>
    /// Ayrıştırılmış harita verilerinden oluşturulan grafı temsil eder.
    /// </summary>
    public class GraphData
    {
        public double[,] AdjacencyMatrix { get; }
        public Point[] NodeCoordinates { get; }
       
        public int NodeCount => NodeCoordinates?.Length ?? 0;
        // Orijinal dosyadaki ID'leri (örn: OSM node ID, XODR junction ID) dizi indeksine eşler.
        public Dictionary<string, int> NodeIdToIndexMap { get; }

        public GraphData(double[,] adjacencyMatrix, Point[] nodeCoordinates, Dictionary<string, int> nodeIdToIndexMap)
        {
            AdjacencyMatrix = adjacencyMatrix;
            NodeCoordinates = nodeCoordinates;
            NodeIdToIndexMap = nodeIdToIndexMap;
        }
    }
}