#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PrettyPrompt.IntegrationTests;

internal class Tests
{
    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/228
    /// https://github.com/waf/PrettyPrompt/issues/229
    /// </summary>
    public static async Task Test_228_229(bool biggerBufferThanWindow)
    {
        var console = await Replay(biggerBufferThanWindow);
        File.WriteAllLines("record#actual.output.txt", console.ReplayOutputLog); //for debugging: the actual replay output
        CheckOutputLogs(console.ReplayOutputLog, console.RecordedOutputLog);
    }

    /// <summary>
    /// Regenerates the recorded (expected) output log for the given variant from the current behavior,
    /// reusing the existing recorded input keystrokes (only the .output.txt file is rewritten).
    /// Invoked via the "regen" program argument; must be run from a real terminal because the replay
    /// writes to the live console and tracks its cursor positions. Use this after an intentional change
    /// to rendering or completion behavior makes <see cref="Test_228_229"/> diverge.
    /// </summary>
    public static async Task Regen_228_229(bool biggerBufferThanWindow)
    {
        var console = await Replay(biggerBufferThanWindow);
        File.WriteAllLines(GetDataPath(DataName(biggerBufferThanWindow)) + ".output.txt", console.ReplayOutputLog);
    }

    private static async Task<ReplayingConsole> Replay(bool biggerBufferThanWindow)
    {
        if (OperatingSystem.IsWindows())
        {
            Console.WindowWidth = 120;
            Console.WindowHeight = 30;
            Console.BufferWidth = 120;
            Console.BufferHeight = biggerBufferThanWindow ? 3000 : 30;
        }

        // Clear and print the banner AFTER resizing the buffer. Resizing moves the cursor by an
        // amount that depends on the console's leftover scroll position from the previous run, so if
        // we cleared before resizing the initial CursorTop alternated between 0 and 1 across runs and
        // the replay was non-deterministic. Doing Clear last pins the cursor to a known row (1, just
        // below the banner), which is what the recorded golden output expects.
        Console.Clear();
        Console.WriteLine("Start typing:");

        var console = new ReplayingConsole(GetDataPath(DataName(biggerBufferThanWindow)));
        await Program.Run(console);
        return console;
    }

    private static string DataName(bool biggerBufferThanWindow) => "record#228#229" + (biggerBufferThanWindow ? "a" : "b");

    private static void CheckOutputLogs(IReadOnlyList<string> replayOutputLog, IReadOnlyList<string> recordedOutputLog)
    {
        if (replayOutputLog.Count != recordedOutputLog.Count) Throw();
        for (int i = 0; i < replayOutputLog.Count; i++)
        {
            if (replayOutputLog[i] != recordedOutputLog[i]) Throw();
        }

        static void Throw() => throw new InvalidOperationException("Recorded (=expected) output log and output log of recorded input replay (=actual) differs.");
    }

    private static string GetDataPath(string name) => Path.Combine("..", "..", "..", "Data", name);
}