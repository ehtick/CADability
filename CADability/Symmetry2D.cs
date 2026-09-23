using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability
{
    /// <summary>
    /// Finds the mirror axes of a set of 2d points or of a <see cref="Path2D"/>. The axes all pass
    /// through the centroid, which is where a mirror axis of a symmetric figure has to lie.
    /// </summary>
    public class Symmetry2D
    {
        /// <summary>
        /// Finds all mirror axes of the provided points. Two points can only be images of each other when
        /// they have the same distance to the centroid, so the candidates for an axis are the
        /// perpendicular bisectors of the pairs that do, and each candidate is then verified against all
        /// remaining points.
        /// </summary>
        /// <param name="points">the points to examine</param>
        /// <param name="axis">receives the directions of the mirror axes, empty when there is none</param>
        /// <returns>the centroid, which every axis passes through</returns>
        public static GeoPoint2D FindSymmetryAxes2D(GeoPoint2D[] points, out List<GeoVector2D> axis)
        {
            GeoPoint2D centroid = new GeoPoint2D(points);
            double[] dist = new double[points.Length];
            for (int i = 0; i < points.Length; i++) dist[i] = points[i] | centroid;
            int[] sortedIndices = Enumerable.Range(0, points.Length).ToArray();
            Array.Sort(sortedIndices, (a, b) => dist[a].CompareTo(dist[b]));
            axis = new List<GeoVector2D>();
            for (int i = 0; i < sortedIndices.Length - 1; i++)
            {
                for (int l = i + 1; l < sortedIndices.Length; ++l)
                {
                    if (dist[sortedIndices[l]] - dist[sortedIndices[i]] < Precision.eps)
                    {
                        // a pair of points with the same distance to the centroid
                        GeoVector2D v = points[sortedIndices[l]] - points[sortedIndices[i]];
                        if (v.Length < Precision.eps) continue; // both points are identical
                        GeoVector2D axisCandidate = new GeoVector2D(-v.y, v.x); // perpendicular to the connection
                        axisCandidate.Norm();
                        bool alreadyFound = false;
                        for (int j = 0; j < axis.Count; j++)
                        {
                            if (Precision.SameDirection(axis[j], axisCandidate, true))
                            {
                                alreadyFound = true;
                                break;
                            }
                        }
                        if (alreadyFound) continue;
                        // verify the candidate: every point must either lie on the axis or have a partner
                        // that is its mirror image
                        HashSet<int> toTest = new HashSet<int>(Enumerable.Range(0, points.Length));
                        toTest.Remove(sortedIndices[i]);
                        toTest.Remove(sortedIndices[l]);
                        for (int j = 0; j < sortedIndices.Length; j++)
                        {
                            if (!toTest.Contains(sortedIndices[j])) continue; // already tested
                            if (Math.Abs(Geometry.DistPL(points[sortedIndices[j]], centroid, axisCandidate)) < Precision.eps)
                            {
                                toTest.Remove(sortedIndices[j]); // the point lies on the axis
                                continue;
                            }
                            for (int k = j + 1; k < points.Length; k++)
                            {
                                if (dist[sortedIndices[k]] - dist[sortedIndices[j]] < Precision.eps)
                                {
                                    double s = (points[sortedIndices[j]] - points[sortedIndices[k]]) * axisCandidate;
                                    if (Math.Abs(s) < Precision.eps) // the connection is perpendicular to the axis
                                    {   // the two points are mirror images of each other
                                        toTest.Remove(sortedIndices[j]);
                                        toTest.Remove(sortedIndices[k]);
                                        break;
                                    }
                                }
                                else break; // no more points with the same distance
                            }
                        }
                        if (!toTest.Any()) axis.Add(axisCandidate);
                    }
                }
            }
            return centroid;
        }

        /// <summary>
        /// Finds the mirror axes of a path. The vertices of the path give the candidates; each candidate
        /// is then verified on the geometry, by splitting the path where the axis crosses it and testing
        /// the parts against their mirror images.
        /// </summary>
        /// <param name="path">the path to examine</param>
        /// <param name="axis">receives the directions of the mirror axes, empty when there is none</param>
        /// <returns>the centroid the axes pass through</returns>
        public static GeoPoint2D FindSymmetryAxes2D(Path2D path, out List<GeoVector2D> axis)
        {
            List<GeoPoint2D> samples = new List<GeoPoint2D>();
            foreach (ICurve2D c2d in path.SubCurves) samples.Add(c2d.EndPoint);
            if (samples.Count <= 2)
            {   // too few vertices to say anything, take the middle of each segment as well
                foreach (ICurve2D c2d in path.SubCurves) samples.Add(c2d.PointAt(0.5));
            }
            GeoPoint2D centroid = FindSymmetryAxes2D(samples.ToArray(), out axis);
            BoundingRect ext = path.GetExtent();
            for (int i = axis.Count - 1; i >= 0; --i)
            {
                GeoPoint2DWithParameter[] ips = path.Intersect(centroid - ext.Size * axis[i], centroid + ext.Size * axis[i]);
                List<GeoPoint2DWithParameter> lips = new List<GeoPoint2DWithParameter>();
                for (int j = 0; j < ips.Length; j++)
                {
                    if (ips[j].par1 < 0.0 || ips[j].par1 > 1.0 || ips[j].par2 < 0.0 || ips[j].par2 > 1.0) continue;
                    if (lips.Count > 0 && Precision.IsEqual(ips[j].p, lips[lips.Count - 1].p)) continue;
                    lips.Add(ips[j]);
                }
                if (lips.Count == 0)
                {   // the axis does not even cross the path
                    axis.RemoveAt(i);
                    continue;
                }
                if (lips.Count == 2)
                {
                    ModOp2D reflect = ModOp2D.Reflect(centroid, axis[i]);
                    List<ICurve2D> parts = new List<ICurve2D>(path.Split(new double[] { lips[0].par1, lips[1].par1 }));
                    if (path.IsClosed && parts.Count > 1
                        && Precision.IsEqual(parts[0].StartPoint, parts[parts.Count - 1].EndPoint))
                    {   // the split ran over the start point of the closed path, join the two ends
                        parts[0] = new Path2D(new ICurve2D[] { parts[parts.Count - 1], parts[0] });
                        (parts[0] as Path2D).Flatten();
                        parts.RemoveAt(parts.Count - 1);
                    }
                    bool ok = false;
                    if (parts.Count % 2 == 0)
                    {
                        ok = true;
                        for (int j = 0; j < parts.Count / 2; j++)
                        {
                            Path2D first = parts[j] as Path2D;
                            Path2D second = parts[parts.Count - 1 - j] as Path2D;
                            if (first == null || second == null) { ok = false; break; }
                            first.Flatten();
                            second.Flatten();
                            ICurve2D mirrored = second.GetModified(reflect);
                            mirrored.Reverse();
                            ICurve a3d = first.MakeGeoObject(Plane.XYPlane) as ICurve;
                            ICurve b3d = mirrored.MakeGeoObject(Plane.XYPlane) as ICurve;
                            if (a3d == null || b3d == null || !a3d.SameGeometry(b3d, Precision.eps))
                            {
                                ok = false;
                                break;
                            }
                        }
                    }
                    if (!ok) axis.RemoveAt(i);
                }
            }
            return centroid;
        }
    }
}
