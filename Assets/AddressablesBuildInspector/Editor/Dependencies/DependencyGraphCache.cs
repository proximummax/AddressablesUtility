using System;
using System.Collections.Generic;

namespace AddressablesBuildInspector.Editor.Dependencies
{
    /// <summary>
    /// Caches dependency graph lookups for the current report.
    /// </summary>
    public sealed class DependencyGraphCache
    {
        private readonly Dictionary<string, IReadOnlyList<DependencyNode>> _dependencies = new Dictionary<string, IReadOnlyList<DependencyNode>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<DependencyNode>> _referencedBy = new Dictionary<string, IReadOnlyList<DependencyNode>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a cached dependency list or stores the created list.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetOrAddDependencies(string assetPath, Func<IReadOnlyList<DependencyNode>> factory)
        {
            if (_dependencies.TryGetValue(assetPath, out IReadOnlyList<DependencyNode> cached))
            {
                return cached;
            }

            IReadOnlyList<DependencyNode> value = factory();
            _dependencies[assetPath] = value;
            return value;
        }

        /// <summary>
        /// Gets a cached reverse dependency list or stores the created list.
        /// </summary>
        public IReadOnlyList<DependencyNode> GetOrAddReferencedBy(string assetPath, Func<IReadOnlyList<DependencyNode>> factory)
        {
            if (_referencedBy.TryGetValue(assetPath, out IReadOnlyList<DependencyNode> cached))
            {
                return cached;
            }

            IReadOnlyList<DependencyNode> value = factory();
            _referencedBy[assetPath] = value;
            return value;
        }

        /// <summary>
        /// Clears all cached graph data.
        /// </summary>
        public void Clear()
        {
            _dependencies.Clear();
            _referencedBy.Clear();
        }
    }
}
