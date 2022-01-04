using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CADability.Forms.OpenGL
{
    internal class OpenGLResourceManager : IDisposable
    {
        private bool disposedValue;

        //Font management
        Dictionary<string, FontDisplayList> fonts = new Dictionary<string, FontDisplayList>();

        // Context management
        List<IntPtr> activeRenderContexts = new List<IntPtr>();

        //Control device context and handle management
        System.Windows.Forms.Control activeControl;
        IntPtr activeControlHandle;

        /// <summary>
        /// Returns the device context of the active control
        /// </summary>
        public IntPtr ActiveControlDC { get; private set; }

        //OpenGL List managment
        List<OpenGlList> listMaster = new List<OpenGlList>();

        /// <summary>
        /// Currently Active OpenGL List
        /// </summary>
        public OpenGlList CurrentList { get; private set; }

        /// <summary>
        /// Contains the main render context, which is responsible for all OpenGL Lists
        /// </summary>
        public IntPtr MainRenderContext { get; private set; }
        /// <summary>
        /// Render context that was created last
        /// </summary>
        public IntPtr LastRenderContext { get; private set; }

        public Dictionary<Bitmap, IPaintTo3DList> IconsCache { get; } = new Dictionary<Bitmap, IPaintTo3DList>();
        public Dictionary<Bitmap, IPaintTo3DList> BitmapsCache { get; } = new Dictionary<Bitmap, IPaintTo3DList>();
        public Dictionary<Bitmap, uint> TexturesCache { get; } = new Dictionary<Bitmap, uint>();


        public OpenGLResourceManager()
        {

        }

        public void OpenList(string listName)
        {
            if (string.IsNullOrEmpty(listName))
                throw new ArgumentException($"{nameof(listName)} is null or empty.", nameof(listName));

            if (CurrentList != null)
                throw new PaintToOpenGLException("IPaintTo3DList: nested lists not allowed");

            CurrentList = CreateNewList(listName);
            CurrentList.Open();
        }

        /// <summary>
        /// Call this function to create a new list and monitor it for deletion.
        /// </summary>
        /// <param name="listName"></param>
        /// <returns></returns>
        /// <exception cref="PaintToOpenGLException"></exception>
        internal OpenGlList CreateNewList(string listName)
        {
            OpenGlList newList = new OpenGlList(listName);

            //Monitor if the list was directly deleted
            newList.Deleted += OpenGlList_Deleted;

            listMaster.Add(newList);

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to open a list: 0x" + error.ToString("X"));
            else
                System.Diagnostics.Debug.WriteLine($"Created List: {listName}, 0x{newList.ListNumber.ToString("X")}");

            return newList;
        }

        private void OpenGlList_Deleted(object sender, EventArgs e)
        {
            ((OpenGlList)sender).Deleted -= OpenGlList_Deleted;
            listMaster.Remove((OpenGlList)sender);
        }

        public IPaintTo3DList CloseList()
        {
            if (CurrentList != null)
                CurrentList.Close();

            OpenGlList res = CurrentList;
            CurrentList = null;

            System.Diagnostics.Debug.WriteLine("Close List: 0x" + res.ListNumber.ToString("X"));

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to close a list: 0x" + error.ToString("X"));

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
            if (sublists.Count == 0)
                return null;

            string listName = string.Empty;

            if (sublists.Count > 0)
            {
                StringBuilder name = new StringBuilder("_");

                foreach (IPaintTo3DList sub in sublists)
                    if (sub != null && !string.IsNullOrEmpty(sub.Name))
                        name.Append(sub.Name + "_");

                listName = name.ToString();
            }

            OpenGlList res = CreateNewList(listName);

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

            return res;
        }

        public IntPtr ConnectToControl(System.Windows.Forms.Control ctrl)
        {
            if (ctrl == null)
                throw new ArgumentNullException(nameof(ctrl), $"{nameof(ctrl)} is null.");

            //Make sure the handle for this control has been created
            if (ctrl.Handle == IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: The control's window handle has not been created.");

            if (ActiveControlDC != IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: Already connected to control!");

            ConnectToHandle(ctrl);

            return ActiveControlDC;
        }

        private void ConnectToHandle(System.Windows.Forms.Control ctrl)
        {
            //Retrieve device context
            IntPtr deviceContext = User.GetDC(ctrl.Handle);

            //Check for errors
            if (deviceContext == IntPtr.Zero)
                throw new PaintToOpenGLException("Unable to get Device Context of Control");

            //Save as active Control to be able to release it later
            activeControl = ctrl;
            ActiveControlDC = deviceContext;
            activeControlHandle = ctrl.Handle;
            ctrl.HandleDestroyed += Ctrl_HandleDestroyed;
            ctrl.HandleCreated += Ctrl_HandleCreated;
        }

        private void Ctrl_HandleCreated(object sender, EventArgs e)
        {
            //Will only be called if the control handle is destroyed and recreated afterwards

            throw new NotImplementedException();
            //TODO: Disconnect from old handle and connect to new handle
            //See https://stackoverflow.com/questions/6796067/how-often-is-a-usercontrol-handle-recreated
            //to find out how to force recreation of a handle.
            //But this will not work anyway. CADability is not prepared for recreating the handle and will throw various errors.

            //All OpenGL Contexts and lists need to be destroyed and recreated with the new handle!

            System.Windows.Forms.Control ctrl = (System.Windows.Forms.Control)sender;
            //Unsubscribe from HandleCreated to avoid duplicate events
            ctrl.HandleCreated -= Ctrl_HandleCreated;
            ConnectToControl(ctrl);
        }

        private void Ctrl_HandleDestroyed(object sender, EventArgs e)
        {
            DestroyHandle((System.Windows.Forms.Control)sender);
        }

        private void DestroyHandle(System.Windows.Forms.Control ctrl)
        {
            //Don't unsubscribe from HandleCreated here, it could be recreated
            ctrl.HandleDestroyed -= Ctrl_HandleDestroyed;

            CleanupLists();
            DisconnectFromControl();
        }

        public void DisconnectFromControl()
        {
            if (ActiveControlDC == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control DC set");

            if (activeControlHandle == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control handle set");

            if (!User.ReleaseDC(activeControlHandle, ActiveControlDC))
            {
                //ERROR_INVALID_MENU_HANDLE 	0x579 - Can happen if this function was called too late.
                int win32err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new PaintToOpenGLException($"Error while releasing device context: 0x{win32err.ToString("X")}");
            }

            ActiveControlDC = IntPtr.Zero;
            activeControlHandle = IntPtr.Zero;
            //LastRenderContext = IntPtr.Zero;
        }


        public IntPtr CreateContext(IntPtr deviceContext, bool toBitmap)
        {
            IntPtr renderContext = Wgl.wglCreateContext(deviceContext);

            if (renderContext == IntPtr.Zero)
                throw new PaintToOpenGLException("CreateContexts: Unable to create an OpenGL rendering context");

            System.Diagnostics.Debug.WriteLine("RenderContext created: 0x" + renderContext.ToString("X"));

            if (!toBitmap)
            {
                if (MainRenderContext == IntPtr.Zero)
                    MainRenderContext = renderContext;
                else
                {
                    if (!Wgl.wglShareLists(LastRenderContext, renderContext))
                        throw new PaintToOpenGLException($"Unable to share Lists between: 0x{LastRenderContext.ToString("X")} & 0x{renderContext.ToString("X")}");

                    System.Diagnostics.Debug.Write($"Sharing Lists between: 0x{LastRenderContext.ToString("X")} & 0x{renderContext.ToString("X")}");
                }

                LastRenderContext = renderContext;
            }

            activeRenderContexts.Add(renderContext);
            return renderContext;
        }


        public FontDisplayList GetFontDisplayList(string fontName, IntPtr deviceContext)
        {
            //If the font is not available in the cache, a new FontDisplayList will be created and added to the cache.
            //A new FontDisplayList is empty at the beginning and will fill if characters are requested later.
            if (!fonts.TryGetValue(fontName, out FontDisplayList res))
            {
                res = new FontDisplayList(fontName, deviceContext, this);
                fonts.Add(fontName, res);
            }

            return res;
        }

        public void MakeCurrent()
        {
            if (ActiveControlDC == IntPtr.Zero)
                throw new PaintToOpenGLException("Connection to Control Device Context lost.");

            if (LastRenderContext == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Render Context set");

            if (!Wgl.wglMakeCurrent(ActiveControlDC, LastRenderContext))
                throw new PaintToOpenGLException("MakeCurrentContext: Unable to activate this control's OpenGL rendering context");
        }

        /// <summary>
        /// Returns if OpenGL is connected to a Control
        /// </summary>
        public bool IsConnected
        {
            get => ActiveControlDC != IntPtr.Zero;
        }

        void CleanupLists()
        {
            /// All Displaylists have to be closed before the context is lost.
            /// Context is lost by either deleting it or releasing the device context to the control

            //Check if the current context is the master context
            //If not or the masterContext is Null the delete operation of the lists will fail!
            if (Wgl.wglGetCurrentContext() != MainRenderContext)
                if (!Wgl.wglMakeCurrent(ActiveControlDC, MainRenderContext))
                    throw new PaintToOpenGLException("Failed to switch to master context to delete open lists.");

            //1. Delete all Fonts (including characters with OpenGL List)
            if (fonts != null)
            {
                foreach (var fontItem in fonts)
                    fontItem.Value.Dispose();

                fonts.Clear();
                fonts = null;
            }

            //2. Delete all open lists
            if (listMaster != null)
            {
                //This way the listMaster can be modified from outside while looping
                while (listMaster.Count > 0)
                    listMaster[0].Dispose();

                listMaster = null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (BitmapsCache != null && BitmapsCache.Count > 0)
                    {
                        //Clear BitmapsCache
                        foreach (var item in this.BitmapsCache)
                        {
                            item.Value.Dispose();
                            item.Key.Dispose();
                        }
                        BitmapsCache.Clear();
                    }

                    if (IconsCache != null && IconsCache.Count > 0)
                    {
                        //Clear IconsCache
                        foreach (var item in this.IconsCache)
                        {
                            item.Value.Dispose();
                            item.Key.Dispose();
                        }
                        IconsCache.Clear();
                    }

                    if (TexturesCache != null && TexturesCache.Count > 0)
                    {
                        //Clear TexturesCache
                        foreach (var item in this.TexturesCache)
                            item.Key.Dispose();

                        IconsCache.Clear();
                    }

                    //We are still connected to the Control but the Resource Manager should be disposed
                    //This will happen if the control (CadCanvas) is still active (not destroyed) but the CADability project changes                    
                    if (activeControlHandle != IntPtr.Zero)
                    {
                        System.Windows.Forms.Control ctrl = System.Windows.Forms.Control.FromHandle(activeControlHandle);
                        DestroyHandle(ctrl);
                    }
                    else
                    {
                        activeControl.HandleCreated -= Ctrl_HandleCreated;
                        activeControl.HandleDestroyed -= Ctrl_HandleDestroyed;
                        activeControl = null;
                    }

                    //Delete all open Contexts
                    if (activeRenderContexts != null)
                    {
                        //Delete from last to first. To delete the main (first) at the end
                        for (int i = activeRenderContexts.Count - 1; i >= 0; i--)
                        {
                            bool deletionSuccessfull = Wgl.wglDeleteContext(activeRenderContexts[i]);
                            if (!deletionSuccessfull)
                                throw new PaintToOpenGLException("Unable to delete Context: 0x" + activeRenderContexts[i].ToString("X"));
                            else
                                System.Diagnostics.Debug.WriteLine("Successfully deleted Context 0x" + activeRenderContexts[i].ToString("X"));
                        }
                        activeRenderContexts.Clear();
                        activeRenderContexts = null;
                        MainRenderContext = IntPtr.Zero;
                    }
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                disposedValue = true;

                IntPtr mh = Kernel.GetModuleHandle("opengl32.dll");
                if (mh != IntPtr.Zero)
                    Kernel.FreeLibrary(mh);
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
