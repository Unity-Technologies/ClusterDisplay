namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Layout modes for <see cref="TiledProjection"/>.
    /// </summary>
    public enum LayoutMode
    {
        /// <summary>
        /// Display one tile of the grid.
        /// </summary>
        /// <remarks>
        /// This is the default layout b
        /// </remarks>
        StandardTile,

        /// <summary>
        /// Display the entire grid.
        /// </summary>
        /// <remarks>
        /// The renderer draws each tile of the grid and assembles them
        /// together for display. Can only be activated in debug mode. Useful for
        /// visualizing the "seams". Can be slow.
        /// </remarks>
        StandardStitcher
    }
}
