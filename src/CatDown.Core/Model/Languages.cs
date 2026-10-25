namespace CatDown.Core.Model;

public static class Languages
{
    public static readonly IReadOnlyList<string> Supported = ["zh", "en", "sv"];

    // Languages written without spaces between sentences.
    public static bool JoinsWithoutSpace(string language) =>
        language.Split('-')[0].ToLowerInvariant() is "zh" or "ja" or "ko";

    public static string SentenceSeparator(string language) => JoinsWithoutSpace(language) ? "" : " ";
}
