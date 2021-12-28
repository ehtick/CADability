namespace CADability.Forms.OpenGL
{
    /// <summary>
    /// The structure contains information about the metrics of the character
    /// and the owner (OpenGl Display List)
    /// </summary>
    struct CharacterDisplayInfo
    {
        /// <summary>
        /// Metrics
        /// </summary>
        public Gdi.GLYPHMETRICSFLOAT Glyphmetrics;
        /// <summary>
        /// Owner
        /// </summary>
        public OpenGlList Displaylist;
    }
}
