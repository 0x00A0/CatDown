namespace CatDown.App.Tests;

// A temporary project folder with a few Markdown files.
public sealed class TestProject : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "catdown-app-tests", Guid.NewGuid().ToString("N"));

    public TestProject()
    {
        Write("README.md", "# Hello\n\nFirst sentence. Second sentence.\n");
        Write("guide/setup.md", "Install it. Then run it.\n\n```\ncode\n```\n");
        Write("guide/api.md", "Call the API.\n");
    }

    public string Write(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public string Read(string relativePath) => File.ReadAllText(System.IO.Path.Combine(Path, relativePath));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
    }
}
