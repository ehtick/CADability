using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CADability.Forms.OpenGL
{
    internal class OpenGLResourceManager : IDisposable
    {
        #region Locals
        private bool disposedValue;
        private System.Windows.Forms.Control fakeControl;

        //Font management
        private Dictionary<string, FontDisplayList> fonts = new Dictionary<string, FontDisplayList>();

        // Context management
        private List<IntPtr> activeRenderContexts = new List<IntPtr>();
        private IntPtr fakeControlContext;

        //Control device context and handle management
        private System.Windows.Forms.Control activeControl;
        private IntPtr activeControlHandle;
        private IntPtr fakeControlDC;

        //OpenGL List managment
        private List<OpenGlList> listMaster = new List<OpenGlList>();

        #endregion
        #region Public Properties
        /// <summary>
        /// Returns the device context of the active control
        /// </summary>
        public IntPtr ActiveControlDC { get; private set; }


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

        /// <summary>
        /// Returns if OpenGL is connected to a Control
        /// </summary>
        public bool IsConnected => ActiveControlDC != IntPtr.Zero;

        public Dictionary<Bitmap, IPaintTo3DList> IconsCache { get; } = new Dictionary<Bitmap, IPaintTo3DList>();
        public Dictionary<Bitmap, IPaintTo3DList> BitmapsCache { get; } = new Dictionary<Bitmap, IPaintTo3DList>();
        public Dictionary<Bitmap, uint> TexturesCache { get; } = new Dictionary<Bitmap, uint>();
        #endregion
        #region Events
        public delegate void HandleRecreatedDelegate(object sender, EventArgs e);
        public event HandleRecreatedDelegate HandleRecreated;
        #endregion

        #region OpenGL Lists
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
        public OpenGlList CreateNewList(string listName)
        {
            OpenGlList newList = new OpenGlList(listName);

            //Monitor if the list was directly deleted
            newList.Deleted += OpenGlList_Deleted;

            listMaster.Add(newList);

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to open a list: 0x" + error.ToString("X"));
            else
                System.Diagnostics.Debug.WriteLine($"Created List: {listName}, 0x{newList.ListNumber.ToString("X")}, Count: {listMaster.Count}");

            return newList;
        }

        /// <summary>
        /// Will be called if the Delted event of the OpenGL List is raised.
        /// Removes the item from the listMaster
        /// </summary>
        /// <param name="sender">OpenGL List that raised the event</param>
        /// <param name="e">Empty Event Args</param>
        private void OpenGlList_Deleted(object sender, EventArgs e)
        {
            ((OpenGlList)sender).Deleted -= OpenGlList_Deleted;
            if (!listMaster.Remove((OpenGlList)sender))
                throw new IndexOutOfRangeException("Unable to find and delete specified OpenGL List");
        }        

        public IPaintTo3DList CloseList()
        {
            if (CurrentList != null)
                CurrentList.Close();

            OpenGlList res = CurrentList;
            CurrentList = null;

            int error = Gl.glGetError();
            if (error != 0)
                throw new PaintToOpenGLException("OpenList: Unable to close a list: 0x" + error.ToString("X"));

            System.Diagnostics.Debug.WriteLine("Close List: 0x" + res.ListNumber.ToString("X"));

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
        #endregion
        #region Windows Control Handling
        public IntPtr ConnectToControl(System.Windows.Forms.Control ctrl)
        {
            if (ActiveControlDC != IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: Already connected to control!");

            IntPtr deviceContext = GetDCForControl(ctrl);

            //Save as active Control to be able to release it later
            activeControl = ctrl;
            ActiveControlDC = deviceContext;
            activeControlHandle = ctrl.Handle;
            ctrl.HandleDestroyed += Ctrl_HandleDestroyed;
            ctrl.HandleCreated += Ctrl_HandleCreated;

            return ActiveControlDC;
        }

        private IntPtr GetDCForControl(System.Windows.Forms.Control ctrl)
        {
            if (ctrl == null)
                throw new ArgumentNullException(nameof(ctrl), $"{nameof(ctrl)} is null.");

            //Make sure the handle for this control has been created
            if (ctrl.Handle == IntPtr.Zero)
                throw new PaintToOpenGLException("ConnectToControl: The control's window handle has not been created.");

            //Retrieve device context
            IntPtr deviceContext = User.GetDC(ctrl.Handle);

            //Check for errors
            if (deviceContext == IntPtr.Zero)
                throw new PaintToOpenGLException("Unable to get Device Context of Control");

            return deviceContext;
        }

        private void Ctrl_HandleCreated(object sender, EventArgs e)
        {
            //FixMe: 06.01.2022 MM Event raised multiple times
            //The Ctrl_HandleDestroyed and Ctrl_HandleCreated are called multiple times for an unkown reason -> should be only once
            //FixMe: 06.01.2022 MM: OpenGL Memory Leak Warning
            //According to gDEBugger GL this application is leaking memory because of wglShareLists.
            //It's unknown if this is true or just a warning that can be ignored

            //Ctrl_HandleCreated Will only be called if the control handle is destroyed and recreated afterwards

            //Disconnect from old handle and connect to new handle
            //See https://stackoverflow.com/questions/6796067/how-often-is-a-usercontrol-handle-recreated
            //to find out how to force recreation of a handle.

            //Release old control
            activeControl.HandleCreated -= Ctrl_HandleCreated;
            activeControl.HandleDestroyed -= Ctrl_HandleDestroyed;
            activeControl = null;

            //It is expected from the managing class (PaintToOpenGL) to reinit the new control here
            if (HandleRecreated is null)
                throw new PaintToOpenGLException("The reinit of the the new control was not handled. Please handle the event OpenGLResourceManager.HandleRecreated");

            HandleRecreated.Invoke(sender, e);

            //Share the OpenGL Lists of the fake control to the new control
            if (!Wgl.wglShareLists(fakeControlContext, MainRenderContext))
                throw new PaintToOpenGLException("Unable to Share Lists between fake context and new context");
            else
                System.Diagnostics.Debug.WriteLine($"Successfully shared Lists between (FakeControlContext) 0x{fakeControlContext.ToString("X")} and (MainRenderContext) 0x{MainRenderContext.ToString("X")}");

            //Release OpenGL context for fake control
            DeleteOpenGLContext(fakeControlContext);

            //Cleanup fake control
            ReleaseControlDC(fakeControl.Handle, fakeControlDC);
            fakeControlDC = IntPtr.Zero;
            fakeControl.Dispose();
            fakeControl = null;

            fakeControlContext = IntPtr.Zero;
        }

        private void Ctrl_HandleDestroyed(object sender, EventArgs e)
        {
            System.Windows.Forms.Control ctrl = (System.Windows.Forms.Control)sender;

            if (ctrl.RecreatingHandle)
            {
                //The Control was destroyed but it will be recreated
                //We need to save the OpenGL lists to a fake control and share them with the new context
                //before deleting the old context
                fakeControl = new System.Windows.Forms.Control();
                fakeControlDC = GetDCForControl(fakeControl);
                SetupPixelFormat(fakeControlDC, false);

                fakeControlContext = GetPtrForOpenGLContext(fakeControlDC);

                //Share all OpenGL Lists between the old render context that will be now destroyed
                //and a fake render context that will store the information until the new render context is created
                if (!Wgl.wglShareLists(MainRenderContext, fakeControlContext))
                    throw new PaintToOpenGLException("Unable to Share Lists between old context and temp context");
                else
                    System.Diagnostics.Debug.WriteLine($"Successfully shared Lists between (MainRenderContext) 0x{MainRenderContext.ToString("X")} and (FakeControlContext) 0x{fakeControlContext.ToString("X")}");
                //This will delete all Contexts except the context for the fakeControl that was just setup.
                DeleteAllActiveRenderContexts();

                DisconnectFromControl();
            }
            else
            {
                //The Control was destroyed and will not be recreated
                //Cleanup OpenGL Lists and release Control
                ctrl.HandleCreated -= Ctrl_HandleCreated;
                ctrl.HandleDestroyed -= Ctrl_HandleDestroyed;
                CleanupLists();
                DisconnectFromControl();
            }
        }

        public void DisconnectFromControl()
        {
            if (ActiveControlDC == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control DC set");

            if (activeControlHandle == IntPtr.Zero)
                throw new PaintToOpenGLException("No active Control handle set");

            ReleaseControlDC(activeControlHandle, ActiveControlDC);

            ActiveControlDC = IntPtr.Zero;
            activeControlHandle = IntPtr.Zero;
            //LastRenderContext = IntPtr.Zero;
        }

        private void ReleaseControlDC(IntPtr controlHandle, IntPtr controlDC)
        {
            if (controlDC == IntPtr.Zero)
                throw new ArgumentNullException(nameof(controlHandle));

            if (controlDC == IntPtr.Zero)
                throw new ArgumentNullException(nameof(controlDC));

            if (!User.ReleaseDC(controlHandle, controlDC))
            {
                //ERROR_INVALID_MENU_HANDLE 	0x579 - Can happen if this function was called too late.
                int win32err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new PaintToOpenGLException($"Error while releasing device context: 0x{win32err.ToString("X")}");
            }
        }

        public void SetupPixelFormat(IntPtr deviceContext, bool toBitmap)
        {
            const byte accumBits = 0, colorBits = 32, depthBits = 16;

            //Setup pixel format
            Gdi.PIXELFORMATDESCRIPTOR pixelFormat = new Gdi.PIXELFORMATDESCRIPTOR();

            pixelFormat.nSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(pixelFormat);
            pixelFormat.nVersion = 1;

            if (toBitmap)
                pixelFormat.dwFlags = Gdi.PFD_SUPPORT_OPENGL | Gdi.PFD_DRAW_TO_BITMAP;
            else
                pixelFormat.dwFlags = Gdi.PFD_DRAW_TO_WINDOW | Gdi.PFD_SUPPORT_OPENGL | Gdi.PFD_DOUBLEBUFFER;

            pixelFormat.iPixelType = (byte)Gdi.PFD_TYPE_RGBA;
            pixelFormat.cColorBits = colorBits;
            pixelFormat.cRedBits = 0;
            pixelFormat.cRedShift = 0;
            pixelFormat.cGreenBits = 0;
            pixelFormat.cGreenShift = 0;
            pixelFormat.cBlueBits = 0;
            pixelFormat.cBlueShift = 0;
            pixelFormat.cAlphaBits = 0;
            pixelFormat.cAlphaShift = 0;
            pixelFormat.cAccumBits = accumBits;
            pixelFormat.cAccumRedBits = 0;
            pixelFormat.cAccumGreenBits = 0;
            pixelFormat.cAccumBlueBits = 0;
            pixelFormat.cAccumAlphaBits = 0;
            pixelFormat.cDepthBits = depthBits;
            pixelFormat.cStencilBits = 1; // stencilBits;
            pixelFormat.cAuxBuffers = 0;
            pixelFormat.iLayerType = (byte)Gdi.PFD_MAIN_PLANE;
            pixelFormat.bReserved = 0;
            pixelFormat.dwLayerMask = 0;
            pixelFormat.dwVisibleMask = 0;
            pixelFormat.dwDamageMask = 0;

            //Set pixel format            
            int selectedFormat = Gdi.ChoosePixelFormat(deviceContext, ref pixelFormat);
            //Make sure the requested pixel format is available
            if (selectedFormat == 0)
                throw new PaintToOpenGLException("SetupPixelFormat: Unable to find a suitable pixel format");

            if (!Gdi.SetPixelFormat(deviceContext, selectedFormat, ref pixelFormat))
                throw new PaintToOpenGLException($"SetupPixelFormat: Unable to set the requested pixel format ({selectedFormat})");
        }
        #endregion
        #region OpenGL Context Handling
        public IntPtr CreateContext(IntPtr deviceContext, bool toBitmap)
        {
            IntPtr renderContext = GetPtrForOpenGLContext(deviceContext);

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

        private IntPtr GetPtrForOpenGLContext(IntPtr deviceContext)
        {
            IntPtr renderContext = Wgl.wglCreateContext(deviceContext);

            if (renderContext == IntPtr.Zero)
            {
                int win32err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new PaintToOpenGLException($"CreateContexts: Unable to create an OpenGL rendering context. Win32Error: 0x{win32err.ToString("X")}");
            }

            System.Diagnostics.Debug.WriteLine("RenderContext created: 0x" + renderContext.ToString("X"));

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
        private void DeleteAllActiveRenderContexts()
        {
            //Delete all open Contexts
            if (activeRenderContexts != null)
            {
                //Delete from last to first. To delete the main (first) at the end
                for (int i = activeRenderContexts.Count - 1; i >= 0; i--)
                    DeleteOpenGLContext(activeRenderContexts[i]);

                activeRenderContexts.Clear();
                MainRenderContext = IntPtr.Zero;
            }
        }
        private void DeleteOpenGLContext(IntPtr openGlContext)
        {
            if (openGlContext == IntPtr.Zero)
                throw new ArgumentNullException(nameof(openGlContext));

            //Throws an error, but why?
            //if (!Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero))
            //    throw new PaintToOpenGLException("Unable to switch to null context");

            if (!Wgl.wglDeleteContext(openGlContext))
                throw new PaintToOpenGLException("Unable to delete Context: 0x" + openGlContext.ToString("X"));
            else
                System.Diagnostics.Debug.WriteLine("Successfully deleted Context 0x" + openGlContext.ToString("X"));
        }

        #endregion

        #region Dispose
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
                        CleanupLists();
                        DisconnectFromControl();
                    }
                    else
                    {
                        activeControl.HandleCreated -= Ctrl_HandleCreated;
                        activeControl.HandleDestroyed -= Ctrl_HandleDestroyed;
                        activeControl = null;
                    }

                    DeleteAllActiveRenderContexts();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null                
                disposedValue = true;

                activeRenderContexts = null;

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
        #endregion
    }
}
