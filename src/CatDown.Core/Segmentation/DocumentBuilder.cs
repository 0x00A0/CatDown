using System.Security.Cryptography;
using System.Text;
using CatDown.Core.Model;

namespace CatDown.Core.Segmentation;

public sealed class DocumentBuilder(ISentenceSplitter splitter, string sourceLanguage)
{
    public FileDocument Build(string sourcePath, ParsedFile file)
    {
        var document = new FileDocument { SourcePath = sourcePath, SourceHash = Hash(file.Text) };
        foreach (var parsed in file.Blocks)
            document.Blocks.Add(BuildBlock(document, parsed));
        return document;
    }

    public Block BuildBlock(FileDocument document, ParsedBlock parsed)
    {
        var block = new Block
        {
            Id = document.NewBlockId(),
            Kind = parsed.Kind,
            Verbatim = parsed.Verbatim,
            Source = parsed.Text,
        };

        if (parsed.Verbatim)
        {
            block.Segments.Add(new Segment
            {
                Id = document.NewSegmentId(),
                Source = parsed.Text,
                Target = parsed.Text,
                Status = SegmentStatus.Confirmed,
            });
            return block;
        }

        foreach (var span in splitter.Split(parsed.Text, parsed.Protected, sourceLanguage))
        {
            block.Segments.Add(new Segment
            {
                Id = document.NewSegmentId(),
                Source = parsed.Text.Substring(span.Start, span.Length),
            });
        }
        return block;
    }

    public static string Hash(string text) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
