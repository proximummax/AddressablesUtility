using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Provides direct dependency data for an asset.
    /// </summary>
    public interface IDependencyProvider
    {
        /// <summary>
        /// True when this provider has dependency data available.
        /// </summary>
        bool HasDependencyData { get; }

        /// <summary>
        /// Gets direct dependencies for an asset path.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path.</param>
        /// <returns>Direct dependencies.</returns>
        IReadOnlyList<DependencyNode> GetDependencies(string assetPath);
    }
}
