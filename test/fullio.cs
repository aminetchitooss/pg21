using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CreditQueryXml
{
    // ---------------------------
    // Models (adapt to your JSON)
    // ---------------------------

    public sealed class CreditReportQuery
    {
        public string Version { get; set; }
        public ServerSection Server { get; set; }
        public ClientSection Client { get; set; }
    }

    public sealed class ServerSection
    {
        // column_expressions: { "ColA": "expr", ... }
        public Dictionary<string, string> ColumnExpressions { get; set; }

        // queries: { "QueryName": part, ... }
        public Dictionary<string, QueryPart> Queries { get; set; }
    }

    public sealed class ClientSection
    {
        public List<CreditTextSubstitution> TextSubstitutions { get; set; }
    }

    public sealed class QueryPart
    {
        // TS part[1]
        // Example items in screenshots: "alias=SomeExpr", also "uniqueKey=..." should be skipped
        public List<string> Keys { get; set; }

        // TS part[2]
        // Example items: "alias=SomeValueExpr"
        public List<string> Values { get; set; }

        // TS part[3]
        public string FilterText { get; set; }

        // Optional: if you have other stuff (TS part[4] object), keep it here
        public Dictionary<string, object> Meta { get; set; }
    }

    public sealed class CreditTextSubstitution
    {
        public string Token { get; set; }
        public string Description { get; set; }
        public bool Multi { get; set; }
        public List<CreditSubstitutionElement> SubstitutionElements { get; set; }
    }

    public sealed class CreditSubstitutionElement
    {
        public string DisplayName { get; set; }
        public string SubstitutionText { get; set; }
    }

    // ---------------------------
    // Generator
    // ---------------------------

    public sealed class CreditQueryXmlGenerator
    {
        public string GenerateFromJson(CreditReportQuery input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var sb = new StringBuilder();

            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            sb.AppendLine(
                @"<QueryGroupDefinition AllowServerSideAggregation=""true"" UseEfficientQueries=""false"" GridType=""PivotGrid"">"
            );

            var columnExpressions = input.Server != null && input.Server.ColumnExpressions != null
                ? input.Server.ColumnExpressions
                : new Dictionary<string, string>();

            GenerateColumnExpressions(sb, columnExpressions);

            var queries = input.Server != null && input.Server.Queries != null
                ? input.Server.Queries
                : new Dictionary<string, QueryPart>();

            GenerateLayout(sb, queries, columnExpressions);
            GenerateQueryDefinitions(sb, queries);

            if (input.Client != null && input.Client.TextSubstitutions != null && input.Client.TextSubstitutions.Count > 0)
            {
                GenerateTextSubstitutions(sb, input.Client.TextSubstitutions);
            }

            sb.AppendLine(@"</QueryGroupDefinition>");

            return sb.ToString();
        }

        private void GenerateColumnExpressions(StringBuilder sb, Dictionary<string, string> columnExpressions)
        {
            sb.AppendLine(Indent(1) + "<ColumnExpressions>");

            foreach (var kvp in columnExpressions)
            {
                var name = XmlEscape(kvp.Key);
                var expr = XmlEscape(kvp.Value ?? "");

                // Matches your screenshot:
                // <Expression Name="X" DataType="System.Double" CopyErrors="true" Expression="..." IsPostTranspose="false" />
                sb.AppendLine(
                    Indent(2) +
                    $@"<Expression Name=""{name}"" DataType=""System.Double"" CopyErrors=""true"" Expression=""{expr}"" IsPostTranspose=""false"" />"
                );
            }

            sb.AppendLine(Indent(1) + "</ColumnExpressions>");
        }

        private void GenerateLayout(StringBuilder sb, Dictionary<string, QueryPart> queries, Dictionary<string, string> columnExpressions)
        {
            sb.AppendLine(Indent(1) + "<Layout>");
            sb.AppendLine(Indent(2) + "<Fields>");

            // TS does:
            // const [, query] = Object.entries(queries)[0];
            // const keyFieldNames = query[1].map(...).filter(...)
            // exceptionList: ['uniqueKey', 'govCorp'] and exclude keys ending with "-SortField"
            if (queries.Count > 0)
            {
                var first = queries.First();
                var part = first.Value;

                var exceptionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "uniqueKey",
                    "govCorp"
                };

                var keyFieldNames = (part?.Keys ?? new List<string>())
                    .Select(s => (s ?? "").Split(new[] { '=' }, 2)[0]) // alias part before '='
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Where(k => !exceptionList.Contains(k))
                    .Where(k => !k.EndsWith("-SortField", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var fn in keyFieldNames)
                {
                    sb.AppendLine(Indent(3) + $@"<Field Name=""{XmlEscape(fn)}"" />");
                }
            }

            // TS does: for (const name of Object.keys(column_expressions)) <Field Name="name" PivotArea="Data" />
            foreach (var name in columnExpressions.Keys)
            {
                sb.AppendLine(Indent(3) + $@"<Field Name=""{XmlEscape(name)}"" PivotArea=""Data"" />");
            }

            sb.AppendLine(Indent(2) + "</Fields>");
            sb.AppendLine(Indent(1) + "</Layout>");
        }

        private void GenerateQueryDefinitions(StringBuilder sb, Dictionary<string, QueryPart> queries)
        {
            sb.AppendLine(Indent(1) + "<QueryDefinitions>");

            foreach (var entry in queries)
            {
                var queryName = entry.Key ?? "";
                var part = entry.Value ?? new QueryPart();

                sb.AppendLine(
                    Indent(2) +
                    $@"<QueryGroupPartDefinition Name=""{XmlEscape(queryName)}"" IncludeSystemPortfolios=""false"" HierarchyOverride=""false"" FilterOverride=""false"">"
                );

                // -------- Filters --------
                var filterText = (part.FilterText ?? "").Trim();
                sb.AppendLine(Indent(3) + "<Filters>");
                sb.AppendLine(Indent(4) + $@"<string>{XmlEscapeElement(filterText)}</string>");
                sb.AppendLine(Indent(3) + "</Filters>");

                // -------- Keys --------
                sb.AppendLine(Indent(3) + "<Keys>");

                foreach (var kv in part.Keys ?? Enumerable.Empty<string>())
                {
                    if (kv == null) continue;
                    if (kv.StartsWith("uniqueKey=", StringComparison.OrdinalIgnoreCase)) continue;

                    var split = kv.Split(new[] { '=' }, 2);
                    var alias = split.Length > 0 ? split[0] : "";
                    var expr = split.Length > 1 ? split[1] : "";

                    // TS fix:
                    // expr.replace(/DateToTenor\(REPORT_DATE/g, 'DateToTenor(date(REPORT_DATE)')
                    var fixedExpr = (expr ?? "").Replace("DateToTenor(REPORT_DATE", "DateToTenor(date(REPORT_DATE)");

                    sb.AppendLine(
                        Indent(4) +
                        $@"<AliasedKey Alias=""{XmlEscape(alias)}"" Key=""{XmlEscape(fixedExpr)}"" AlwaysQuery=""false"" />"
                    );
                }

                sb.AppendLine(Indent(3) + "</Keys>");

                // -------- Values --------
                sb.AppendLine(Indent(3) + "<Values>");

                foreach (var kv in part.Values ?? Enumerable.Empty<string>())
                {
                    if (kv == null) continue;

                    // TS:
                    // const [alias] = kv.split("=");
                    // const [, expr] = kv.split(alias + "=");
                    // (basically: alias is left of first '='; expr is everything after 'alias=')
                    var split = kv.Split(new[] { '=' }, 2);
                    var alias = split.Length > 0 ? split[0] : "";
                    var expr = split.Length > 1 ? split[1] : "";

                    sb.AppendLine(
                        Indent(4) +
                        $@"<AliasedValue Alias=""{XmlEscape(alias)}"" Value=""{XmlEscape(expr)}"" AllowEnvironmentComparison=""false"" />"
                    );
                }

                sb.AppendLine(Indent(3) + "</Values>");

                sb.AppendLine(Indent(2) + "</QueryGroupPartDefinition>");
            }

            sb.AppendLine(Indent(1) + "</QueryDefinitions>");
        }

        private void GenerateTextSubstitutions(StringBuilder sb, List<CreditTextSubstitution> textSubstitutions)
        {
            sb.AppendLine(Indent(1) + "<TextSubstitutions>");

            foreach (var elem in textSubstitutions ?? Enumerable.Empty<CreditTextSubstitution>())
            {
                if (elem == null) continue;

                sb.AppendLine(
                    Indent(2) +
                    $@"<TextSubstitution Description=""{XmlEscape(elem.Description ?? "")}"" Token=""{XmlEscape(elem.Token ?? "")}"" Width=""0"" Advanced=""false"">"
                );

                if (elem.SubstitutionElements != null && elem.SubstitutionElements.Count > 0)
                {
                    sb.AppendLine(Indent(3) + "<SubstitutionElements>");

                    foreach (var sub in elem.SubstitutionElements)
                    {
                        if (sub == null) continue;

                        sb.AppendLine(
                            Indent(4) +
                            $@"<SubstitutionElement DisplayName=""{XmlEscape(sub.DisplayName ?? "")}"" SubstitutionText=""{XmlEscape(sub.SubstitutionText ?? "")}"" />"
                        );
                    }

                    sb.AppendLine(Indent(3) + "</SubstitutionElements>");
                }

                sb.AppendLine(Indent(2) + "</TextSubstitution>");
            }

            sb.AppendLine(Indent(1) + "</TextSubstitutions>");
        }

        private static string Indent(int level) => new string(' ', level * 2);

        // Escapes text used inside XML attributes
        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("'", "&apos;");
        }

        // Escapes text used inside XML element content (we can keep it same; this is just clearer)
        private static string XmlEscapeElement(string s) => XmlEscape(s);
    }
}
