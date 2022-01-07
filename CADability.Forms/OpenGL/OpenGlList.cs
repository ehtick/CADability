using System;
using System.Collections.Generic;
using System.Linq;


namespace CADability.Forms.OpenGL
{
    internal class OpenGlList : IPaintTo3DList
    {
        public static int listCounter = 0;

        public bool hasContents, isDeleted;
        public OpenGlList(string name = null)
        {
            ListNumber = Gl.glGenLists(1); // make a single list

            if (!string.IsNullOrEmpty(name))
                this.name = name;
            else
                this.name = "NoName_" + ListNumber.ToString();

            listCounter++;
        }

        public int ListNumber { get; }
        public void SetHasContents()
        {
            hasContents = true;
        }
        public bool HasContents()
        {
            return hasContents;
        }
        public void Open()
        {
            Gl.glNewList(ListNumber, Gl.GL_COMPILE);
        }
        public void Close()
        {
            Gl.glEndList();
        }
        public void Delete()
        {
            if (isDeleted)
                return;

            isDeleted = true;
            Gl.glDeleteLists(ListNumber, 1);

            int glError = Gl.glGetError();
            if (glError != 0)
                throw new PaintToOpenGLException($"Unable to delete List no:0x{ListNumber.ToString("X")}");            

            listCounter--;

            RaiseDeletedEvent();
            //var current = Wgl.wglGetCurrentContext();
        }

        public delegate void DeletedEventHandler(object sender, EventArgs e);
        public event DeletedEventHandler Deleted;

        private void RaiseDeletedEvent()
        {
            Deleted?.Invoke(this, EventArgs.Empty);
        }

        #region IPaintTo3DList Members
        private string name;
        string IPaintTo3DList.Name
        {
            get => name;
            set => name = value;
        }
        private List<IPaintTo3DList> keepAlive;
        private bool disposedValue;

        List<IPaintTo3DList> IPaintTo3DList.containedSubLists
        {
            // das Problem mit den SubLists ist so:
            // Es werden meherere OpenGlList objekte generiert (z.B. Block)
            // dann werden diese Listen durch "glCallList" in eine zusammengeführt. Aber gl
            // merkt sich nur die Nummern. deshalb müssen diese Listen am Leben bleiben
            // und dürfen nicht freigegeben werden. Hier ist der Platz sie zu erhalten.
            set
            {
                keepAlive = value;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)                    
                    System.Diagnostics.Debug.WriteLine("Disposing OpenGl List No.: " + ListNumber.ToString());
                    Delete();

                    if (keepAlive != null)
                        for (int i = 0; i < keepAlive.Count; i++)
                            (keepAlive[i] as OpenGlList)?.Delete();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
