#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Documents;
using Xunit;

namespace PrettyPrompt.Tests;

public class GraphemeTests
{
    // 🤦🏼‍♂️ = U+1F926 U+1F3FC U+200D U+2642 U+FE0F = 7 chars, one grapheme cluster.
    private const string Emoji = "\U0001F926\U0001F3FC\u200D\u2642\uFE0F";

    // "a" + emoji + "b": 'a'=[0], emoji=[1..8), 'b'=[8], length 9.
    private const string Text = "a" + Emoji + "b";

    [Theory]
    [InlineData(0, 1)] // past 'a'
    [InlineData(1, 8)] // past the whole emoji cluster (not just one surrogate)
    [InlineData(8, 9)] // past 'b'
    [InlineData(9, 9)] // clamped at end
    public void NextBoundary_StepsOverWholeCluster(int index, int expected)
        => Assert.Equal(expected, Grapheme.NextBoundary(Text, index));

    [Theory]
    [InlineData(9, 8)] // before 'b'
    [InlineData(8, 1)] // before the whole emoji cluster
    [InlineData(1, 0)] // before 'a'
    [InlineData(0, 0)] // clamped at 0
    public void PreviousBoundary_StepsOverWholeCluster(int index, int expected)
        => Assert.Equal(expected, Grapheme.PreviousBoundary(Text, index));

    [Theory]
    [InlineData(1, 1)] // already on a boundary
    [InlineData(4, 1)] // inside the emoji -> snap back to its start
    [InlineData(8, 8)] // already on a boundary
    [InlineData(0, 0)]
    [InlineData(9, 9)]
    public void RoundDownToBoundary_SnapsMidClusterIndices(int index, int expected)
        => Assert.Equal(expected, Grapheme.RoundDownToBoundary(Text, index));

    // "ﾊﾟｸﾞ" = パグ ("pug") in halfwidth katakana = U+FF8A U+FF9F U+FF78 U+FF9E. The voiced / semi-voiced
    // sound marks (U+FF9E/U+FF9F) are grapheme extenders, so each kana+mark pair is ONE cluster: the caret
    // steps over a pair as a single editing unit (it never lands between a kana and its mark), even though
    // each pair displays as two columns. See https://github.com/waf/PrettyPrompt/issues/270.
    private const string Pug = "ﾊﾟｸﾞ";

    [Theory]
    [InlineData(0, 2)] // past ﾊﾟ (both chars of the first cluster)
    [InlineData(2, 4)] // past ｸﾞ
    [InlineData(4, 4)] // clamped at end
    public void NextBoundary_TreatsKanaPlusSoundMarkAsOneCluster(int index, int expected)
        => Assert.Equal(expected, Grapheme.NextBoundary(Pug, index));

    [Theory]
    [InlineData(4, 2)] // before ｸﾞ
    [InlineData(2, 0)] // before ﾊﾟ
    [InlineData(0, 0)] // clamped at 0
    public void PreviousBoundary_TreatsKanaPlusSoundMarkAsOneCluster(int index, int expected)
        => Assert.Equal(expected, Grapheme.PreviousBoundary(Pug, index));
}
