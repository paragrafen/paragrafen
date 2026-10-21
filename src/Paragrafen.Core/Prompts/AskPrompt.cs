using System.Security;
using System.Text;
using Paragrafen.Core.Models;

namespace Paragrafen.Core.Prompts;

public class AskPrompt
{

    private const string SystemMarker = "=== SYSTEM ===";
    private const string UserMarker = "=== USER ===";

    private readonly string _system;
    private readonly string _userTemplate;

    public string Version { get; } // the prompt version

    private AskPrompt(string version, string system, string userTemplate)
    {
        Version = version;
        _system = system;
        _userTemplate = userTemplate;
    }

    public static AskPrompt Load(string version = "v0")
    {
        var name = $"Paragrafen.Core.Prompts.prompt_{version}.md";
        using var stream = typeof(AskPrompt).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Prompt resource '{name}' not found.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var systemStart = text.IndexOf(SystemMarker, StringComparison.Ordinal);
        var userStart = text.IndexOf(UserMarker, StringComparison.Ordinal);
        if (systemStart < 0 || userStart < systemStart)
            throw new InvalidOperationException($"Prompt '{name}' needs '{SystemMarker}' before '{UserMarker}'.");

        var system = text[(systemStart + SystemMarker.Length)..userStart].Trim();
        var user = text[(userStart + UserMarker.Length)..].Trim();
        return new AskPrompt(version, system, user);
    }

    public (string System, string User) Render(string question, string documents) =>
        (_system, _userTemplate.Replace("{{documents}}", documents).Replace("{{question}}", question));

    // One <document> per chunk, in the format the prompt's examples use. Ids are numbered from 1 in search order,
    // and the model cites them as [1], [2], so the same order must be used when turning citations into sources.
    public static string FormatDocuments(IReadOnlyList<ChunkMatch> chunks)
    {
        var documents = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            documents.Append($"<document id=\"{i + 1}\" law=\"{SecurityElement.Escape(chunk.Law)}\" section=\"{SecurityElement.Escape(chunk.SectionRef ?? "")}\">\n");
            documents.Append(chunk.Text.Trim()).Append("\n</document>\n");
        }

        return documents.ToString().TrimEnd();
    }
}
