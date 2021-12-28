using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CADability.Forms.OpenGL
{
    internal class OpenGLResourceManager : IDisposable
    {
        private bool disposedValue;

        //Font management
        Dictionary<string, FontDisplayList> fonts = new Dictionary<string, FontDisplayList>();

        // Context management
        List<IntPtr> activeRenderContexts = new List<IntPtr>();

        IntPtr mainRenderContext;

        //Control device context and handle management
        IntPtr activeControlDC;
        IntPtr activeControlHandle;

        //OpenGL List managment
        List<OpenGlList> listMaster = new List<OpenGlList>();

        /// <summary>
        /// Currently Active OpenGL List
        /// </summary>
        public OpenGlList CurrentList { get; private set; }

        public OpenGLResourceManager()
        {

        }


        public void OpenList(string listName)
        {
            if (string.IsNullOrEmpty(listName))
                throw new ArgumentException($"{nameof(listName)} is null or empty.", nameof(listName));

            if (CurrentList != null)
                throw new PaintToOpenGLException("IPaintTo3DList: nested lists not allowed");

            CurrentList = new OpenGlList(listName);
            CurrentList.Open();

            //If the list was deleted in the meantime it can be removed from the listMaster
            //as there is no need to monitor it anymore
            CurrentList.Deleted += OpenGlList_Deleted;

            listMaster.Add(CurrentList);
            System.Diagnostics.Debug.WriteLine($"Open List: {listName}, {CurrentList.ListNumber}");

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to open a list:" + error.ToString("X"));
        }

        private void OpenGlList_Deleted(object sender, EventArgs e)
        {
            ((OpenGlList)sender).Deleted -= OpenGlList_Deleted;
            listMaster.Remove((OpenGlList)sender);
        }

        public IPaintTo3DList CloseList()
        {
            if (CurrentList != null)
            {
                listMaster.Remove(CurrentList);
                CurrentList.Close();
            }

            OpenGlList res = CurrentList;
            CurrentList = null;

            System.Diagnostics.Debug.WriteLine("Close List:" + res.ListNumber.ToString("X"));

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to close a list:" + error.ToString("X"));

            if (res is null)
                return null;

            if (res.HasContents())
                return res;
            else
            {
                res.Delete();
                return null;
            }
        }

        public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists)
        {
            StringBuilder name = new StringBuilder("_");

            foreach (IPaintTo3DList sub in sublists)
                if (sub != null && !string.IsNullOrEmpty(sub.Name))
                    name.Append(sub.Name + "_");

            OpenGlList res = new OpenGlList(name.ToString());
            listMaster.Add(res);

            res.Open();
            foreach (IPaintTo3DList sub in sublists)
            {
                if (sub != null)
                {
                    Gl.glCallList((sub as OpenGlList).ListNumber);
                    res.hasContents = true;
                }
            }
            res.Close();

            (res as IPaintTo3DList).containedSubLists = sublists;

            if (!res.hasContents)
                res.Delete();

            System.Diagnostics.Debug.WriteLine("Make List: " + res.ListNumber.ToString("X"));

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("MakeList: Unable to make a list:" + error.ToString("X"));

            return res;
        }

        public IntPtr ConnectToControl(System.Windows.Forms.Control ctrl)
        {
            if (ctrl == null)
                throw new ArgumentNullException(nameof(ctrl), $"{nameof(ctrl)} is null.");

            //Make sure the handle for this control has been created
            if (ctrl.Handle == IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: The control's window handle has not been created.");

            if (activeControlDC != IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: Already connected to control!");

            //Retrieve device context
            IntPtr deviceContext = User.GetDC(ctrl.Handle);

            //Check for errors
            if (deviceContext == IntPtr.Zero)
                throw new PaintToOpenGLException("Unable to get Device Context of Control");

            //Save as active Control to be able to release it later
            activeControlDC = deviceContext;
            activeControlHandle = ctrl.Handle;

            return deviceContext;
        }

        public void DisconnectFromControl()
        {
            if (activeControlDC == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control DC set");

            if (activeControlHandle == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control handle set");

            if (!User.ReleaseDC(activeControlHandle, activeControlDC))
                throw new PaintToOpenGLException("Error while releasing device context");
        }


        public IntPtr CreateContext(IntPtr deviceContext)
        {
            IntPtr renderContext = Wgl.wglCreateContext(deviceContext);

            if (renderContext == IntPtr.Zero)
            {
                int error = Gl.glGetError();
                throw new PaintToOpenGLException("CreateContexts: Unable to create an OpenGL rendering context:" + error.ToString("X"));
            }

            System.Diagnostics.Debug.WriteLine("RenderContext created: " + renderContext.ToString());

            //First render context that is created is always the main render context
            if (activeRenderContexts.Count == 0)
                mainRenderContext = renderContext;

            activeRenderContexts.Add(renderContext);
            return renderContext;
        }


        public FontDisplayList GetFontDisplayList(string fontName, IntPtr deviceContext)
        {
            //If the font is not available in the cache, a new FontDisplayList will be created and added to the cache.
            //A new FontDisplayList is empty at the beginning and will fill if characters are requested later.
            if (!fonts.TryGetValue(fontName, out FontDisplayList res))
            {
                res = new FontDisplayList(fontName, deviceContext);
                fonts.Add(fontName, res);
            }

            return res;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //1. Delete all Fonts (including characters with OpenGL List)
                    if (fonts != null)
                    {
                        foreach (var fontItem in fonts)
                            fontItem.Value.Dispose();
                    }

                    //2. Delete all open lists
                    if(listMaster != null)
                    {
                        for (int i = listMaster.Count - 1; i >= 0; i--)
                        {
                            OpenGlList item = listMaster[i];
                            item.Dispose();
                            listMaster.RemoveAt(i);
                        }
                    }

                    //3. Release DC
                    DisconnectFromControl();

                    //4. Delete all open Contexts
                    if (activeRenderContexts != null)
                    {
                        for (int i = activeRenderContexts.Count - 1; i >= 0; i--)
                        {
                            bool deletionSuccessfull = Wgl.wglDeleteContext(activeRenderContexts[i]);
                            if (!deletionSuccessfull)
                                throw new PaintToOpenGLException("Unable to delete Context:" + activeRenderContexts[i].ToString("X"));
                            activeRenderContexts.RemoveAt(i);
                        }
                    }
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                disposedValue = true;

                IntPtr mh = Kernel.GetModuleHandle("opengl32.dll");
                if (mh != IntPtr.Zero) Kernel.FreeLibrary(mh);
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
