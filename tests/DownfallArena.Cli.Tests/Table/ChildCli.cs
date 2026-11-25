using System.Diagnostics;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The command line run as a process of its own, as the platform runs it: what a test sends a signal to without
/// sending it to the test run (#1205). Its output is read a line at a time, as the ops report reads it.
/// </summary>
internal sealed class ChildCli : IAsyncDisposable
{
    private readonly Process _process;
    private readonly List<string> _lines = [];
    private readonly List<(Func<string, bool> Match, TaskCompletionSource<string> Seen)> _awaited = [];

    private ChildCli(Process process) => _process = process;

    /// <summary>Every line the process wrote so far, its output and its errors in the order they came.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    /// <summary>
    /// Starts the command line built beside the tests, from the folder where <c>table/</c> is copied. It inherits none of
    /// this run's <c>DOWNFALL_*</c> settings, the relay's among them.
    /// </summary>
    public static ChildCli Start(params string[] arguments) => Start(new Dictionary<string, string>(), arguments);

    /// <summary>The same, with these variables set for it alone: a test's own queue name (#1259).</summary>
    public static ChildCli Start(IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "DownfallArena.Cli.dll"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DOWNFALL_", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(name);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        var process = new Process { StartInfo = start };
        var child = new ChildCli(process);
        process.OutputDataReceived += (_, line) => child.Said(line.Data);
        process.ErrorDataReceived += (_, line) => child.Said(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return child;
    }

    /// <summary>The first line <paramref name="match" /> holds for, whether written already or later, within <see cref="Waits.Within" />.</summary>
    public async Task<string> LineAsync(Func<string, bool> match, CancellationToken cancellationToken)
    {
        var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lines)
        {
            if (_lines.FirstOrDefault(match) is { } line)
            {
                return line;
            }

            _awaited.Add((match, seen));
        }

        var exited = _process.WaitForExitAsync(cancellationToken);
        Task first;
        try
        {
            first = await Task.WhenAny(seen.Task, exited).WaitAsync(Waits.Within, cancellationToken);
        }
        catch (TimeoutException)
        {
            lock (_lines)
            {
                _awaited.RemoveAll(awaited => awaited.Seen == seen);
            }

            throw new TimeoutException($"The line was not written within {Waits.Within}:{Environment.NewLine}{string.Join(Environment.NewLine, Lines)}");
        }

        return first == seen.Task
            ? await seen.Task
            : throw new InvalidOperationException($"The process exited ({_process.ExitCode}) before the line was written:{Environment.NewLine}{string.Join(Environment.NewLine, Lines)}");
    }

    /// <summary>Sends SIGTERM, as the platform stops a replica it scales in, and returns the exit code once the process has stopped.</summary>
    public async Task<int> TerminateAsync(CancellationToken cancellationToken)
    {
        using (var kill = Process.Start("kill", ["-TERM", $"{_process.Id}"]))
        {
            await kill.WaitForExitAsync(cancellationToken);
            kill.ExitCode.ShouldBe(0, _process.HasExited
                ? $"the process had exited ({_process.ExitCode}) before the signal:{Environment.NewLine}{string.Join(Environment.NewLine, Lines)}"
                : "the signal was not sent");
        }

        await _process.WaitForExitAsync(cancellationToken).WaitAsync(Waits.Within, cancellationToken);

        // The last lines are read off the pipes after the process has gone; this waits until they are.
        _process.WaitForExit();
        return _process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(CancellationToken.None);
        }

        _process.Dispose();
    }

    private void Said(string? line)
    {
        if (line is null)
        {
            return;
        }

        List<TaskCompletionSource<string>> told;
        lock (_lines)
        {
            _lines.Add(line);
            told = [.. _awaited.Where(awaited => awaited.Match(line)).Select(awaited => awaited.Seen)];
            _awaited.RemoveAll(awaited => told.Contains(awaited.Seen));
        }

        foreach (var seen in told)
        {
            seen.TrySetResult(line);
        }
    }
}
