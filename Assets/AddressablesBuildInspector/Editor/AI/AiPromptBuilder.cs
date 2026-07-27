using System.Text;

namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Small helper for consistently formatted LLM prompts.
    /// </summary>
    public sealed class AiPromptBuilder
    {
        private readonly StringBuilder _builder = new StringBuilder(8192);

        public AiPromptBuilder Heading(string title)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
            }

            _builder.AppendLine(title);
            _builder.AppendLine();
            return this;
        }

        public AiPromptBuilder Line(string line = "")
        {
            _builder.AppendLine(line ?? string.Empty);
            return this;
        }

        public AiPromptBuilder Field(string label, string value)
        {
            _builder.AppendLine(label + ":");
            _builder.AppendLine(string.IsNullOrWhiteSpace(value) ? "None" : value);
            _builder.AppendLine();
            return this;
        }

        public override string ToString()
        {
            return _builder.ToString().TrimEnd();
        }
    }
}
