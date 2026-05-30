#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Globalization;

namespace PrettyPrompt.Documents;

/// <summary>
/// Helpers for moving between grapheme-cluster boundaries in a string.
///
/// The document caret is a UTF-16 offset, but it should only ever rest on a grapheme-cluster
/// boundary so that cursor movement and deletion operate on whole user-perceived characters -
/// e.g. an emoji such as "🤦🏼‍♂️" (one cluster, seven <see cref="char"/>s) or a base letter plus
/// combining marks - rather than splitting them into halves of a surrogate pair or orphaned
/// combining marks. See https://github.com/waf/PrettyPrompt/issues/270.
/// </summary>
internal static class Grapheme
{
    /// <summary>
    /// The smallest cluster boundary strictly greater than <paramref name="index"/>
    /// (i.e. the caret position one grapheme to the right), clamped to the text length.
    /// </summary>
    public static int NextBoundary(string text, int index)
    {
        if (index < 0) index = 0;
        if (index >= text.Length) return text.Length;
        return index + StringInfo.GetNextTextElementLength(text, index);
    }

    /// <summary>
    /// The largest cluster boundary strictly less than <paramref name="index"/>
    /// (i.e. the caret position one grapheme to the left), clamped to 0.
    /// </summary>
    public static int PreviousBoundary(string text, int index)
    {
        if (index <= 0) return 0;
        if (index > text.Length) index = text.Length;
        int i = 0;
        while (i < text.Length)
        {
            int next = i + StringInfo.GetNextTextElementLength(text, i);
            if (next >= index) return i;
            i = next;
        }
        return i;
    }

    /// <summary>
    /// The largest cluster boundary less than or equal to <paramref name="index"/>. Snaps an index
    /// that may have landed in the middle of a cluster (e.g. from column-based vertical navigation)
    /// back onto a boundary, without moving it if it is already on one.
    /// </summary>
    public static int RoundDownToBoundary(string text, int index)
    {
        if (index <= 0) return 0;
        if (index >= text.Length) return text.Length;
        int i = 0;
        while (i < text.Length)
        {
            int next = i + StringInfo.GetNextTextElementLength(text, i);
            if (next == index) return index; // already on a boundary
            if (next > index) return i;      // index is inside [i, next): snap back to the cluster start
            i = next;
        }
        return i;
    }
}
