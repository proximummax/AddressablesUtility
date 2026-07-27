namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Prompt generation limits and section switches for one report mode.
    /// </summary>
    public sealed class AiPromptTemplate
    {
        public AiReportMode Mode { get; private set; }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public int MaxItems { get; private set; }

        public bool IncludeBuildGrowth { get; private set; }

        public bool IncludeDuplicates { get; private set; }

        public bool IncludeDependencies { get; private set; }

        public bool IncludeTechnicalAuditQuestions { get; private set; }

        public static AiPromptTemplate ForMode(AiReportMode mode)
        {
            switch (mode)
            {
                case AiReportMode.QuickReview:
                    return new AiPromptTemplate
                    {
                        Mode = mode,
                        Title = "Quick Review",
                        Description = "Short prompt with only the highest-impact findings. Use for quick triage before deeper analysis.",
                        MaxItems = 5,
                        IncludeDuplicates = true,
                        IncludeBuildGrowth = false,
                        IncludeDependencies = false
                    };
                case AiReportMode.BuildGrowthInvestigation:
                    return new AiPromptTemplate
                    {
                        Mode = mode,
                        Title = "Build Growth Investigation",
                        Description = "Focuses on Build Diff data: bundle growth, added assets, largest contributors, and regression insights.",
                        MaxItems = 10,
                        IncludeDuplicates = false,
                        IncludeBuildGrowth = true,
                        IncludeDependencies = false
                    };
                case AiReportMode.DuplicateInvestigation:
                    return new AiPromptTemplate
                    {
                        Mode = mode,
                        Title = "Duplicate Investigation",
                        Description = "Focuses on duplicated assets, suspected causes, dependency paths, and safe deduplication opportunities.",
                        MaxItems = 12,
                        IncludeDuplicates = true,
                        IncludeBuildGrowth = false,
                        IncludeDependencies = true
                    };
                case AiReportMode.TechnicalAudit:
                    return new AiPromptTemplate
                    {
                        Mode = mode,
                        Title = "Technical Audit",
                        Description = "Most detailed report. Includes optimization, diff, dependency, audit, validation, and CI-oriented questions.",
                        MaxItems = 15,
                        IncludeDuplicates = true,
                        IncludeBuildGrowth = true,
                        IncludeDependencies = true,
                        IncludeTechnicalAuditQuestions = true
                    };
                default:
                    return new AiPromptTemplate
                    {
                        Mode = AiReportMode.OptimizationConsultant,
                        Title = "Optimization Consultant",
                        Description = "Default full optimization prompt with duplicate waste, candidates, dependencies, build growth, and ROI questions.",
                        MaxItems = 10,
                        IncludeDuplicates = true,
                        IncludeBuildGrowth = true,
                        IncludeDependencies = true
                    };
            }
        }
    }
}
