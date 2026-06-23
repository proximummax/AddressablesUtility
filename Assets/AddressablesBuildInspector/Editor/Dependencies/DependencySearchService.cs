using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Utilities;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Searches dependency graph nodes by name and path.
    /// </summary>
    public sealed class DependencySearchService
    {
        private readonly DependencyGraphService _graphService;

        /// <summary>
        /// Creates a dependency search service.
        /// </summary>
        public DependencySearchService(DependencyGraphService graphService)
        {
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
        }

        /// <summary>
        /// Searches all report assets.
        /// </summary>
        public IReadOnlyList<DependencyNode> Search(string query, int maxCount = int.MaxValue)
        {
            return _graphService.GetAllAssets()
                .Where(node => SearchUtility.Matches(query, node.AssetName, node.AssetPath))
                .OrderBy(node => node.AssetPath, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .ToList();
        }
    }
}
