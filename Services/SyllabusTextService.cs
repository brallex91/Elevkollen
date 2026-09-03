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

    /// <summary>Betygssteg som saknar eget innehåll ("mellan C och E") och inte går att välja.</summary>
    public static bool IsSelectableGradeStep(string? gradeStep) =>
        gradeStep is "E" or "C" or "A";

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

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<h4[^>]*>(?<h4>.*?)</h4>|<li[^>]*>(?<li>.*?)</li>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOrItemRegex();

    [GeneratedRegex(@"^\s*<h3[^>]*>.*?</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LeadingHeadingRegex();
}
