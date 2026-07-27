using System.IO;
using System.Text;
using UnityEngine;

namespace AddressablesBuildInspector.Editor.AI
{
    /// <summary>
    /// Exports generated AI prompts without contacting external services.
    /// </summary>
    public sealed class AiPromptExporter
    {
        public void CopyToClipboard(string prompt)
        {
            GUIUtility.systemCopyBuffer = prompt ?? string.Empty;
        }

        public void ExportTxt(string prompt, string path)
        {
            File.WriteAllText(path, prompt ?? string.Empty, Encoding.UTF8);
        }

        public void ExportMarkdown(string prompt, string path)
        {
            File.WriteAllText(path, prompt ?? string.Empty, Encoding.UTF8);
        }
    }
}
