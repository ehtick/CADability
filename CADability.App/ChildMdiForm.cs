using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CADability.App
{
    public partial class ChildMdiForm : Form
    {
        public ChildMdiForm()
        {
            InitializeComponent();
            cadControl1.CreateMainMenu = true;
        }

        private void ChildMdiForm_Load(object sender, EventArgs e)
        {


            Project newProj = Project.CreateSimpleProject();
            cadControl1.CadFrame.Project = newProj;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.RightToLeft == System.Windows.Forms.RightToLeft.Yes)
                this.RightToLeft = System.Windows.Forms.RightToLeft.No;
            else this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var m = cadControl1.CadFrame.Project.GetActiveModel();

            List<ICurve> curves = new List<ICurve>(m.AllObjects.Count);
            foreach (IGeoObject geo in m.AllObjects)
                if (geo is ICurve geoCurve)
                    curves.Add(geoCurve);

            if (!Curves.GetCommonPlane(curves, out Plane pln))
                return;

            List<ICurve2D> curves2D = new List<ICurve2D>(curves.Count);

            foreach (ICurve curve in curves)
                curves2D.Add(curve.GetProjectedCurve(pln));

            var cs = CompoundShape.CreateFromList(curves2D.ToArray(), 0.01, true, out GeoObjectList deads);

            Path2D ssOutlines = cs.SimpleShapes[0].Outline.AsPath();
            for (int i = 1; i < cs.SimpleShapes.Length; i++)
                ssOutlines = SimpleShape.ConnectPaths(ssOutlines, cs.SimpleShapes[i].Outline.AsPath());

            //Create Convex Hull
            var hull = ssOutlines.MakeBorder().ConvexHull();

            //Convert Border of Convex Hull to GeoObjects
            var segements = hull.GetClonedSegments();
            GeoObjectList geos = new GeoObjectList(segements.Length);
            foreach (ICurve2D i in segements)
            {
                i.Reverse();
                IGeoObject geoObject = i.MakeGeoObject(pln);
                geos.Add(geoObject);
            }
            // Add Convex Hull Border to Model
            m.Add(geos);

            //Create smalles surrounding rect
            var ssr = ssOutlines.MakeBorder().GetSmallestEnclosingRectangle();

            //Calc lower left start point
            var lowerleft = pln.ToGlobal(ssr.FixPoint);


            GeoObject.Polyline line = GeoObject.Polyline.Construct();
            line.SetRectangle(lowerleft, new GeoVector(ssr.Width, 0, 0), new GeoVector(0, ssr.Height, 0));
            line.ParallelogramMainDirection = new GeoVector(ssr.BasisDir);            
            
            //m.Add(line);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var m = cadControl1.CadFrame.Project.GetActiveModel();

            List<ICurve> curves = new List<ICurve>(m.AllObjects.Count);
            foreach (IGeoObject geo in m.AllObjects)
                if (geo is ICurve geoCurve)
                    curves.Add(geoCurve);

            if (!Curves.GetCommonPlane(curves, out Plane pln))
                return;

            List<ICurve2D> curves2D = new List<ICurve2D>(curves.Count);

            foreach (ICurve curve in curves)
                curves2D.Add(curve.GetProjectedCurve(pln));

            var cs = CompoundShape.CreateFromList(curves2D.ToArray(), 0.01, true, out GeoObjectList deads);


        }
    }
}
