#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.IntegrationTests;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException($"Unknown options '{string.Join(" ", args)}'.");

        switch (args[0])
        {
            case "record":
                {
                    Console.Clear();
                    Console.WriteLine("Start typing:");
                    var console = new RecordingConsole();
                    await Run(console);
                    console.Save("record");
                }
                break;

            case "run":
                {
                    var tests = new[]
                    {
                        async () => await Tests.Test_228_229(false),
                        async () => await Tests.Test_228_229(true)
                    };

                    foreach (var test in tests)
                    {
                        // Note: the console is cleared and the "Start typing:" banner is printed inside
                        // each test (after the buffer is resized) to keep the initial cursor deterministic.
                        await test();
                    }

                    Console.Clear();
                    Console.WriteLine("All integration tests ran successfully");
                }
                break;

            case "regen":
                {
                    // Rewrites the expected output logs (Data/*.output.txt) from current behavior,
                    // reusing the recorded input keystrokes. Run from a real terminal.
                    // (Clear + banner happen inside the replay, after the buffer is resized.)
                    foreach (var biggerBufferThanWindow in new[] { false, true })
                    {
                        await Tests.Regen_228_229(biggerBufferThanWindow);
                    }

                    Console.Clear();
                    Console.WriteLine("Regenerated expected output logs.");
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown argument '{args[0]}'.");
        }
    }

    internal static async Task Run(IConsole console)
    {
        var prompt = new Prompt(
            // No persistent history file: it would carry submitted entries across runs, and the
            // replayed history-navigation keys would then render differently from the recorded
            // (clean-state) golden output, making the test non-deterministic. In-session history
            // still works, so the replay depends only on the fixed recorded keystrokes.
            persistentHistoryFilepath: null,
            callbacks: new PrettyPrompt.Program.FruitPromptCallbacks(),
            console: console,
            configuration: new PromptConfiguration(
                prompt: new FormattedString(">>> ", new FormatSpan(0, 1, AnsiColor.Red), new FormatSpan(1, 1, AnsiColor.Yellow), new FormatSpan(2, 1, AnsiColor.Green)),
                completionItemDescriptionPaneBackground: AnsiColor.Rgb(30, 30, 30),
                selectedCompletionItemBackground: AnsiColor.Rgb(30, 30, 30),
                selectedTextBackground: AnsiColor.Rgb(20, 61, 102)));

        while (true)
        {
            var response = await prompt.ReadLineAsync().ConfigureAwait(false);
            if (response.IsSuccess)
            {
                if (response.Text == "exit") break;
                // optionally, use response.CancellationToken so the user can
                // cancel long-running processing of their response via ctrl-c
                console.WriteLine("You wrote " + (response.SubmitKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) ? response.Text.ToUpper() : response.Text));
            }
        }
    }
}