#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Highlighting;
using Xunit;
using static System.ConsoleKey;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static PrettyPrompt.Highlighting.AnsiColor;

namespace PrettyPrompt.Tests;

public class SyntaxHighlightingTests
{
    [Fact]
    public async Task ReadLine_SyntaxHighlight()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"red green nocolor blue{Enter}");

        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                HighlightCallback = new SyntaxHighlighterTestData().HighlightHandlerAsync
            },
            console: console
        );

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("red green nocolor blue", result.Text);
        var output = console.GetAllOutput();

        // although the words are typed character-by-character, we should still "go back" and redraw
        // it once we know the word should be drawn in a syntax-highlighted color.
        Assert.Contains(
            GetMoveCursorLeft("red".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.RedFormat) + "red" + Reset, // when we press 'd' go back two chars and to rewrite the word "red"
            output
        );
        Assert.Contains(
            GetMoveCursorLeft("green".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.GreenFormat) + "green" + Reset,
            output
        );
        Assert.Contains(
            GetMoveCursorLeft("blue".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.BlueFormat) + "blue" + Reset,
            output
        );

        Assert.DoesNotContain("nocolor", output); // it should output character by character as we type; never the whole string at once.
    }

    [Fact]
    public async Task ReadLine_CJKCharacters_SyntaxHighlight()
    {
        var format1 = new ConsoleFormat(Foreground: Red);
        var format2 = new ConsoleFormat(Foreground: Blue);
        var format3 = new ConsoleFormat(Foreground: Green);

        var console = ConsoleStub.NewConsole(width: 20);
        console.StubInput($"苹果 o 蓝莓 o avocado o{Enter}");

        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                HighlightCallback = new SyntaxHighlighterTestData(new Dictionary<string, AnsiColor>
                {
                    { "苹果", format1.Foreground!.Value },
                    { "蓝莓", format2.Foreground!.Value },
                    { "avocado", format3.Foreground!.Value }
                }).HighlightHandlerAsync
            },
            console: console
        );

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("苹果 o 蓝莓 o avocado o", result.Text);
        var output = console.GetAllOutput();

        // although the words are typed character-by-character, we should still "go back" and redraw
        // it once we know the word should be drawn in a syntax-highlighted color.
        Assert.Contains(
            GetMoveCursorLeft(2) + ToAnsiEscapeSequenceSlow(format1) + "苹果" + Reset,
            output
        );

        Assert.Contains(
            GetMoveCursorLeft(2) + ToAnsiEscapeSequenceSlow(format2) + "蓝莓" + Reset,
            output
        );

        // avocado is green, but wrapped because the console width is narrow.
        Assert.Contains(
            output,
            str => str.Contains(ToAnsiEscapeSequenceSlow(format3) + "avoc\n")
        );

        Assert.Contains(
            output,
            str => str.Contains("ado" + Reset)
        );
    }

    [Theory]
    [InlineData("var x = \"abcdefg🙃hijklmnop\";")] // 🙃 (U+1F643): surrogate pair -> 2 chars, 2 columns
    [InlineData("var x = \"ábcdefghij\";")]    // a + combining acute -> 2 chars, 1 column
    public void ApplyColorToCharacters_HighlightEndingAfterMultiCharCluster_DoesNotBleedOntoNextCharacter(string text)
    {
        // Highlight spans use UTF-16 offsets, so this string-literal span ends exactly before the trailing ';'.
        // When a grapheme cluster inside the string spans more than one UTF-16 char (a surrogate-pair emoji, or a
        // base char + combining mark), the renderer must measure the span end in chars - not cells/clusters - or
        // the string color bleeds onto the ';'. Regression test for the CSharpRepl emoji highlighting bug.
        int stringStart = text.IndexOf('"');
        int stringLength = text.LastIndexOf('"') - stringStart + 1;
        var highlight = new FormatSpan(stringStart, stringLength, AnsiColor.Red);

        var rows = CellRenderer.ApplyColorToCharacters(new[] { highlight }, text, textWidth: 200);
        var row = rows[0];

        int semicolon = -1;
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i].Text == ";") { semicolon = i; break; }
        }
        Assert.True(semicolon > 0, "expected to find a ';' cell");

        Assert.Equal(AnsiColor.Red, row[semicolon - 1].Formatting.Foreground); // closing '"' is highlighted
        Assert.Null(row[semicolon].Formatting.Foreground);                     // the ';' must NOT be (the bug)
    }
}
