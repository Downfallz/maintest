using System.Buffers.Text;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

public sealed class PushKeysTests
{
    [Fact]
    public void A_generated_pair_is_an_uncompressed_P256_point_and_a_scalar_in_base64url()
    {
        var keys = PushKeys.Generate();

        var point = Base64Url.DecodeFromChars(keys.PublicKey);
        point.Length.ShouldBe(65);
        point[0].ShouldBe((byte)0x04);
        Base64Url.DecodeFromChars(keys.PrivateKey).Length.ShouldBe(32);
        keys.Subject.ShouldBe(PushKeys.DefaultSubject);
        keys.PublicKey.ShouldNotContain("=");
        PushKeys.Generate().PublicKey.ShouldNotBe(keys.PublicKey);
    }

    [Fact]
    public void The_json_printed_is_the_json_read_back_from_the_environment_or_a_file()
    {
        var keys = PushKeys.Generate() with { Subject = "mailto:table@example.org" };
        var json = keys.ToJson();
        var directory = Directory.CreateTempSubdirectory("push-keys");
        try
        {
            var path = Path.Combine(directory.FullName, "push-keys.json");
            File.WriteAllText(path, json);

            PushKeys.Load(null, json).ShouldBe(keys);
            PushKeys.Load(path, "{}").ShouldBe(keys, "the file wins over the variable");
            PushKeys.Load(null, null).ShouldBeNull();
            PushKeys.Load(null, "   ").ShouldBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_missing_subject_is_the_repository_and_a_pair_that_is_not_one_is_refused_in_one_line()
    {
        var keys = PushKeys.Generate();

        PushKeys.Load(null, $$"""{"publicKey":"{{keys.PublicKey}}","privateKey":"{{keys.PrivateKey}}"}""")!.Subject.ShouldBe(PushKeys.DefaultSubject);
        Should.Throw<InvalidDataException>(() => PushKeys.Load(null, $$"""{"publicKey":"{{keys.PrivateKey}}","privateKey":"{{keys.PrivateKey}}"}""")).Message.ShouldContain("public key");
        Should.Throw<InvalidDataException>(() => PushKeys.Load(null, $$"""{"publicKey":"{{keys.PublicKey}}","privateKey":"not-a-key"}""")).Message.ShouldContain("private key");
        Should.Throw<InvalidDataException>(() => PushKeys.Load(null, "null"));
    }
}
