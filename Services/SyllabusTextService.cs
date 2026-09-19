using System.Net;
using System.Text.RegularExpressions;

namespace Elevkollen.Services;

/// <summary>
/// Renskriver Skolverkets HTML-texter till ren, valbar text.
///
/// Rådata ser ut så här:
///   &lt;h3&gt;I årskurs 4-6&lt;/h3&gt;&lt;h4&gt;Algebra&lt;/h4&gt;&lt;ul&gt;&lt;li&gt;Punkt...&lt;/li&gt;&lt;/ul&gt;
/// och innehåller mjuka bindestreck (\u00AD) som gör texten oläslig i UI.
///
/// Ren statisk klass utan DI — enkel att enhetstesta.
/// </summary>
public static partial class SyllabusTextService
{
    private const char SoftHyphen = '\u00AD';

    /// <summary>
    /// Betygssteg som går att välja. D och B saknar eget innehåll ("mellan C och E").
    /// Tomt steg förekommer i årskurs 1 och 3 ("godtagbara kunskaper") och är giltigt.
    /// </summary>
    public static bool IsSelectableGradeStep(string? gradeStep) =>
        string.IsNullOrWhiteSpace(gradeStep) || gradeStep is "E" or "C" or "A";

    /// <summary>Tar bort taggar, mjuka bindestreck och entiteter. Returnerar en rad ren text.</summary>
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
    /// Delar upp centralt innehåll i punkter grupperade under närmast föregående h4-rubrik.
    /// Punkter utan rubrik hamnar under "Övrigt".
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
    /// Plockar bort den inledande h3-rubriken ("Betygskriterier för betyget E...")
    /// eftersom ämne och betygssteg redan visas separat i UI.
    /// </summary>
    public static string CleanCriterion(string? html) =>
        Clean(LeadingHeadingRegex().Replace(html ?? "", ""));

    /// <summary>
    /// Delar upp ett betygskriterium i de enskilda kriterier som Skolverket
    /// avgränsar med &lt;p&gt;, och plockar ut värdeorden ur &lt;strong&gt;.
    ///
    /// Värdeorden är de enda orden som skiljer samma kriterium mellan betygsstegen,
    /// t.ex. "på ett fungerande sätt" (E) mot "med god säkerhet" (A).
    ///
    /// Saknas &lt;p&gt; helt behandlas hela texten som ett enda kriterium, så att
    /// framtida läroplaner med annan styckeindelning fortfarande ger något användbart.
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
    /// Plockar ut värdeorden ur &lt;strong&gt;. Skolverket delar ibland ett värdeord
    /// över flera taggar ("&lt;strong&gt;väl&lt;/strong&gt; &lt;strong&gt;fungerande&lt;/strong&gt;),
    /// så intilliggande fragment slås ihop till ett uttryck.
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

            // Bara blanktecken mellan förra taggen och denna => samma uttryck.
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
