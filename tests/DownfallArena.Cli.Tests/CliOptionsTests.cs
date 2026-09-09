namespace DownfallArena.Cli.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void The_studio_command_defaults_to_the_repository_layout()
    {
        var options = CliOptions.Parse(["studio"]);

        options.Command.ShouldBe("studio");
        options.Data.ShouldBe(CliOptions.DefaultData);
        options.Port.ShouldBe(CliOptions.DefaultPort);
        options.SchemaPath.ShouldBe(CliOptions.DefaultSchemaPath);
    }

    [Fact]
    public void The_studio_takes_its_own_port_and_content_directory()
    {
        var options = CliOptions.Parse(["studio", "--port", "5100", "--data", "other/data"]);

        options.Port.ShouldBe(5100);
        options.Data.ShouldBe("other/data");
    }

    /// <summary>A bad port is one line to read, not a stack from the host trying to bind it.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void A_port_the_host_cannot_bind_is_refused_while_parsing(string port)
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["studio", "--port", port]))
            .Message.ShouldContain("between 1 and 65535");
    }

    [Fact]
    public void An_unknown_option_names_itself()
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["studio", "--porrt", "5100"]))
            .Message.ShouldContain("--porrt");
    }
}
