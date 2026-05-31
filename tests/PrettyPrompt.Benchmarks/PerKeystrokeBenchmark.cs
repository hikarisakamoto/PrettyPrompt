#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using PrettyPrompt;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

/// <summary>
/// Isolates the per-keystroke work that scales with document size. The existing <see cref="PromptBenchmark"/>
/// only drives short input through full submits, so it can't see the O(n) re-wrap / cell-build that happens
/// on every keypress. These benchmarks hold a document of <see cref="DocumentLineCount"/> lines and measure
/// the three hot stages individually:
///
///   WordWrap          - the full re-wrap a caret-only move (arrow key) pays today.
///   WrapAndBuildCells - wrap + Row/Cell generation for the whole document.
///   FullRedraw        - the whole output pipeline (wrap -> cells -> screen -> ANSI diff).
///
/// Run with: dotnet run -c Release --project tests/PrettyPrompt.Benchmarks -- --filter *PerKeystroke*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class PerKeystrokeBenchmark
{
    // a typical-to-wide terminal; lines are generated ~100 columns wide so each wraps once at this width.
    private const int Width = 80;

    [Params(10, 100, 1000)]
    public int DocumentLineCount;

    private string text = "";
    private StringBuilder stringBuilder = null!;
    private int caret;
    private IReadOnlyCollection<FormatSpan> highlights = Array.Empty<FormatSpan>();

    [GlobalSetup]
    public void Setup()
    {
        text = GenerateDocument(DocumentLineCount);
        stringBuilder = new StringBuilder(text);
        caret = text.Length / 2; // simulate editing / navigating in the middle of the document
        highlights = GenerateHighlights(text);
    }

    /// <summary>
    /// The cost a caret-only move pays today: <see cref="Document.Changed"/> fires for cursor moves too,
    /// so every arrow key re-wraps the entire document even though only the 2-D cursor coordinate changed.
    /// This is the work the caret-only fast path (and incremental wrap) aims to eliminate.
    /// </summary>
    [Benchmark]
    public int WordWrap()
    {
        var wrapped = WordWrapping.WrapEditableCharacters(stringBuilder, caret, Width);
        return wrapped.WrappedLines.Count; // consume the result so it isn't optimized away
    }

    /// <summary>
    /// Wrap plus building a Row (and a Cell per character) for every wrapped line. Cells are disposed back
    /// to the shared pool so this measures warm steady-state allocation, matching production where
    /// Screen.Dispose recycles them each keystroke. Target of viewport-bounded cell generation.
    /// </summary>
    [Benchmark]
    public int WrapAndBuildCells()
    {
        var rows = CellRenderer.ApplyColorToCharacters(highlights, text, Width);
        int count = rows.Length;
        foreach (var row in rows) row.Dispose();
        return count;
    }

    /// <summary>
    /// The whole output pipeline: wrap + cells + screen buffer + ANSI diff. This diffs against a blank screen
    /// (a full redraw, e.g. after Ctrl+L or a resize), so its allocation numbers are the cold upper bound -
    /// steady-state per-keystroke is cheaper thanks to cell pooling and the incremental diff against the
    /// previous near-identical screen (captured end-to-end by PromptBenchmark).
    /// </summary>
    [Benchmark]
    public int FullRedraw()
        => Prompt.RenderAnsiOutput(text, highlights, Width).Length;

    private static readonly string[] Words =
    {
        "apple", "the", "banana", "quick", "mango", "brown", "grape", "fox",
        "melon", "jumps", "orange", "over", "pear", "lazy", "peach", "dog",
    };

    // words we mark as highlighted, to give the cell renderer realistic syntax-highlight work.
    private static readonly string[] HighlightedWords =
    {
        "apple", "banana", "mango", "grape", "melon", "orange", "pear", "peach",
    };

    private static string GenerateDocument(int lineCount)
    {
        var sb = new StringBuilder();
        int w = 0;
        for (int line = 0; line < lineCount; line++)
        {
            int col = 0;
            // build a ~100-column line so it wraps once at Width (80)
            while (col < 100)
            {
                if (col > 0) { sb.Append(' '); col++; }
                var word = Words[w++ % Words.Length];
                sb.Append(word);
                col += word.Length;
            }
            if (line < lineCount - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static IReadOnlyCollection<FormatSpan> GenerateHighlights(string text)
    {
        var spans = new List<FormatSpan>();
        foreach (var word in HighlightedWords)
        {
            int idx = 0;
            while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                spans.Add(new FormatSpan(idx, word.Length, AnsiColor.Green));
                idx += word.Length;
            }
        }
        return spans;
    }
}
