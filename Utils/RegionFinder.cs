using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Bulico
{
    public static class RegionFinder
    {
        private const int MaxEdges = 20000;
        private const int MaxFaces = 5000;

        public static List<CurveLoop> FindClosedRegions(List<Curve> curves, double tolerance)
        {
            if (curves == null || curves.Count < 3)
                return new List<CurveLoop>();

            double eps = tolerance > 0 ? tolerance : 0.01;

            List<Line> lines = new List<Line>();
            foreach (var curve in curves)
            {
                Line line = curve as Line;
                if (line != null)
                {
                    XYZ p0 = RoundPt(new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, 0));
                    XYZ p1 = RoundPt(new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, 0));
                    if (p0.DistanceTo(p1) > eps)
                        lines.Add(Line.CreateBound(p0, p1));
                    continue;
                }

                Arc arc = curve as Arc;
                if (arc != null)
                {
                    IList<XYZ> pts = arc.Tessellate();
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        XYZ flat1 = RoundPt(new XYZ(pts[i].X, pts[i].Y, 0));
                        XYZ flat2 = RoundPt(new XYZ(pts[i + 1].X, pts[i + 1].Y, 0));
                        if (flat1.DistanceTo(flat2) > eps)
                            lines.Add(Line.CreateBound(flat1, flat2));
                    }
                }
            }

            if (lines.Count < 3)
                return new List<CurveLoop>();

            List<Edge> edges = new List<Edge>(lines.Count);
            foreach (var line in lines)
                edges.Add(new Edge(line.GetEndPoint(0), line.GetEndPoint(1)));

            SplitIntersections(edges, eps);
            RemoveDanglingEdges(edges);

            edges.RemoveAll(e => e.Start.DistanceTo(e.End) < eps);

            if (edges.Count < 3)
                return new List<CurveLoop>();

            var graph = BuildGraph(edges);
            var faces = FindFaces(graph);

            var result = new List<CurveLoop>();
            foreach (var face in faces)
            {
                double area = ComputeSignedArea(face);
                if (area > eps)
                {
                    var pts = face.ToList();
                    CurveLoop loop = new CurveLoop();
                    bool validLoop = true;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        XYZ start = new XYZ(pts[i].X, pts[i].Y, 0);
                        XYZ end = new XYZ(pts[(i + 1) % pts.Count].X, pts[(i + 1) % pts.Count].Y, 0);
                        if (start.DistanceTo(end) < eps) { validLoop = false; break; }
                        loop.Append(Line.CreateBound(start, end));
                    }
                    if (validLoop && !loop.IsOpen() && loop.Count() >= 3)
                    {
                        try
                        {
                            if (loop.HasPlane())
                                result.Add(loop);
                        }
                        catch { }
                    }
                }
            }

            return result;
        }

        class Edge
        {
            public XYZ Start;
            public XYZ End;

            public Edge(XYZ s, XYZ e) { Start = s; End = e; }
            public Edge Reversed() { return new Edge(End, Start); }
        }

        static void SplitIntersections(List<Edge> edges, double eps)
        {
            for (int pass = 0; pass < 20; pass++)
            {
                int n = edges.Count;
                if (n < 2 || n > MaxEdges) break;

                // 按最小 X 排序做扫描线，只比较 X 区间可能重叠的边，
                // 避免复杂选择下的全量两两求交。
                List<int> order = new List<int>(n);
                for (int i = 0; i < n; i++)
                    order.Add(i);
                order.Sort((a, b) => MinX(edges[a]).CompareTo(MinX(edges[b])));

                List<int> active = new List<int>();
                bool anySplit = false;

                foreach (int i in order)
                {
                    Edge e1 = edges[i];
                    double e1MinX = MinX(e1);

                    active.RemoveAll(j => MaxX(edges[j]) < e1MinX - eps);

                    for (int k = 0; k < active.Count; k++)
                    {
                        Edge e2 = edges[active[k]];
                        if (!AabbOverlap(e1, e2, eps)) continue;

                        XYZ pt = ComputeIntersection(e1, e2, eps);
                        if (pt == null) continue;
                        if (!IsPointOnSegment(pt, e1, eps) || !IsPointOnSegment(pt, e2, eps)) continue;

                        bool epE1 = pt.DistanceTo(e1.Start) < eps || pt.DistanceTo(e1.End) < eps;
                        bool epE2 = pt.DistanceTo(e2.Start) < eps || pt.DistanceTo(e2.End) < eps;

                        if (!epE1)
                        {
                            edges.Add(new Edge(pt, e1.End));
                            e1.End = pt;
                            anySplit = true;
                        }
                        if (!epE2)
                        {
                            edges.Add(new Edge(pt, e2.End));
                            e2.End = pt;
                            anySplit = true;
                        }
                    }

                    active.Add(i);
                }

                if (!anySplit) break;
            }
        }

        static double MinX(Edge edge)
        {
            return Math.Min(edge.Start.X, edge.End.X);
        }

        static double MaxX(Edge edge)
        {
            return Math.Max(edge.Start.X, edge.End.X);
        }

        static bool AabbOverlap(Edge e1, Edge e2, double eps)
        {
            if (MaxX(e1) < MinX(e2) - eps) return false;
            if (MinX(e1) > MaxX(e2) + eps) return false;

            double e1MinY = Math.Min(e1.Start.Y, e1.End.Y);
            double e1MaxY = Math.Max(e1.Start.Y, e1.End.Y);
            double e2MinY = Math.Min(e2.Start.Y, e2.End.Y);
            double e2MaxY = Math.Max(e2.Start.Y, e2.End.Y);

            if (e1MaxY < e2MinY - eps) return false;
            if (e1MinY > e2MaxY + eps) return false;

            return true;
        }

        static XYZ ComputeIntersection(Edge e1, Edge e2, double eps)
        {
            XYZ a = e1.Start, b = e1.End;
            XYZ c = e2.Start, d = e2.End;

            XYZ ab = b - a;
            XYZ cd = d - c;

            double denom = ab.X * cd.Y - ab.Y * cd.X;
            if (Math.Abs(denom) < 1e-12) return null;

            double t = ((c.X - a.X) * cd.Y - (c.Y - a.Y) * cd.X) / denom;
            double u = ((c.X - a.X) * ab.Y - (c.Y - a.Y) * ab.X) / denom;

            if (t >= -eps && t <= 1 + eps && u >= -eps && u <= 1 + eps)
            {
                double ix = a.X + t * ab.X;
                double iy = a.Y + t * ab.Y;
                return new XYZ(ix, iy, 0);
            }

            return null;
        }

        static bool IsPointOnSegment(XYZ pt, Edge edge, double eps)
        {
            XYZ a = edge.Start, b = edge.End;
            double len = a.DistanceTo(b);
            if (len < eps) return false;
            return Math.Abs(pt.DistanceTo(a) + pt.DistanceTo(b) - len) < eps;
        }

        static XYZ RoundPt(XYZ pt)
        {
            return new XYZ(
                Math.Round(pt.X, 6),
                Math.Round(pt.Y, 6),
                0);
        }

        static string PtKey(XYZ pt)
        {
            return string.Format("{0:F4},{1:F4}", pt.X, pt.Y);
        }

        static void RemoveDanglingEdges(List<Edge> edges)
        {
            if (edges.Count == 0) return;

            var endpointEdges = new Dictionary<string, List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                AddEndpointEdge(endpointEdges, PtKey(edges[i].Start), i);
                AddEndpointEdge(endpointEdges, PtKey(edges[i].End), i);
            }

            bool[] alive = new bool[edges.Count];
            for (int i = 0; i < alive.Length; i++)
                alive[i] = true;

            var queue = new Queue<string>();
            foreach (var kvp in endpointEdges)
            {
                if (kvp.Value.Count == 1)
                    queue.Enqueue(kvp.Key);
            }

            while (queue.Count > 0)
            {
                string key = queue.Dequeue();
                List<int> list;
                if (!endpointEdges.TryGetValue(key, out list) || list.Count != 1)
                    continue;

                int index = list[0];
                if (index < 0 || index >= edges.Count || !alive[index])
                    continue;

                Edge edge = edges[index];
                string startKey = PtKey(edge.Start);
                string endKey = PtKey(edge.End);

                alive[index] = false;
                RemoveEndpointEdge(endpointEdges, startKey, index);
                RemoveEndpointEdge(endpointEdges, endKey, index);

                EnqueueIfDangling(endpointEdges, queue, startKey);
                EnqueueIfDangling(endpointEdges, queue, endKey);
            }

            int write = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (alive[i])
                    edges[write++] = edges[i];
            }

            if (write < edges.Count)
                edges.RemoveRange(write, edges.Count - write);
        }

        private static void AddEndpointEdge(Dictionary<string, List<int>> map, string key, int index)
        {
            List<int> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<int>();
                map[key] = list;
            }
            list.Add(index);
        }

        private static void RemoveEndpointEdge(Dictionary<string, List<int>> map, string key, int index)
        {
            List<int> list;
            if (!map.TryGetValue(key, out list))
                return;

            list.Remove(index);
            if (list.Count == 0)
                map.Remove(key);
        }

        private static void EnqueueIfDangling(Dictionary<string, List<int>> map, Queue<string> queue, string key)
        {
            List<int> list;
            if (map.TryGetValue(key, out list) && list.Count == 1)
                queue.Enqueue(key);
        }

        class Graph
        {
            public Dictionary<string, List<Edge>> Adj = new Dictionary<string, List<Edge>>();

            public string Key(XYZ pt)
            {
                return PtKey(pt);
            }

            public void AddEdge(Edge e)
            {
                string k1 = Key(e.Start);
                string k2 = Key(e.End);
                List<Edge> list1, list2;
                if (!Adj.TryGetValue(k1, out list1)) { list1 = new List<Edge>(); Adj[k1] = list1; }
                if (!Adj.TryGetValue(k2, out list2)) { list2 = new List<Edge>(); Adj[k2] = list2; }
                list1.Add(e);
                list2.Add(e.Reversed());
            }

            public List<Edge> GetEdgesFrom(XYZ pt)
            {
                List<Edge> list;
                if (Adj.TryGetValue(Key(pt), out list))
                    return list;
                return new List<Edge>();
            }
        }

        static Graph BuildGraph(List<Edge> edges)
        {
            Graph graph = new Graph();
            int n = edges.Count;
            for (int i = 0; i < n; i++)
                graph.AddEdge(edges[i]);

            foreach (var kvp in graph.Adj)
            {
                var list = kvp.Value;
                list.Sort((a, b) =>
                {
                    double aa = Math.Atan2(a.End.Y - a.Start.Y, a.End.X - a.Start.X);
                    double ab = Math.Atan2(b.End.Y - b.Start.Y, b.End.X - b.Start.X);
                    return aa.CompareTo(ab);
                });
            }

            return graph;
        }

        static List<List<XYZ>> FindFaces(Graph graph)
        {
            var faces = new List<List<XYZ>>();
            var visited = new HashSet<string>();
            var seenFaces = new HashSet<string>();

            foreach (var kvp in graph.Adj)
            {
                var edges = kvp.Value;
                int m = edges.Count;
                for (int ei = 0; ei < m; ei++)
                {
                    var edge = edges[ei];
                    string ekey = graph.Key(edge.Start) + "->" + graph.Key(edge.End);
                    if (visited.Contains(ekey))
                        continue;

                    var face = new List<XYZ>();
                    XYZ curS = edge.Start;
                    XYZ curE = edge.End;
                    face.Add(curS);

                    bool closed = false;
                    int iter = 0;

                    while (!closed && iter < 50000)
                    {
                        iter++;
                        visited.Add(graph.Key(curS) + "->" + graph.Key(curE));

                        var outgoing = graph.GetEdgesFrom(curE);
                        Edge next = null;
                        XYZ inDir = (curS - curE).Normalize();
                        double best = -1.0;

                        int oc = outgoing.Count;
                        for (int oi = 0; oi < oc; oi++)
                        {
                            var e = outgoing[oi];
                            if (graph.Key(e.End) == graph.Key(curS)) continue;
                            XYZ outDir = (e.End - e.Start).Normalize();
                            double dot = inDir.X * outDir.X + inDir.Y * outDir.Y;
                            double det = inDir.X * outDir.Y - inDir.Y * outDir.X;
                            double ang = Math.Atan2(det, dot);
                            if (ang < 0) ang += 2 * Math.PI;
                            if (ang > best) { best = ang; next = e; }
                        }

                        if (next == null) break;

                        curS = next.Start;
                        curE = next.End;

                        if (graph.Key(curS) == graph.Key(face[0]))
                        {
                            closed = true;
                            break;
                        }
                        face.Add(curS);
                    }

                    if (closed && face.Count >= 3)
                    {
                        // 用规范化顶点序列去重，避免每发现一个面就与已有面逐点比较。
                        string faceKey = CanonicalFaceKey(face);
                        if (seenFaces.Add(faceKey))
                        {
                            faces.Add(face);
                            if (faces.Count >= MaxFaces)
                                return faces;
                        }
                    }
                }
            }

            return faces;
        }

        static string CanonicalFaceKey(List<XYZ> face)
        {
            int n = face.Count;
            string[] keys = new string[n];
            for (int i = 0; i < n; i++)
                keys[i] = PtKey(face[i]);

            string best = null;
            for (int start = 0; start < n; start++)
            {
                for (int dir = 0; dir < 2; dir++)
                {
                    var sb = new System.Text.StringBuilder(n * 12);
                    for (int i = 0; i < n; i++)
                    {
                        int idx = dir == 0
                            ? (start + i) % n
                            : ((start - i) % n + n) % n;
                        sb.Append(keys[idx]);
                        sb.Append(';');
                    }

                    string candidate = sb.ToString();
                    if (best == null || string.CompareOrdinal(candidate, best) < 0)
                        best = candidate;
                }
            }

            return best ?? string.Empty;
        }

        static double ComputeSignedArea(List<XYZ> polygon)
        {
            double area = 0;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                XYZ p1 = polygon[i];
                XYZ p2 = polygon[(i + 1) % n];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return area / 2.0;
        }
    }
}
