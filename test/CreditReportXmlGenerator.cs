using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CreditReport.Models;

namespace CreditReport.Services
{
    /// <summary>
    /// Service for generating XML from CreditReportQuery objects
    /// </summary>
    public class CreditReportXmlGenerator
    {
        private static readonly string[] ExceptionList = { "uniqueKey", "govCorp" };

        /// <summary>
        /// Generates XML string from a CreditReportQuery input
        /// </summary>
        /// <param name="input">The credit report query configuration</param>
        /// <returns>XML string representation</returns>
        public string GenerateFromJson(CreditReportQuery input)
        {
            var sb = new List<string>();

            sb.Add("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Add(
                Indent(1) +
                "<QueryGroupDefinition AllowServerSideAggregation=\"true\" UseEfficientQueries=\"false\" GridType=\"PivotGrid\" >"
            );

            var columnExpressions = input?.Server?.ColumnExpressions ?? new Dictionary<string, object>();
            GenerateColumnExpressions(sb, columnExpressions);

            var queries = input?.Server?.Queries as Dictionary<string, object> 
                ?? new Dictionary<string, object>();
            
            // Convert queries to the expected format
            var queriesArray = ConvertQueriesToArray(queries);
            GenerateLayout(sb, queriesArray, columnExpressions);
            GenerateQueryDefinitions(sb, queriesArray);

            if (input?.Client != null)
            {
                GenerateTextSubstitutions(sb, input.Client.TextSubstitutions);
            }

            sb.Add(Indent(1) + "</QueryGroupDefinition>");

            return string.Join(Environment.NewLine, sb);
        }

        /// <summary>
        /// Converts queries dictionary to array format for processing
        /// </summary>
        private Tuple<string, object[]>[] ConvertQueriesToArray(Dictionary<string, object> queries)
        {
            return queries.Select(kvp => Tuple.Create(kvp.Key, new object[] { kvp.Value })).ToArray();
        }

        /// <summary>
        /// Generates the Layout section of the XML
        /// </summary>
        private void GenerateLayout(
            List<string> sb, 
            Tuple<string, object[]>[] queries, 
            Dictionary<string, object> columnExpressions)
        {
            sb.Add(Indent(1) + "<Layout>");
            sb.Add(Indent(2) + "<Fields>");

            if (queries.Length > 0)
            {
                var query = queries[0];
                var keyFieldNames = GetKeyFieldNames(query.Item2)
                    .Where(k => !ExceptionList.Any(e => e == k) && !k.EndsWith("_SortField"))
                    .ToList();

                Console.WriteLine("query[1]: " + string.Join(", ", query.Item2));
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
        private void GenerateQueryDefinitions(List<string> sb, Tuple<string, object[]>[] queries)
        {
            sb.Add(Indent(1) + "<QueryDefinitions>");

            foreach (var queryEntry in queries)
            {
                var queryName = queryEntry.Item1;
                var part = ParseQueryParts(queryEntry.Item2);

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
                    if (kv.Key.StartsWith("uniqueKey="))
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
        /// Parses query parts from the raw query data
        /// </summary>
        private QueryParts ParseQueryParts(object[] queryData)
        {
            var result = new QueryParts();

            if (queryData == null || queryData.Length < 4)
                return result;

            // Part[1] = Keys, Part[2] = Values, Part[3] = Filter
            if (queryData.Length > 3 && queryData[3] != null)
            {
                result.FilterText = queryData[3].ToString().Trim();
            }

            // Parse keys from part[1]
            if (queryData.Length > 1 && queryData[1] is IEnumerable<string> keys)
            {
                foreach (var kv in keys)
                {
                    var parts = kv.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        result.Keys.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
                    }
                }
            }

            // Parse values from part[2]
            if (queryData.Length > 2 && queryData[2] is IEnumerable<string> values)
            {
                foreach (var kv in values)
                {
                    var parts = kv.Split(new[] { '=' }, 2);
                    if (parts.Length >= 1)
                    {
                        var alias = parts[0];
                        var expr = parts.Length > 1 ? alias + "=" + parts[1] : alias;
                        result.Values.Add(new KeyValuePair<string, string>(alias, expr));
                    }
                }
            }

            return result;
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
