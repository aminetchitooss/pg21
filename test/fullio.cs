// CreditQueryXmlGenerator.cs
// .NET Framework 4.8
// NuGet: Install-Package Newtonsoft.Json

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CreditQueryXml
{
    // ---------------------------
    // Root models (JSON -> C#)
    // ---------------------------

    public sealed class CreditReportQuery
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("server")]
        public ServerSection Server { get; set; }

        [JsonProperty("client")]
        public ClientSection Client { get; set; }
    }

    public sealed class ServerSection
    {
        // JSON: "column_expressions": { "ColA": "expr", ... }
        [JsonProperty("column_expressions")]
        public Dictionary<string, string> ColumnExpressions { get; set; }

        // JSON: "queries": { "QueryName": ["", string[], string[], "filter", {}], ... }
        [JsonProperty("queries")]
        public Dictionary<string, QueryPart> Queries { get; set; }
    }

    public sealed class ClientSection
    {
        // JSON: "text_substitutions": [...]
        [JsonProperty("text_substitutions")]
        public List<CreditTextSubstitution> TextSubstitutions { get; set; }
    }

    public sealed class CreditTextSubstitution
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("multi")]
        public bool Multi { get; set; }

        [JsonProperty("substitutionElements")]
        public List<CreditSubstitutionElement> SubstitutionElements { get; set; }
    }

    public sealed class CreditSubstitutionElement
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("substitutionText")]
        public string SubstitutionText { get; set; }
    }

    // --------------------------------------------
    // QueryPart supports BOTH shapes:
    // 1) Tuple/array: ["", string[], string[], "filter", object]
    // 2) Object: { keys:[], values:[], filterText:"", meta:{} }
    // --------------------------------------------

    [JsonConverter(typeof(QueryPartJsonConverter))]
    public sealed class QueryPart
    {
        public List<string> Keys { get; set; } = new List<string>();
        public List<string> Values { get; set; } = new List<string>();
        public string FilterText { get; set; } = "";
        public JObject Meta { get; set; }
    }

    public sealed class QueryPartJsonConverter : JsonConverter<QueryPart>
    {
        public override QueryPart ReadJson(JsonReader reader, Type objectType, QueryPart existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var token = JToken.Load(reader);

            // Tuple/array shape: ["", string[], string[], "filter", object]
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;

                return new QueryPart
                {
                    Keys = arr.Count > 1 ? (arr[1]?.ToObject<List<string>>(serializer) ?? new List<string>()) : new List<string>(),
                    Values = arr.Count > 2 ? (arr[2]?.ToObject<List<string>>(serializer) ?? new List<string>()) : new List<string>(),
                    FilterText = arr.Count > 3 ? (arr[3]?.ToString() ?? "") : "",
                    Meta = arr.Count > 4 ? (arr[4] as JObject) : null
                };
            }

            // Object shape
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;

                // accept multiple possible property names (in case your JSON differs)
                var keys = obj["keys"] ?? obj["Keys"];
                var values = obj["values"] ?? obj["Values"];
                var filter = obj["filterText"] ?? obj["filter"] ?? obj["FilterText"];

                return new QueryPart
                {
                    Keys = keys?.ToObject<List<string>>(serializer) ?? new List<string>(),
                    Values = values?.ToObject<List<string>>(serializer) ?? new List<string>(),
                    FilterText = filter?.ToString() ?? "",
                    Meta = (obj["meta"] ?? obj["Meta"]) as JObject
                };
            }

            throw new JsonSerializationException($"Unexpected token for QueryPart: {token.Type}");
        }

        public override void WriteJson(JsonWriter writer, QueryPart value, JsonSerializer serializer)
        {
            // If you don't need to serialize back, keep it simple.
            writer.WriteStartObject();
            writer.WritePropertyName("keys");
            serializer.Serialize(writer, value?.Keys ?? new List<string>());
            writer.WritePropertyName("values");
            serializer.Serialize(writer, value?.Values ?? new List<string>());
            writer.WritePropertyName("filterText");
            writer.WriteValue(value?.FilterText ?? "");
            writer.WritePropertyName("meta");
            serializer.Serialize(writer, value?.Meta);
            writer.WriteEndObject();
        }
    }

    // ---------------------------
    // Generator
    // ---------------------------

    public sealed class CreditQueryXmlGenerator
    {
        /// <summary>
        /// Takes JSON string, deserializes into CreditReportQuery, then generates XML (string).
        /// </summary>
        public string GenerateFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON input is empty.", nameof(json));

            var input = JsonConvert.DeserializeObject<CreditReportQuery>(json);
            if (input == null)
                throw new InvalidOperationException("Failed to deserialize JSON into CreditReportQuery.");

            return GenerateFromObject(input);
        }

        /// <summary>
        /// Takes an already-built object and generates the XML.
        /// </summary>
        public string GenerateFromObject(CreditReportQuery input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var sb = new StringBuilder();

            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            sb.AppendLine(
                @"<QueryGroupDefinition AllowServerSideAggregation=""true"" UseEfficientQueries=""false"" GridType=""PivotGrid"">"
            );

            var columnExpressions = input.Server?.ColumnExpressions ?? new Dictionary<string, string>();
            var queries = input.Server?.Queries ?? new Dictionary<string, QueryPart>();

            GenerateColumnExpressions(sb, columnExpressions);
            GenerateLayout(sb, queries, columnExpressions);
            GenerateQueryDefinitions(sb, queries);

            if (input.Client?.TextSubstitutions?.Any() == true)
                GenerateTextSubstitutions(sb, input.Client.TextSubstitutions);

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

                // From your screenshot:
                // <Expression Name="..." DataType="System.Double" CopyErrors="true" Expression="..." IsPostTranspose="false" />
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

            // Matches your TS:
            // const [, query] = Object.entries(queries)[0];
            // exceptionList: ['uniqueKey', 'govCorp'] and exclude '-SortField'
            if (queries.Count > 0)
            {
                var first = queries.First();
                var part = first.Value ?? new QueryPart();

                var exceptionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "uniqueKey",
                    "govCorp"
                };

                var keyFieldNames = (part.Keys ?? new List<string>())
                    .Select(s => (s ?? "").Split(new[] { '=' }, 2)[0])
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Where(k => !exceptionList.Contains(k))
                    .Where(k => !k.EndsWith("-SortField", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var fn in keyFieldNames)
                    sb.AppendLine(Indent(3) + $@"<Field Name=""{XmlEscape(fn)}"" />");
            }

            // Add all column expressions as PivotArea="Data"
            foreach (var name in columnExpressions.Keys)
                sb.AppendLine(Indent(3) + $@"<Field Name=""{XmlEscape(name)}"" PivotArea=""Data"" />");

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

                    // Your TS replacement:
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

        // ---------------------------
        // Helpers
        // ---------------------------

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

        // Escapes text used inside XML element content
        private static string XmlEscapeElement(string s) => XmlEscape(s);
    }
}
