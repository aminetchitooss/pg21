using System;
using System.Collections.Generic;
using CreditReport.Models;
using CreditReport.Services;

namespace CreditReport
{
    /// <summary>
    /// Example usage of the CreditReportXmlGenerator
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create sample data
            var query = new CreditReportQuery
            {
                Version = "1.0",
                Server = new ReportQueryParams
                {
                    Comment = "Sample Credit Report",
                    RowBased = false,
                    StreamResponse = true,
                    KeepZeroes = false,
                    DisablePackageLogic = false,
                    DisableNighthawk = false,
                    MaxQueryDuration = 300,
                    Queries = new Dictionary<string, object>
                    {
                        { "MainQuery", new object[] { 
                            null, // placeholder
                            new[] { "Date=REPORT_DATE", "Portfolio=Portfolio1" },
                            new[] { "Value=SUM(Amount)" },
                            "Status='Active'"
                        }}
                    },
                    ColumnExpressions = new Dictionary<string, object>
                    {
                        { "TotalValue", "[Value] * 100" },
                        { "AdjustedValue", "[Value] - [Adjustment]" }
                    }
                },
                Client = new CreditPrestoQuery
                {
                    TextSubstitutions = new List<CreditTextSubstitution>
                    {
                        new CreditTextSubstitution
                        {
                            Token = "REPORT_DATE",
                            Description = "Report Date",
                            Multi = false,
                            SubstitutionElements = new List<CreditSubstitutionElement>
                            {
                                new CreditSubstitutionElement
                                {
                                    DisplayName = "Today",
                                    SubstitutionText = "TODAY()"
                                },
                                new CreditSubstitutionElement
                                {
                                    DisplayName = "Yesterday",
                                    SubstitutionText = "TODAY()-1"
                                }
                            }
                        }
                    },
                    ColumnExpressionKeys = new List<string> { "TotalValue", "AdjustedValue" }
                }
            };

            // Generate XML
            var generator = new CreditReportXmlGenerator();
            string xml = generator.GenerateFromJson(query);

            Console.WriteLine("Generated XML:");
            Console.WriteLine(xml);
        }
    }
}
