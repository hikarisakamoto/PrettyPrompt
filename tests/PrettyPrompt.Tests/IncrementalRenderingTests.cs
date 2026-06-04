#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

public class IncrementalRenderingTests
{
    /// <summary>
    /// Regression test for CSharpRepl issue #411 (https://github.com/waf/CSharpRepl/issues/411).
    /// </summary>
    [Fact]
    public void CalculateDiff_ColoredNewlineShiftedLeft_ErasesGhostCharacterAtNewlineColumn()
    {
        // The string-literal highlight color. Any non-default format reproduces the bug; the point is only that
        // the newline cell is NOT formatted with the default format.
        var stringColor = new ConsoleFormat(Foreground: AnsiColor.Red);
        var ansiCoordinate = new ConsoleCoordinate(1, 1);

        // BEFORE Shift+Enter: a single line `("""`+`)` where the unterminated raw string literal colors the
        // quotes and the trailing ')'.
        var before = new Screen(
            width: 20, height: 5, new ConsoleCoordinate(0, 5),
            new ScreenArea(ConsoleCoordinate.Zero, new[] { CodeRow(("(", default), ("\"\"\"", stringColor), (")", stringColor)) }, TruncateToScreenHeight: false));

        // AFTER Shift+Enter at the caret between `"""` and `)`: the ')' moves to the next line and the raw string
        // literal now spans the newline, so the newline cell is colored too.
        var after = new Screen(
            width: 20, height: 5, new ConsoleCoordinate(1, 0),
            new ScreenArea(ConsoleCoordinate.Zero, new[]
            {
                CodeRow(("(", default), ("\"\"\"", stringColor), ("\n", stringColor)),
                CodeRow((")", stringColor)),
            }, TruncateToScreenHeight: false));

        // Mimic the way a real console works: first the full draw of `before`, then the
        // incremental diff that turns `before` into `after`. What remains on the (emulated) screen is what the
        // user actually sees.
        var terminal = new FakeTerminal(ansiCoordinate);
        terminal.Apply(IncrementalRendering.CalculateDiff(before, new Screen(0, 0, ConsoleCoordinate.Zero), ansiCoordinate));
        terminal.Apply(IncrementalRendering.CalculateDiff(after, before, ansiCoordinate));

        Assert.Equal("(\"\"\"", terminal.ReadRow(row: 1, width: 20)); // first line ends at the quotes...
        Assert.Equal(")", terminal.ReadRow(row: 2, width: 20));       // ...the ')' is only on the second line
        Assert.DoesNotContain(")", terminal.ReadRow(row: 1, width: 20)); // and never lingers as a ghost on the first
    }

    private static Row CodeRow(params (string text, ConsoleFormat format)[] segments)
    {
        var row = new Row(capacity: 8);
        foreach (var (text, format) in segments)
        {
            row.Add(text, format);
        }
        return row;
    }

    /// <summary>
    /// A minimal ANSI terminal that records the visible character at each screen coordinate. It understands only
    /// what <see cref="IncrementalRendering"/> emits: printable text, '\n', the cursor moves (CSI A/B/C/D/G) and
    /// color/clear sequences (which it ignores, as they don't change which character occupies a cell). The '\n'
    /// handling mirrors <c>IncrementalRendering.UpdateCoordinateFromNewLine</c> so the emulated cursor tracks the
    /// renderer's own assumption on each platform.
    /// </summary>
    private sealed class FakeTerminal
    {
        private const char Escape = (char)27;

        private readonly Dictionary<(int row, int col), char> grid = new();
        private readonly int originColumn;
        private int row;
        private int col;

        public FakeTerminal(ConsoleCoordinate origin)
        {
            row = origin.Row;
            col = origin.Column;
            originColumn = origin.Column;
        }

        public void Apply(string ansi)
        {
            int i = 0;
            while (i < ansi.Length)
            {
                char c = ansi[i];
                if (c == Escape && i + 1 < ansi.Length && ansi[i + 1] == '[')
                {
                    int paramStart = i + 2;
                    int j = paramStart;
                    while (j < ansi.Length && !char.IsLetter(ansi[j])) j++;
                    if (j < ansi.Length)
                    {
                        ApplyControlSequence(ansi[j], ansi.Substring(paramStart, j - paramStart));
                    }
                    i = j + 1;
                }
                else if (c == '\n')
                {
                    row++;
                    // Matches UpdateCoordinateFromNewLine: on non-Windows a '\n' also returns to the first column.
                    if (!OperatingSystem.IsWindows()) col = originColumn;
                    i++;
                }
                else
                {
                    grid[(row, col)] = c;
                    col++;
                    i++;
                }
            }
        }

        private void ApplyControlSequence(char command, string parameters)
        {
            int.TryParse(parameters.Split(';')[0], out int n);
            switch (command)
            {
                case 'A': row -= n; break;
                case 'B': row += n; break;
                case 'C': col += n; break;
                case 'D': col -= n; break;
                case 'G': col = n; break;
                // 'm' (color) and 'K'/'J' (clear) don't change which character occupies a cell here.
            }
        }

        public string ReadRow(int row, int width)
        {
            var sb = new StringBuilder();
            for (int x = originColumn; x < originColumn + width; x++)
            {
                sb.Append(grid.TryGetValue((row, x), out var ch) ? ch : ' ');
            }
            return sb.ToString().TrimEnd();
        }
    }
}
