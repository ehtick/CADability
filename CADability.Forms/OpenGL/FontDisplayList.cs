using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Forms.OpenGL
{
    class FontDisplayList : Dictionary<char, CharacterDisplayInfo>, IDisposable
    {
        private string fontName;
        private IntPtr deviceContext;
        private bool disposedValue;
        private OpenGLResourceManager resManager;

        public string FontName { get => fontName; }
        public IntPtr DeviceContext { get => deviceContext; }

        public FontDisplayList(string fontName, IntPtr deviceContext, OpenGLResourceManager resManager)
        {
            if (string.IsNullOrEmpty(fontName))
                throw new ArgumentNullException(nameof(fontName));

            if (deviceContext == IntPtr.Zero)
                throw new ArgumentNullException(nameof(deviceContext));

            if (resManager is null)
                throw new ArgumentNullException(nameof(resManager));

            this.fontName = fontName;
            this.deviceContext = deviceContext;
            this.resManager = resManager;
        }

        /// <summary>
        /// Check if the Character is already present otherwise create it.
        /// </summary>
        /// <param name="c"></param>
        public void AssertCharacter(char c)
        {
            if (!ContainsKey(c))
            {
                IntPtr fnt = Gdi.CreateFont(100, 0, 0, 0, 0, false, false, false, 1, 0, 0, 0, 0, FontName);
                IntPtr oldfont = Gdi.SelectObject(DeviceContext, fnt);
                Gdi.GLYPHMETRICSFLOAT[] glyphmetrics = new Gdi.GLYPHMETRICSFLOAT[1];

                OpenGlList list = resManager.CreateNewList(FontName + "-" + c);
                //OpenGlList list = new OpenGlList(FontName + "-" + c);

                if (Wgl.wglUseFontOutlines(DeviceContext, (int)c, 1, list.ListNumber, 20.0f, 0.0f, Wgl.WGL_FONT_POLYGONS, glyphmetrics))
                {
                    //System.Diagnostics.Debug.WriteLine("wglUseFontOutlines success: " + deviceContext.ToString() + ", " + c);
                    CharacterDisplayInfo cdl;
                    cdl.Glyphmetrics = glyphmetrics[0];
                    cdl.Displaylist = list;
                    this[c] = cdl;
                }
                else
                {
                    fnt = Gdi.CreateFont(-100, 0, 0, 0, 0, false, false, false, 1, 0, 0, 0, 0, FontName);
                    bool dbg = Wgl.wglUseFontOutlines(DeviceContext, (int)c, 1, list.ListNumber, 20.0f, 0.0f, Wgl.WGL_FONT_POLYGONS, glyphmetrics);
                }

                Gdi.SelectObject(DeviceContext, oldfont);
                Gdi.DeleteObject(fnt);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //Dispose all open OpenGLDisplayLists
                    System.Diagnostics.Debug.WriteLine("Disposing FontDisplayList: " + this.FontName);
                    if (Values != null)
                        foreach (CharacterDisplayInfo cdi in Values)
                            cdi.Displaylist.Dispose();                                        
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                resManager = null;
                disposedValue = true;
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
