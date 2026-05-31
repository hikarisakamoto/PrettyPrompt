#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// Given the text and the syntax highlighting information, render the text into the "cells" of the terminal screen.
/// </summary>
internal static class CellRenderer
{
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, SelectionSpan? selection, AnsiColor? selectedTextBackground)
        => ApplyColorToCharacters(highlights, lines, selection, selectedTextBackground, startLine: 0, endLine: lines.Count);

    /// <summary>
    /// Builds the <see cref="Row"/>/<see cref="Cell"/>s for the wrapped lines in the half-open range
    /// [<paramref name="startLine"/>, <paramref name="endLine"/>). Building only the visible range (instead
    /// of the whole document and then discarding off-screen rows) keeps per-keystroke cost and allocation
    /// bounded by the viewport rather than the document size (see PERFORMANCE_PLAN.md Tier C).
    ///
    /// When <paramref name="startLine"/> &gt; 0, the two pieces of state a full top-down pass would have
    /// carried across the skipped lines are seeded explicitly:
    ///   - <c>currentHighlight</c>: a multi-line highlight span that began above the viewport and is still open.
    ///   - <c>selectionHighlight</c>: whether the text selection is already "open" at the top of the viewport.
    /// </summary>
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, SelectionSpan? selection, AnsiColor? selectedTextBackground, int startLine, int endLine)
    {
        Debug.Assert(startLine >= 0 && startLine <= endLine && endLine <= lines.Count);

        var selectionStart = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        var selectionEnd = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        if (selection.TryGet(out var selectionValue))
        {
            selectionStart = selectionValue.Start;
            selectionEnd = selectionValue.End;
        }

        // If the selection began above the viewport and hasn't ended yet, it's already "open" at startLine.
        bool selectionHighlight = selectionStart.Row < startLine && selectionEnd.Row >= startLine;

        var highlightsLookup = HighlightsGroupingPool.Shared.Get(highlights);
        var highlightedRows = new Row[endLine - startLine];
        FormatSpan? currentHighlight = SeedCurrentHighlight(highlights, lines, startLine);
        for (int lineIndex = startLine; lineIndex < endLine; lineIndex++)
        {
            WrappedLine line = lines[lineIndex];
            int lineFullWidthCharacterOffset = 0;
            var row = new Row(line.Content);
            for (int cellIndex = 0; cellIndex < row.Length; cellIndex++)
            {
                var cell = row[cellIndex];
                if (cell.IsContinuationOfPreviousCharacter)
                    lineFullWidthCharacterOffset++;

                // syntax highlight wrapped lines
                if (currentHighlight.TryGet(out var previousLineHighlight) &&
                    cellIndex == 0)
                {
                    currentHighlight = HighlightSpan(previousLineHighlight, row, cellIndex, previousLineHighlight.Start - line.StartIndex);
                }

                // get current syntaxt highlight start
                int characterPosition = line.StartIndex + cellIndex - lineFullWidthCharacterOffset;
                currentHighlight ??= highlightsLookup.TryGetValue(characterPosition, out var lookupHighlight) ? lookupHighlight : null;

                // syntax highlight based on start
                if (currentHighlight.TryGet(out var highlight) &&
                    highlight.Contains(characterPosition))
                {
                    currentHighlight = HighlightSpan(highlight, row, cellIndex, cellIndex);
                }

                // if there's text selected, invert colors to represent the highlight of the selected text.
                if (selectionStart.Equals(lineIndex, cellIndex - lineFullWidthCharacterOffset)) //start is inclusive
                {
                    selectionHighlight = true;
                }
                if (selectionEnd.Equals(lineIndex, cellIndex - lineFullWidthCharacterOffset)) //end is exclusive
                {
                    selectionHighlight = false;
                }
                if (selectionHighlight)
                {
                    if (selectedTextBackground.TryGet(out var background))
                    {
                        cell.TransformBackground(background);
                    }
                    else
                    {
                        cell.Formatting = new ConsoleFormat { Inverted = true };
                    }
                }
            }
            highlightedRows[lineIndex - startLine] = row;
        }

        // Return the lookup to the pool. The dict is local and its values (FormatSpan/ConsoleFormat) are value
        // types copied into the cells, so nothing outlives this call. Without this Put the pool stayed empty and
        // every render allocated a fresh dictionary sized to ALL highlight spans (large in highlight-heavy docs).
        HighlightsGroupingPool.Shared.Put(highlightsLookup);
        return highlightedRows;
    }

    /// <summary>
    /// When rendering starts partway down the document (<paramref name="startLine"/> &gt; 0), find the
    /// highlight span the top-down pass would have been carrying into <paramref name="startLine"/>: one that
    /// began strictly before this line's first character and still covers it. Returns null when starting at
    /// the top, or when no span straddles the viewport's top boundary.
    /// </summary>
    private static FormatSpan? SeedCurrentHighlight(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, int startLine)
    {
        if (startLine == 0)
        {
            return null;
        }

        int startCharIndex = lines[startLine].StartIndex;
        FormatSpan? seed = null;
        foreach (var span in highlights)
        {
            if (span.Start < startCharIndex && span.Contains(startCharIndex))
            {
                // Prefer the span that began closest to the boundary (and, on a tie, the longest) so we
                // match the single span the top-down carry would be holding. For the usual disjoint
                // (non-overlapping) syntax-highlight spans there is at most one candidate.
                if (seed is null
                    || span.Start > seed.Value.Start
                    || (span.Start == seed.Value.Start && span.Length > seed.Value.Length))
                {
                    seed = span;
                }
            }
        }
        return seed;
    }

    private static FormatSpan? HighlightSpan(FormatSpan currentHighlight, Row row, int cellIndex, int endPosition)
    {
        var highlightedFullWidthOffset = 0;
        int i;
        for (i = cellIndex; i < Math.Min(endPosition + currentHighlight.Length + highlightedFullWidthOffset, row.Length); i++)
        {
            highlightedFullWidthOffset += row[i].ElementWidth - 1;
            row[i].Formatting = currentHighlight.Formatting;
        }
        if (i != row.Length)
        {
            return null;
        }

        return currentHighlight;
    }

    /// <summary>
    /// This is just an extra function used by <see cref="Prompt.RenderAnsiOutput"/> that highlights arbitrary text. It's
    /// not used for drawing input during normal functioning of the prompt.
    /// </summary>
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, string text, int textWidth)
    {
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), 0, textWidth);
        return ApplyColorToCharacters(highlights, wrapped.WrappedLines, selection: null, selectedTextBackground: null);
    }

    private sealed class HighlightsGroupingPool
    {
        private readonly Stack<Dictionary<int, FormatSpan>> pool = new();

        public static readonly HighlightsGroupingPool Shared = new();

        public Dictionary<int, FormatSpan> Get(IReadOnlyCollection<FormatSpan> highlights)
        {
            Dictionary<int, FormatSpan>? result = null;
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    result = pool.Pop();
                }
            }
            if (result is null)
            {
                result = new Dictionary<int, FormatSpan>(highlights.Count);
            }
            else
            {
                result.EnsureCapacity(highlights.Count);
            }

            foreach (var highlight in highlights)
            {
                if (result.TryGetValue(highlight.Start, out var formatSpan))
                {
                    if (highlight.Length > formatSpan.Length)
                    {
                        result[highlight.Start] = highlight;
                    }
                }
                else
                {
                    result.Add(highlight.Start, highlight);
                }
            }

            return result;
        }

        public void Put(Dictionary<int, FormatSpan> list)
        {
            list.Clear();
            lock (pool)
            {
                pool.Push(list);
            }
        }
    }
}