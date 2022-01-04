using System;

namespace CADability.Forms.OpenGL
{
    internal class MoveFacesBehindEdgesOffset : IDisposable
    {        
        public MoveFacesBehindEdgesOffset(PaintToOpenGL paintTo)
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPushMatrix();

            IPaintTo3D to3D = paintTo;            
            Gl.glTranslated(to3D.Precision * paintTo.ProjectionDirection.x, to3D.Precision * paintTo.ProjectionDirection.y, to3D.Precision * paintTo.ProjectionDirection.z);
        }
        #region IDisposable Members
        void IDisposable.Dispose()
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPopMatrix();
        }
        #endregion
    }
}
