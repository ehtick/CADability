using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;


namespace CADability.Forms.OpenGL
{
    public class PaintToOpenGLException : ApplicationException
    {
        public PaintToOpenGLException()
        {
        }

        public PaintToOpenGLException(string message) : base(message)
        {            
            //if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
            //    MessageBox.Show(msg);
        }

        public PaintToOpenGLException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PaintToOpenGLException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
