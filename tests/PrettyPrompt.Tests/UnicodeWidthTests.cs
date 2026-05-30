#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

public class UnicodeWidthTests
{
    [Theory]
    // basics
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abc", 3)]
    [InlineData("\u4E66", 2)]            // 书 full-width CJK
    [InlineData("a\u4E66bc", 5)]         // mixed narrow + wide

    // single emoji (supplementary plane)
    [InlineData("\U0001F600", 2)]        // 😀

    // multi-codepoint grapheme clusters from issue #270 — each is a single two-column cell,
    // NOT the sum of its parts (which previously produced widths of 3-5 and crashed).
    [InlineData("\U0001F926\U0001F3FC", 2)]                       // 🤦🏼 base + skin-tone modifier
    [InlineData("\U0001F926\u200D\u2642", 2)]                     // 🤦‍♂ base + ZWJ + male sign
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0F", 2)]     // 🤦🏼‍♂️ full ZWJ sequence
    [InlineData("\u79B0\U000E0100", 2)]                           // 禰󠄀 CJK + variation selector supplement (U+E0100)

    // base char + combining mark is one column (the trailing mark adds nothing)
    [InlineData("e\u0301", 1)]                                    // é = e + combining acute accent

    // multiple clusters sum their (capped) widths
    [InlineData("a\U0001F926\U0001F3FCb", 4)]                     // narrow + two-column cluster + narrow

    // narrow supplementary glyph that the old code mis-sized (issue called this out): a playing card is 1
    [InlineData("\U0001F0A1", 1)]                                 // 🂡 playing card ace of spades
    public void GetWidth_ReturnsExpectedDisplayWidth(string text, int expectedWidth)
    {
        Assert.Equal(expectedWidth, UnicodeWidth.GetWidth(text));
    }

    [Theory]
    // a single grapheme cluster never exceeds two columns, regardless of how many scalars it contains
    [InlineData("\U0001F926", 2)]                                 // 🤦
    [InlineData("\U0001F926\U0001F3FC", 2)]                       // 🤦🏼
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0F", 2)]     // 🤦🏼‍♂️
    [InlineData("\u79B0\U000E0100", 2)]                           // 禰󠄀
    [InlineData("a", 1)]                                          // narrow
    [InlineData("\u4E66", 2)]                                     // 书 wide
    public void GetGraphemeClusterWidth_IsCappedAtTwo(string cluster, int expectedWidth)
    {
        Assert.Equal(expectedWidth, UnicodeWidth.GetGraphemeClusterWidth(cluster));
        Assert.InRange(UnicodeWidth.GetGraphemeClusterWidth(cluster), 0, 2);
    }

    [Theory]
    // GetLengthThatFits returns a CHAR count whose display width fits the column budget, on a cluster boundary.
    [InlineData("abcde", 3, 3)]                          // narrow: 3 columns == 3 chars
    [InlineData("\u4E66\u4E66\u4E66", 4, 2)]                          // each wide char is 2 columns; 4 columns fits 2 chars
    [InlineData("\u4E66\u4E66\u4E66", 5, 2)]                          // 5 columns: a 3rd wide char would be 6, so stop at 2 chars
    [InlineData("a\u4E66", 1, 1)]                            // 'a' fits 1 column; 书 would overflow
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0Fx", 2, 7)]  // the width-2 emoji (7 chars) fits in 2 columns; 'x' would not
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0Fx", 1, 0)]  // the emoji is 2 columns wide and cannot fit in 1
    public void GetLengthThatFits_TruncatesByWidthOnClusterBoundary(string text, int maxWidth, int expectedLength)
        => Assert.Equal(expectedLength, UnicodeWidth.GetLengthThatFits(text, maxWidth));
}
