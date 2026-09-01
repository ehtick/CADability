using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression reported from the field: a polyline vertex bulge of |bulge| &gt; 1 (an arc
    /// of more than 180°, e.g. a keyhole contour whose partial circle spans 287°) placed the
    /// arc center on the wrong side of the chord, so the arc collapsed to its complement on
    /// the mirrored center.
    /// </summary>
    [TestClass]
    public class DxfArcAndEllipseTest
    {
        public TestContext TestContext { get; set; }

        // --- 1. bulge import -----------------------------------------------------------------

        // Old-style POLYLINE/VERTEX (the entity the customer file contains). One segment from
        // (10,0) to (0,10) with bulge tan(270°/4) = 1+√2: a counterclockwise 270° arc with
        // radius 10 around (10,10); its midpoint is (10+10·cos45°, 10+10·sin45°).
        // The closing segment back to (10,0) is a line.
        [TestMethod]
        public void import_dxf_polyline_bulge_over_half_circle()
        {
            const string dxf = @"  0
SECTION
  2
ENTITIES
  0
POLYLINE
  8
0
 66
1
 70
1
  0
VERTEX
  8
0
 10
10.0
 20
0.0
 30
0.0
 42
2.414213562373095
  0
VERTEX
  8
0
 10
0.0
 20
10.0
 30
0.0
  0
SEQEND
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path, "POLYLINE should import as a Path");
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc, "the bulge segment should import as an arc");

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(10.0, 10.0, 0.0), 1e-8,
                "arc center: an arc of more than 180° has its center on the other side of the chord");
            Assert.AreEqual(1.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(10.0 + s, 10.0 + s, 0.0), 1e-8,
                "arc midpoint (the bulge apex)");
        }

        // The clockwise twin (negative bulge, LWPOLYLINE this time): 270° from (10,0) to
        // (0,10) around (0,0), midpoint at (-10·cos45°, -10·sin45°).
        [TestMethod]
        public void import_dxf_lwpolyline_negative_bulge_over_half_circle()
        {
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
LWPOLYLINE
  8
0
100
AcDbEntity
100
AcDbPolyline
 90
2
 70
1
 10
10.0
 20
0.0
 42
-2.414213562373095
 10
0.0
 20
10.0
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path, "LWPOLYLINE should import as a Path");
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc, "the bulge segment should import as an arc");

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(0.0, 0.0, 0.0), 1e-8, "arc center");
            Assert.AreEqual(1.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(-s, -s, 0.0), 1e-8,
                "arc midpoint (the bulge apex)");
        }

        // Arcs up to 180° were correct before the fix and have to stay so: 90° fillet,
        // counterclockwise from (10,0) to (0,10) around (0,0), midpoint on the diagonal.
        [TestMethod]
        public void import_dxf_lwpolyline_small_bulge_still_correct()
        {
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
LWPOLYLINE
  8
0
100
AcDbEntity
100
AcDbPolyline
 90
2
 70
1
 10
10.0
 20
0.0
 42
0.4142135623730951
 10
0.0
 20
10.0
  0
ENDSEC
  0
EOF
";
            var model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count);
            var path = model.AllObjects[0] as GeoObject.Path;
            Assert.IsNotNull(path);
            Ellipse arc = null;
            foreach (ICurve curve in path.Curves)
                if (curve is Ellipse e) arc = e;
            Assert.IsNotNull(arc);

            Assert.AreEqual(10.0, arc.Radius, 1e-8, "arc radius");
            Assert.AreEqual(0.0, arc.Center | new GeoPoint(0.0, 0.0, 0.0), 1e-8, "arc center");
            Assert.AreEqual(0.5 * Math.PI, Math.Abs(arc.SweepParameter), 1e-8, "arc sweep");
            double s = 10.0 * Math.Sqrt(0.5);
            Assert.AreEqual(0.0, (arc as ICurve).PointAt(0.5) | new GeoPoint(s, s, 0.0), 1e-8,
                "arc midpoint");
        }

        // --- helpers -------------------------------------------------------------------------

        private Model ImportDxf(string dxf)
        {
            string file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }

    }
}
