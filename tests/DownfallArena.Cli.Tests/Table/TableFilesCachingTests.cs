using System.IO.Compression;
using System.Text;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// How the table's files reach a phone (ADR 0166): tagged, answered 304 when the phone's copy is current, compressed
/// when it takes Brotli or gzip, kept a day when they are pictures, asked again on every load when they are not, and
/// read again when they changed on disk.
/// </summary>
public sealed class TableFilesCachingTests : IDisposable
{
    private static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "table");

    private readonly string _copy = Path.Combine(Path.GetTempPath(), $"table-files-{Guid.CreateVersion7():N}");

    public void Dispose()
    {
        if (Directory.Exists(_copy))
        {
            Directory.Delete(_copy, recursive: true);
        }
    }

    private static TableFiles Files => new(Shipped);

    private static string Header(DownfallArena.Cli.Studio.StudioResponse response, string name) =>
        response.Headers!.Single(header => header.Key == name).Value;

    [Fact]
    public void A_file_the_phone_already_holds_is_answered_304_with_no_body()
    {
        var files = Files;
        var tag = Header(files.Get("/lobby.js"), "ETag");

        var again = files.Get("/lobby.js", tag, acceptEncoding: null);

        again.Status.ShouldBe(304);
        again.Body.ShouldBeEmpty();
    }

    [Fact]
    public void A_copy_held_compressed_is_current_too()
    {
        var files = Files;
        var tag = Header(files.Get("/lobby.js", null, "gzip, deflate, br"), "ETag");

        files.Get("/lobby.js", tag, "gzip, deflate, br").Status.ShouldBe(304);
        files.Get("/lobby.js", tag, null).Status.ShouldBe(304, "the bytes are the same file, whatever they were sent in");
    }

    [Fact]
    public void Another_tag_is_answered_with_the_file()
    {
        Files.Get("/lobby.js", "\"0000\"", null).Status.ShouldBe(200);
    }

    [Theory]
    [InlineData("gzip, deflate, br", "br")]
    [InlineData("gzip", "gzip")]
    public void A_script_is_sent_compressed_to_a_browser_that_takes_it_and_reads_back_as_the_file(string accepted, string encoding)
    {
        var raw = File.ReadAllBytes(Path.Combine(Shipped, "lobby.js"));

        var answer = Files.Get("/lobby.js", null, accepted);

        Header(answer, "Content-Encoding").ShouldBe(encoding);
        answer.Body.Length.ShouldBeLessThan(raw.Length);
        Decompressed(answer.Body, encoding).ShouldBe(raw);
    }

    [Fact]
    public void A_browser_that_takes_no_compression_is_sent_the_file_as_it_is()
    {
        var answer = Files.Get("/lobby.js", null, null);

        answer.Headers!.ShouldNotContain(header => header.Key == "Content-Encoding");
        answer.Body.ShouldBe(File.ReadAllBytes(Path.Combine(Shipped, "lobby.js")));
    }

    [Fact]
    public void Pages_and_scripts_are_asked_again_on_every_load_and_pictures_kept_a_day()
    {
        var files = Files;

        Header(files.Get("/lobby"), "Cache-Control").ShouldBe(TableFiles.RevalidatedCache);
        Header(files.Get("/sw.js"), "Cache-Control").ShouldBe(TableFiles.RevalidatedCache);
        Header(files.Get("/icon-192.png"), "Cache-Control").ShouldBe(TableFiles.LastingCache);
        Header(files.Get("/fonts/marcellus-latin-400.woff2"), "Cache-Control").ShouldBe(TableFiles.LastingCache);
    }

    [Fact]
    public void A_picture_is_never_compressed_again()
    {
        Files.Get("/icon-192.png", null, "br").Headers!.ShouldNotContain(header => header.Key == "Content-Encoding");
    }

    [Fact]
    public async Task A_file_changed_on_disk_is_read_again_under_a_new_tag()
    {
        Directory.CreateDirectory(_copy);
        var page = Path.Combine(_copy, "lobby.html");
        await File.WriteAllTextAsync(page, "<title>before</title>", TestContext.Current.CancellationToken);
        var files = new TableFiles(_copy);
        var before = files.Get("/lobby");

        await File.WriteAllTextAsync(page, "<title>after</title>", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(page, DateTime.UtcNow.AddMinutes(1));
        var after = files.Get("/lobby", Header(before, "ETag"), null);

        after.Status.ShouldBe(200);
        Encoding.UTF8.GetString(after.Body).ShouldContain("after");
        Header(after, "ETag").ShouldNotBe(Header(before, "ETag"));
    }

    private static byte[] Decompressed(byte[] body, string encoding)
    {
        using var input = new MemoryStream(body);
        using Stream reading = encoding == "br" ? new BrotliStream(input, CompressionMode.Decompress) : new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        reading.CopyTo(output);
        return output.ToArray();
    }
}
