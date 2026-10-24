using System.Text;
using CatDown.Core.Glossary;

namespace CatDown.Core.Llm;

public sealed record TranslationRequest(
    string SourceLanguage,
    string TargetLanguage,
    string Sentence,
    string BlockSource,
    IReadOnlyList<(string Source, string Target)> DoneInBlock,
    IReadOnlyList<GlossaryEntry> Terms);

public static class TranslationPrompt
{
    public static string LanguageName(string code) => code.Split('-')[0].ToLowerInvariant() switch
    {
        "zh" => "Simplified Chinese",
        "en" => "English",
        "sv" => "Swedish",
        var other => other,
    };

    public static IReadOnlyList<ChatMessage> Build(TranslationRequest request)
    {
        var source = LanguageName(request.SourceLanguage);
        var target = LanguageName(request.TargetLanguage);
        var system = $"""
            You are a professional translator. Translate one sentence of a Markdown document from {source} to {target}.
            Keep every piece of Markdown inline markup exactly as it is in the source: **bold**, *italic*, `code`, links, images, and HTML tags. Do not translate code or URLs.
            Use the glossary translations when a term appears.
            Reply with the translation of that one sentence only: no quotes, no notes, no explanations.
            """;

        var user = new StringBuilder();
        user.AppendLine("Paragraph the sentence comes from:").AppendLine(request.BlockSource).AppendLine();
        if (request.DoneInBlock.Count > 0)
        {
            user.AppendLine("Sentences of this paragraph already translated:");
            foreach (var (s, t) in request.DoneInBlock)
                user.AppendLine($"- {s} => {t}");
            user.AppendLine();
        }
        if (request.Terms.Count > 0)
        {
            user.AppendLine("Glossary:");
            foreach (var term in request.Terms)
                user.AppendLine(term.Note.Length > 0 ? $"- {term.Source} => {term.Target} ({term.Note})" : $"- {term.Source} => {term.Target}");
            user.AppendLine();
        }
        user.AppendLine("Sentence to translate:").Append(request.Sentence);

        return [new ChatMessage("system", system), new ChatMessage("user", user.ToString())];
    }

    // Models sometimes wrap the answer in a code fence or quotes despite the instructions.
    public static string CleanReply(string reply, string sentence)
    {
        var text = reply.Trim();
        if (text.StartsWith("```") && text.EndsWith("```") && text.Length >= 6)
        {
            var firstNewline = text.IndexOf('\n');
            text = firstNewline > 0 ? text[(firstNewline + 1)..^3].Trim() : text[3..^3].Trim();
        }
        if (text.Length >= 2 && (text[0], text[^1]) is ('"', '"') or ('“', '”') && !(sentence.StartsWith('"') || sentence.StartsWith('“')))
            text = text[1..^1].Trim();
        return text;
    }
}
