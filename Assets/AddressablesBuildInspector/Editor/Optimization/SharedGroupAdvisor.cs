using System;
using System.Collections.Generic;
using System.Linq;

namespace AddressablesBuildInspector.Editor.Optimization
{
    /// <summary>
    /// Produces shared Addressables group recommendations from optimization candidates.
    /// </summary>
    public sealed class SharedGroupAdvisor
    {
        /// <summary>
        /// Selects the strongest candidates for a shared group.
        /// </summary>
        public IReadOnlyList<SharedGroupRecommendation> Recommend(IEnumerable<OptimizationCandidate> candidates, int maxCount = 10)
        {
            if (candidates == null)
            {
                return Array.Empty<SharedGroupRecommendation>();
            }

            return candidates
                .Where(candidate => candidate?.Asset != null)
                .Where(candidate => candidate.Recommendation == OptimizationRecommendation.MoveToSharedGroup ||
                                    candidate.Recommendation == OptimizationRecommendation.CreateCommonAssetsGroup ||
                                    candidate.Recommendation == OptimizationRecommendation.ConvertToSharedDependency)
                .OrderByDescending(candidate => candidate.Impact.PotentialSavingsBytes)
                .ThenByDescending(candidate => candidate.BundleCount)
                .ThenBy(candidate => candidate.Asset.Path, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxCount))
                .Select(candidate => new SharedGroupRecommendation
                {
                    AssetName = candidate.Asset.Name,
                    AssetPath = candidate.Asset.Path,
                    BundleCount = candidate.BundleCount,
                    PotentialSavingsBytes = candidate.Impact.PotentialSavingsBytes
                })
                .ToList();
        }
    }
}
