using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Bulico
{
    public static class RegionFinder
    {
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
                bool anySplit = false;
                int n = edges.Count;

                for (int i = 0; i < n; i++)
                {
                    Edge e1 = edges[i];
                    double e1x1 = Math.Min(e1.Start.X, e1.End.X);
                    double e1x2 = Math.Max(e1.Start.X, e1.End.X);
                    double e1y1 = Math.Min(e1.Start.Y, e1.End.Y);
                    double e1y2 = Math.Max(e1.Start.Y, e1.End.Y);

                    for (int j = i + 1; j < n; j++)
                    {
                        Edge e2 = edges[j];

                        if (e1x2 < Math.Min(e2.Start.X, e2.End.X) - eps) continue;
                        if (e1x1 > Math.Max(e2.Start.X, e2.End.X) + eps) continue;
                        if (e1y2 < Math.Min(e2.Start.Y, e2.End.Y) - eps) continue;
                        if (e1y1 > Math.Max(e2.Start.Y, e2.End.Y) + eps) continue;

                        XYZ pt = ComputeIntersection(e1, e2, eps);
                        if (pt == null) continue;

                        bool onE1 = IsPointOnSegment(pt, e1, eps);
                        bool onE2 = IsPointOnSegment(pt, e2, eps);
                        if (!onE1 || !onE2) continue;

                        bool epE1 = pt.DistanceTo(e1.Start) < eps || pt.DistanceTo(e1.End) < eps;
                        bool epE2 = pt.DistanceTo(e2.Start) < eps || pt.DistanceTo(e2.End) < eps;

                        if (onE1 && !epE1)
                        {
                            edges.Add(new Edge(pt, e1.End));
                            e1.End = pt;
                            n = edges.Count;
                            anySplit = true;
                        }
                        if (onE2 && !epE2)
                        {
                            edges.Add(new Edge(pt, e2.End));
                            e2.End = pt;
                            n = edges.Count;
                            anySplit = true;
                        }
                    }
                }

                if (!anySplit) break;
            }
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
            bool any;
            do
            {
                var deg = new Dictionary<string, int>();
                foreach (var e in edges)
                {
                    string ks = PtKey(e.Start);
                    string ke = PtKey(e.End);
                    int c;
                    deg.TryGetValue(ks, out c); deg[ks] = c + 1;
                    deg.TryGetValue(ke, out c); deg[ke] = c + 1;
                }

                any = false;
                for (int i = edges.Count - 1; i >= 0; i--)
                {
                    if (deg[PtKey(edges[i].Start)] == 1 || deg[PtKey(edges[i].End)] == 1)
                    {
                        edges.RemoveAt(i);
                        any = true;
                    }
                }
            } while (any);
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
                        bool dup = false;
                        int fc = faces.Count;
                        for (int fi = 0; fi < fc; fi++)
                        {
                            if (IsSamePolygon(face, faces[fi]))
                            { dup = true; break; }
                        }
                        if (!dup) faces.Add(face);
                    }
                }
            }

            return faces;
        }

        static bool IsSamePolygon(List<XYZ> p1, List<XYZ> p2)
        {
            int n = p1.Count;
            if (p2.Count != n) return false;
            for (int off = 0; off < n; off++)
            {
                bool ok = true;
                for (int i = 0; i < n; i++)
                {
                    if (!p1[i].IsAlmostEqualTo(p2[(i + off) % n]))
                    { ok = false; break; }
                }
                if (ok) return true;
            }
            for (int off = 0; off < n; off++)
            {
                bool ok = true;
                for (int i = 0; i < n; i++)
                {
                    if (!p1[i].IsAlmostEqualTo(p2[(n - 1 - i + off) % n]))
                    { ok = false; break; }
                }
                if (ok) return true;
            }
            return false;
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
