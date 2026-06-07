using PrettyPrompt.Consoles;
using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

public class ScreenTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("ab", 2)]
    [InlineData("abc", 3)]

    [InlineData("书", 2)]
    [InlineData("a书", 3)]
    [InlineData("a书bc", 5)]
    [InlineData("a书上bc", 7)]

    [InlineData("😀", 2)]
    [InlineData("a😁", 3)]
    [InlineData("a😉bc", 5)]
    [InlineData("a😐🙄bc", 7)]

    //different emojis (some had incorrectly specified width in UnicodeWidth.GetWidth)
    //circles
    [InlineData("⚪", 2)]
    [InlineData("⚫", 2)]
    [InlineData("⭕", 2)]
    [InlineData("🔴", 2)]
    [InlineData("🔵", 2)]
    [InlineData("🟠", 2)]
    [InlineData("🟡", 2)]
    [InlineData("🟢", 2)]
    [InlineData("🟣", 2)]
    [InlineData("🟤", 2)]

    [InlineData("⚡", 2)]
    [InlineData("💡", 2)]
    [InlineData("❌", 2)]
    [InlineData("✅", 2)]

    //squares
    [InlineData("⬛", 2)]
    [InlineData("🟫", 2)]
    [InlineData("🟪", 2)]
    [InlineData("🟦", 2)]
    [InlineData("🟩", 2)]
    [InlineData("🟨", 2)]
    [InlineData("🟧", 2)]
    [InlineData("🟥", 2)]
    [InlineData("⬜", 2)]

    [InlineData("🔷", 2)]
    [InlineData("🔶", 2)]

    // Multi-codepoint grapheme clusters that previously crashed cursor positioning (issue #270).
    // Each is a single cluster occupying two columns, so the cursor ends up at column 2.
    // Literals use escapes because the sequences contain invisible joiners/selectors.
    [InlineData("\U0001F926\U0001F3FC", 2)]                       // 🤦🏼 face palm + Fitzpatrick skin-tone modifier
    [InlineData("\U0001F926\u200D\u2642", 2)]                     // 🤦‍♂ face palm + ZWJ + male sign
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0F", 2)]     // 🤦🏼‍♂️ full ZWJ sequence with variation selector
    [InlineData("\u79B0\U000E0100", 2)]                           // 禰󠄀 CJK ideograph + variation selector supplement (U+E0100)
    [InlineData("a\U0001F926\U0001F3FC", 3)]                      // narrow char then a two-column cluster

    // base char + combining mark is a single-column cluster: the cursor lands one column past it, not two.
    [InlineData("e\u0301", 1)]                                    // e + combining acute accent (decomposed "é")
    [InlineData("ae\u0301b", 3)]                                  // a + combining "é" + b
    // Halfwidth katakana + (semi-)voiced sound mark: each kana+mark pair is ONE grapheme cluster, but the
    // sound mark is a SPACING extender that takes its own halfwidth cell, so a pair is two columns. The
    // cursor must land at column 4 past "ﾊﾟｸﾞ" (= パグ "pug"), matching what the terminal renders - not 2.
    // See https://github.com/microsoft/terminal/issues/18087 and issue #270.
    [InlineData("ﾊﾟｸﾞ", 4)]                   // ﾊﾟｸﾞ = U+FF8A + semi-voiced U+FF9F + U+FF78 + voiced U+FF9E
    [InlineData("aﾊﾟb", 4)]                            // a + ﾊﾟ kana+mark cluster (2 columns) + b
    public void ScreenCursorPositionTest(string text, int expectedCursorPosition)
    {
        var screen = new Screen(
            width: 128,
            height: 16,
            new ConsoleCoordinate(0, text.Length), //cursor at the end of the text
            new ScreenArea(
                new ConsoleCoordinate(0, 0),
                new[]
                {
                    new Row(text)
                }));

        Assert.Equal(new ConsoleCoordinate(0, expectedCursorPosition), screen.Cursor);
    }
}
