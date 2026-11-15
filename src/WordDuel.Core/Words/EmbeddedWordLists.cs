namespace WordDuel.Core.Words;

/// <summary>
/// Loads the word lists that are compiled into this assembly (Data/answers.txt and Data/allowed.txt).
///
/// This is the only place in the core that touches a data source. It is read-only, deterministic and
/// loaded once (Lazy is thread-safe), so every other function can simply receive a WordDictionary
/// as a parameter and stay pure (Lecture 2: "Accept data and dependencies as parameters").
/// </summary>
public static class EmbeddedWordLists
{
    private static readonly Lazy<WordDictionary> Instance = new(() =>
        WordDictionary.Create(ReadLines("answers.txt"), ReadLines("allowed.txt")));

    public static WordDictionary Load() => Instance.Value;

    private static IEnumerable<string> ReadLines(string fileName)
    {
        var resourceName = $"WordDuel.Core.Data.{fileName}";
        using var stream = typeof(EmbeddedWordLists).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded word list '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
