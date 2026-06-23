using System;

namespace AddressablesBuildInspector.Editor.Utilities
{
    /// <summary>
    /// Shared null-safe search helpers for editor tables.
    /// </summary>
    public static class SearchUtility
    {
        /// <summary>
        /// Returns true when the query is empty or appears in any supplied field.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="fields">Fields to scan.</param>
        /// <returns>True if the item should be visible.</returns>
        public static bool Matches(string query, params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            if (fields == null)
            {
                return false;
            }

            string trimmedQuery = query.Trim();
            foreach (string field in fields)
            {
                if (!string.IsNullOrEmpty(field) &&
                    field.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
