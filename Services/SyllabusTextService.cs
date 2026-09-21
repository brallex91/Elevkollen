using System.Net;
using System.Text.RegularExpressions;

namespace Elevkollen.Services;

/// <summary>
/// Cleans up Skolverket's HTML texts into plain, selectable text.
///
/// Raw data looks like this:
///   &lt;h3&gt;I årskurs 4-6&lt;/h3&gt;&lt;h4&gt;Algebra&lt;/h4&gt;&lt;ul&gt;&lt;li&gt;Punkt...&lt;/li&gt;&lt;/ul&gt;
/// and contains soft hyphens (\u00AD) that make the text unreadable in the UI.
///
/// Pure static class without DI — easy to unit test.
/// </summary>
public static partial class SyllabusTextService
{
    private const char SoftHyphen = '\u00AD';

    /// <summary>
    /// Selectable grade steps. D and B have no content of their own ("between C and E").
    /// An empty step occurs in years 1 and 3 ("godtagbara kunskaper") and is valid.
    /// </summary>
    public static bool IsSelectableGradeStep(string? gradeStep) =>
        string.IsNullOrWhiteSpace(gradeStep) || gradeStep is "E" or "C" or "A";

    /// <summary>Strips tags, soft hyphens and entities. Returns a single line of plain text.</summary>
    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = TagRegex().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = text.Replace(SoftHyphen.ToString(), "").Replace("\u200B", "");
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    /// Splits central content into items grouped under the nearest preceding h4 heading.
    /// Items without a heading end up under "Övrigt".
    /// </summary>
    public static IReadOnlyList<(string Heading, IReadOnlyList<string> Items)> SplitCentralContent(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var groups = new List<(string, IReadOnlyList<string>)>();
        var heading = "Övrigt";
        var items = new List<string>();

        foreach (Match m in HeadingOrItemRegex().Matches(html))
        {
            if (m.Groups["h4"].Success)
            {
                if (items.Count > 0)
                {
                    groups.Add((heading, items));
                    items = [];
                }

                heading = Clean(m.Groups["h4"].Value);
            }
            else
            {
                var item = Clean(m.Groups["li"].Value);
                if (item.Length > 0)
                {
                    items.Add(item);
                }
            }
        }

        if (items.Count > 0)
        {
            groups.Add((heading, items));
        }

        return groups;
    }

    /// <summary>
    /// Removes the leading h3 heading ("Betygskriterier för betyget E...") since subject
    /// and grade step are already shown separately in the UI.
    /// </summary>
    public static string CleanCriterion(string? html) =>
        Clean(LeadingHeadingRegex().Replace(html ?? "", ""));

    /// <summary>
    /// Splits a grading criterion into the individual criteria Skolverket delimits with
    /// &lt;p&gt;, and extracts the value words from &lt;strong&gt;.
    ///
    /// The value words are the only words separating the same criterion between grade
    /// steps, e.g. "på ett fungerande sätt" (E) versus "med god säkerhet" (A).
    ///
    /// With no &lt;p&gt; at all the whole text is treated as one criterion, so future
    /// syllabuses with different paragraphing still yield something usable.
    /// </summary>
    public static IReadOnlyList<(string Text, IReadOnlyList<string> ValueWords)> SplitCriteria(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var body = LeadingHeadingRegex().Replace(html, "");

        var parts = ParagraphRegex().Matches(body)
            .Select(m => m.Groups["p"].Value)
            .ToList();

        if (parts.Count == 0)
        {
            parts = [body];
        }

        var result = new List<(string, IReadOnlyList<string>)>();

        foreach (var part in parts)
        {
            var text = Clean(part);
            if (text.Length == 0)
            {
                continue;
            }

            result.Add((text, ExtractValueWords(part)));
        }

        return result;
    }

    /// <summary>
    /// Extracts value words from &lt;strong&gt;. Skolverket sometimes splits one value word
    /// across several tags ("&lt;strong&gt;väl&lt;/strong&gt; &lt;strong&gt;fungerande&lt;/strong&gt;),
    /// so adjacent fragments are merged into one phrase.
    /// </summary>
    private static IReadOnlyList<string> ExtractValueWords(string html)
    {
        var words = new List<string>();
        var end = -1;

        foreach (Match m in StrongRegex().Matches(html))
        {
            var word = Clean(m.Groups["s"].Value);
            if (word.Length == 0)
            {
                continue;
            }

            // Only whitespace between the previous tag and this one => same phrase.
            var joinable = end >= 0
                && words.Count > 0
                && string.IsNullOrWhiteSpace(html[end..m.Index]);

            if (joinable)
            {
                words[^1] = $"{words[^1]} {word}";
            }
            else
            {
                words.Add(word);
            }

            end = m.Index + m.Length;
        }

        return words;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<h4[^>]*>(?<h4>.*?)</h4>|<li[^>]*>(?<li>.*?)</li>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOrItemRegex();

    [GeneratedRegex(@"^\s*<h3[^>]*>.*?</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LeadingHeadingRegex();

    [GeneratedRegex("<p[^>]*>(?<p>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex("<strong[^>]*>(?<s>.*?)</strong>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StrongRegex();
}
