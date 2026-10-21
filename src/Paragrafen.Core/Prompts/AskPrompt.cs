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
}
