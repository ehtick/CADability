using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CADability.Forms.OpenGL
{    struct State
    {   // der state wird in einem Stack gehalten und wieder restauriert
        // da können noch mehr Dinge dazukommen
        public bool useZBuffer;
        public bool blending;
    }
}
