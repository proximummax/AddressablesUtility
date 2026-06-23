using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// One reference path leading to a duplicated asset.
    /// </summary>
    public sealed class DependencyPath
    {
        /// <summary>Ordered asset paths from root to target.</summary>
        public List<string> AssetPaths { get; } = new List<string>();

        /// <summary>Total estimated path impact in bytes.</summary>
        public long ImpactBytes { get; set; }

        /// <summary>Number of edges in the path.</summary>
        public int Depth => AssetPaths.Count;
    }
}
