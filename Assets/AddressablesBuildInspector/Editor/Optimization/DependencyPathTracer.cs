using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesBuildInspector.Editor.Dependencies;
using AddressablesBuildInspector.Editor.Models;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Finds report-backed paths that lead to duplicated dependency assets.
    /// </summary>
    public sealed class DependencyPathTracer
    {
        private const int MaxPathCount = 12;
        private const int MaxDepth = 24;
        private readonly DependencyGraphService _graphService;

        /// <summary>
        /// Creates a path tracer for the supplied dependency graph.
        /// </summary>
        public DependencyPathTracer(DependencyGraphService graphService)
        {
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
        }

        /// <summary>
        /// Traces paths from referencing assets to the duplicated asset.
        /// </summary>
        public IReadOnlyList<DependencyPath> TracePaths(AssetData targetAsset)
        {
            if (targetAsset == null)
            {
                return Array.Empty<DependencyPath>();
            }

            string targetPath = NormalizePath(targetAsset.Path);
            var paths = new List<DependencyPath>();
            foreach (DependencyNode reference in _graphService.GetReferencedBy(targetPath).Take(MaxPathCount))
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var stack = new List<string>();
                if (TryTrace(reference.AssetPath, targetPath, visited, stack, 0, out DependencyPath path))
                {
                    path.ImpactBytes = targetAsset.SizeBytes;
                    paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                paths.Add(new DependencyPath
                {
                    ImpactBytes = targetAsset.SizeBytes,
                    AssetPaths = { targetPath }
                });
            }

            return paths;
        }

        private bool TryTrace(
            string currentPath,
            string targetPath,
            HashSet<string> visited,
            List<string> stack,
            int depth,
            out DependencyPath path)
        {
            path = null;
            string normalized = NormalizePath(currentPath);
            if (depth > MaxDepth || !visited.Add(normalized))
            {
                return false;
            }

            stack.Add(normalized);
            if (string.Equals(normalized, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                path = new DependencyPath();
                path.AssetPaths.AddRange(stack);
                return true;
            }

            foreach (DependencyNode dependency in _graphService.GetDependencies(normalized))
            {
                if (TryTrace(dependency.AssetPath, targetPath, visited, stack, depth + 1, out path))
                {
                    return true;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            visited.Remove(normalized);
            return false;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
