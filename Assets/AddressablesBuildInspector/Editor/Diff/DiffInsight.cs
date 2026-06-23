namespace AddressablesBuildInspector.Editor.Diff
{
    /// <summary>
    /// Ranked text insight generated from a build diff.
    /// </summary>
    public sealed class DiffInsight
    {
        /// <summary>Ranking order, lower is more important.</summary>
        public int Rank { get; set; }

        /// <summary>Insight message.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
