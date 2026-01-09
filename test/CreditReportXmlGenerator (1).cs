using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CreditReport.Models;

namespace CreditReport.Services
{
    /// <summary>
    /// Service for generating XML from CreditReportQuery objects
    /// </summary>
    public class CreditReportXmlGenerator
    {
        private static readonly string[] ExceptionList = { "uniqueKey", "govCorp" };

        private readonly JsonSerializerSettings _jsonSettings;

        public CreditReportXmlGenerator()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
        }

        /// <summary>
        /// Generates XML string from a JSON string input
        /// </summary>
        /// <param name="json">The JSON string containing credit report query configuration</param>
        /// <returns>XML string representation</returns>
        public string GenerateFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON input cannot be null or empty", nameof(json));
            }

            CreditReportQuery input = DeserializeJson(json);
            return GenerateXml(input);
        }

        /// <summary>
        /// Generates XML string from either a CreditReportQuery object or a JSON string
        /// </summary>
        /// <param name="input">The credit report query configuration (CreditReportQuery object or JSON string)</param>
        /// <returns>XML string representation</returns>
        public string GenerateFromObject(object input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            CreditReportQuery query;

            // Check if input is a string (JSON)
            if (input is string jsonString)
            {
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    throw new ArgumentException("JSON input cannot be null or empty", nameof(input));
                }
                query = DeserializeJson(jsonString);
            }
            // Check if input is already a CreditReportQuery
            else if (input is CreditReportQuery creditReportQuery)
            {
                query = creditReportQuery;
            }
            // Try to serialize and deserialize other object types
            else
            {
                try
                {
                    var json = JsonConvert.SerializeObject(input, _jsonSettings);
                    query = JsonConvert.DeserializeObject<CreditReportQuery>(json, _jsonSettings);
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Unable to convert input to CreditReportQuery: {ex.Message}", nameof(input), ex);
                }
            }

            if (query == null)
            {
                throw new ArgumentException("Input resolved to null", nameof(input));
            }

            return GenerateXml(query);
        }

        /// <summary>
        /// Deserializes a JSON string to CreditReportQuery
        /// </summary>
        private CreditReportQuery DeserializeJson(string json)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<CreditReportQuery>(json, _jsonSettings);
                if (result == null)
                {
                    throw new ArgumentException("JSON deserialized to null");
                }
                return result;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Internal method that generates XML from a validated CreditReportQuery object
        /// </summary>
        private string GenerateXml(CreditReportQuery input)
        {
            var sb = new List<string>();

            sb.Add("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Add(
                Indent(1) +
                "<QueryGroupDefinition AllowServerSideAggregation=\"true\" UseEfficientQueries=\"false\" GridType=\"PivotGrid\" >"
            );

            var columnExpressions = input?.Server?.ColumnExpressions ?? new Dictionary<string, object>();
            GenerateColumnExpressions(sb, columnExpressions);

            // Handle queries - could be Dictionary or JObject from JSON deserialization
            var queries = ParseQueries(input?.Server?.Queries);
            GenerateLayout(sb, queries, columnExpressions);
            GenerateQueryDefinitions(sb, queries);

            if (input?.Client != null)
            {
                GenerateTextSubstitutions(sb, input.Client.TextSubstitutions);
            }

            sb.Add(Indent(1) + "</QueryGroupDefinition>");

            return string.Join(Environment.NewLine, sb);
        }

        /// <summary>
        /// Parses queries from various input formats (Dictionary, JObject, etc.)
        /// </summary>
        private List<QueryEntry> ParseQueries(object queriesObj)
        {
            var result = new List<QueryEntry>();

            if (queriesObj == null)
                return result;

            // Handle JObject from JSON deserialization
            if (queriesObj is JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    var entry = new QueryEntry
                    {
                        Name = prop.Name,
                        Parts = ParseQueryPartsFromJToken(prop.Value)
                    };
                    result.Add(entry);
                }
            }
            // Handle Dictionary<string, object>
            else if (queriesObj is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    var entry = new QueryEntry
                    {
                        Name = kvp.Key,
                        Parts = ParseQueryPartsFromObject(kvp.Value)
                    };
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// Parses query parts from a JToken
        /// </summary>
        private QueryParts ParseQueryPartsFromJToken(JToken token)
        {
            var parts = new QueryParts();

            if (token is JArray arr && arr.Count >= 4)
            {
                // arr[0] = unused, arr[1] = keys, arr[2] = values, arr[3] = filter
                
                // Parse keys from arr[1]
                if (arr[1] is JArray keysArr)
                {
                    foreach (var item in keysArr)
                    {
                        var str = item.ToString();
                        var splitParts = str.Split(new[] { '=' }, 2);
                        if (splitParts.Length == 2)
                        {
                            parts.Keys.Add(new KeyValuePair<string, string>(splitParts[0], splitParts[1]));
                        }
                    }
                }

                // Parse values from arr[2]
                if (arr[2] is JArray valuesArr)
                {
                    foreach (var item in valuesArr)
                    {
                        var str = item.ToString();
                        var splitParts = str.Split(new[] { '=' }, 2);
                        if (splitParts.Length >= 1)
                        {
                            var alias = splitParts[0];
                            var expr = splitParts.Length > 1 ? alias + "=" + splitParts[1] : alias;
                            parts.Values.Add(new KeyValuePair<string, string>(alias, expr));
                        }
                    }
                }

                // Parse filter from arr[3]
                if (arr[3] != null && arr[3].Type != JTokenType.Null)
                {
                    parts.FilterText = arr[3].ToString().Trim();
                }
            }

            return parts;
        }

        /// <summary>
        /// Parses query parts from a generic object
        /// </summary>
        private QueryParts ParseQueryPartsFromObject(object obj)
        {
            var parts = new QueryParts();

            if (obj is object[] arr && arr.Length >= 4)
            {
                // Parse keys from arr[1]
                if (arr[1] is IEnumerable<string> keys)
                {
                    foreach (var str in keys)
                    {
                        var splitParts = str.Split(new[] { '=' }, 2);
                        if (splitParts.Length == 2)
                        {
                            parts.Keys.Add(new KeyValuePair<string, string>(splitParts[0], splitParts[1]));
                        }
                    }
                }

                // Parse values from arr[2]
                if (arr[2] is IEnumerable<string> values)
                {
                    foreach (var str in values)
                    {
                        var splitParts = str.Split(new[] { '=' }, 2);
                        if (splitParts.Length >= 1)
                        {
                            var alias = splitParts[0];
                            var expr = splitParts.Length > 1 ? alias + "=" + splitParts[1] : alias;
                            parts.Values.Add(new KeyValuePair<string, string>(alias, expr));
                        }
                    }
                }

                // Parse filter from arr[3]
                if (arr[3] != null)
                {
                    parts.FilterText = arr[3].ToString().Trim();
                }
            }

            return parts;
        }

        /// <summary>
        /// Generates the Layout section of the XML
        /// </summary>
        private void GenerateLayout(
            List<string> sb, 
            List<QueryEntry> queries, 
            Dictionary<string, object> columnExpressions)
        {
            sb.Add(Indent(1) + "<Layout>");
            sb.Add(Indent(2) + "<Fields>");

            if (queries.Count > 0)
            {
                var query = queries[0];
                var keyFieldNames = query.Parts.Keys
                    .Select(k => k.Key)
                    .Where(k => !ExceptionList.Any(e => e == k) && !k.EndsWith("_SortField"))
                    .ToList();

                Console.WriteLine("query keys: " + string.Join(", ", query.Parts.Keys.Select(k => k.Key)));
                Console.WriteLine("keyFieldNames: " + string.Join(", ", keyFieldNames));

                foreach (var fn in keyFieldNames)
                {
                    sb.Add(Indent(3) + $"<Field Name=\"{fn}\" />");
                }
            }

            foreach (var name in columnExpressions.Keys)
            {
                sb.Add(Indent(3) + $"<Field Name=\"{name}\" PivotArea=\"Data\" />");
            }

            sb.Add(Indent(2) + "</Fields>");
            sb.Add(Indent(1) + "</Layout>");
        }

        /// <summary>
        /// Extracts key field names from query parameters
        /// </summary>
        private List<string> GetKeyFieldNames(object[] queryParams)
        {
            var result = new List<string>();
            
            if (queryParams == null || queryParams.Length == 0)
                return result;

            // Handle string array or parse from query format
            foreach (var param in queryParams)
            {
                if (param is string str)
                {
                    // Parse "key=value" format and extract keys
                    var parts = str.Split('=');
                    if (parts.Length > 0)
                    {
                        result.Add(parts[0]);
                    }
                }
                else if (param is IEnumerable<string> strArray)
                {
                    foreach (var item in strArray)
                    {
                        var parts = item.Split('=');
                        if (parts.Length > 0)
                        {
                            result.Add(parts[0]);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Generates the ColumnExpressions section of the XML
        /// </summary>
        private void GenerateColumnExpressions(List<string> sb, Dictionary<string, object> columnExpressions)
        {
            sb.Add(Indent(1) + "<ColumnExpressions>");

            foreach (var entry in columnExpressions)
            {
                var name = entry.Key;
                var expr = entry.Value?.ToString() ?? string.Empty;

                sb.Add(
                    Indent(2) +
                    $"<Expression Name=\"{name}\" DataType=\"System.Double\" CopyErrors=\"true\" Expression=\"{expr}\" IsPostTranspose=\"false\" />"
                );
            }

            sb.Add(Indent(1) + "</ColumnExpressions>");
        }

        /// <summary>
        /// Generates the TextSubstitutions section of the XML
        /// </summary>
        private void GenerateTextSubstitutions(List<string> sb, List<CreditTextSubstitution> textSubstitutions)
        {
            if (textSubstitutions == null || textSubstitutions.Count == 0)
                return;

            sb.Add(Indent(1) + "<TextSubstitutions>");

            foreach (var elem in textSubstitutions)
            {
                sb.Add(
                    Indent(2) +
                    $"<TextSubstitution Description=\"{elem.Description}\" Token=\"{elem.Token}\" Width=\"0\" Advanced=\"false\">"
                );

                if (elem.SubstitutionElements != null && elem.SubstitutionElements.Count > 0)
                {
                    sb.Add(Indent(3) + "<SubstitutionElements>");

                    foreach (var sub in elem.SubstitutionElements)
                    {
                        sb.Add(
                            Indent(4) +
                            $"<SubstitutionElement DisplayName=\"{sub.DisplayName}\" SubstitutionText=\"{sub.SubstitutionText}\" />"
                        );
                    }

                    sb.Add(Indent(3) + "</SubstitutionElements>");
                }

                sb.Add(Indent(2) + "</TextSubstitution>");
            }

            sb.Add(Indent(1) + "</TextSubstitutions>");
        }

        /// <summary>
        /// Generates the QueryDefinitions section of the XML
        /// </summary>
        private void GenerateQueryDefinitions(List<string> sb, List<QueryEntry> queries)
        {
            sb.Add(Indent(1) + "<QueryDefinitions>");

            foreach (var queryEntry in queries)
            {
                var queryName = queryEntry.Name;
                var part = queryEntry.Parts;

                sb.Add(
                    Indent(2) +
                    $"<QueryGroupPartDefinition Name=\"{queryName}\" IncludeSystemPortfolios=\"false\" HierarchyOverride=\"false\" FilterOverride=\"false\">"
                );

                // Filters section
                if (!string.IsNullOrEmpty(part.FilterText))
                {
                    sb.Add(Indent(3) + "<Filters>");
                    sb.Add(Indent(4) + $"<string>{part.FilterText}</string>");
                    sb.Add(Indent(3) + "</Filters>");
                }

                // Keys section
                sb.Add(Indent(3) + "<Keys>");
                foreach (var kv in part.Keys)
                {
                    if (kv.Key.StartsWith("uniqueKey"))
                        continue;

                    var alias = kv.Key;
                    var fixedExpr = FixDateExpression(kv.Value);
                    sb.Add(Indent(4) + $"<AliasedKey Alias=\"{alias}\" Key=\"{fixedExpr}\" AlwaysQuery=\"false\" />");
                }
                sb.Add(Indent(3) + "</Keys>");

                // Values section
                sb.Add(Indent(3) + "<Values>");
                foreach (var kv in part.Values)
                {
                    var alias = kv.Key;
                    var expr = kv.Value;
                    sb.Add(Indent(4) + $"<AliasedValue Alias=\"{alias}\" Value=\"{expr}\" AllowEnvironmentComparison=\"false\" />");
                }
                sb.Add(Indent(3) + "</Values>");

                sb.Add(Indent(2) + "</QueryGroupPartDefinition>");
            }

            sb.Add(Indent(1) + "</QueryDefinitions>");
        }

        /// <summary>
        /// Fixes date expressions by replacing DateToTenor patterns
        /// </summary>
        private string FixDateExpression(string expr)
        {
            if (string.IsNullOrEmpty(expr))
                return expr;

            return Regex.Replace(
                expr,
                @"DateToTenor\(REPORT_DATE",
                "DateToTenor(date(REPORT_DATE)"
            );
        }

        /// <summary>
        /// Returns indentation string for the specified level
        /// </summary>
        private string Indent(int level)
        {
            return new string(' ', level * 2);
        }

        /// <summary>
        /// Helper class to hold a query entry with name and parts
        /// </summary>
        private class QueryEntry
        {
            public string Name { get; set; }
            public QueryParts Parts { get; set; }

            public QueryEntry()
            {
                Parts = new QueryParts();
            }
        }

        /// <summary>
        /// Helper class to hold parsed query parts
        /// </summary>
        private class QueryParts
        {
            public string FilterText { get; set; }
            public List<KeyValuePair<string, string>> Keys { get; set; }
            public List<KeyValuePair<string, string>> Values { get; set; }

            public QueryParts()
            {
                FilterText = string.Empty;
                Keys = new List<KeyValuePair<string, string>>();
                Values = new List<KeyValuePair<string, string>>();
            }
        }
    }
}
