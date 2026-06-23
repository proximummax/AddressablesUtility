using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Builds graph-safe dependency trees for UI rendering.
    /// </summary>
    public sealed class DependencyTreeBuilder
    {
        private readonly DependencyGraphService _service;

        /// <summary>
        /// Creates a dependency tree builder.
        /// </summary>
        public DependencyTreeBuilder(DependencyGraphService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Builds a dependency tree for an asset path.
        /// </summary>
        public DependencyNode BuildTree(string rootAssetPath, int maxDepth = 32)
        {
            return BuildTree(rootAssetPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Math.Max(1, maxDepth));
        }

        private DependencyNode BuildTree(string assetPath, HashSet<string> visited, int remainingDepth)
        {
            DependencyNode node = _service.CreateNode(assetPath);
            if (!visited.Add(node.AssetPath))
            {
                node.IsCircularReference = true;
                return node;
            }

            if (remainingDepth <= 1)
            {
                return node;
            }

            foreach (DependencyNode dependency in _service.GetDependencies(node.AssetPath))
            {
                node.Dependencies.Add(BuildTree(
                    dependency.AssetPath,
                    new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase),
                    remainingDepth - 1));
            }

            return node;
        }
    }
}
