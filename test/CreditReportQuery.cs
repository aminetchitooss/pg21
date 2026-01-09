using System;
using System.Collections.Generic;

namespace CreditReport.Models
{
    /// <summary>
    /// Represents a Presto query configuration for credit reports
    /// </summary>
    public class CreditPrestoQuery
    {
        public List<CreditTextSubstitution> TextSubstitutions { get; set; }
        public List<string> ColumnExpressionKeys { get; set; }

        public CreditPrestoQuery()
        {
            TextSubstitutions = new List<CreditTextSubstitution>();
            ColumnExpressionKeys = new List<string>();
        }
    }

    /// <summary>
    /// Represents the complete credit report query configuration
    /// </summary>
    public class CreditReportQuery
    {
        public string Version { get; set; }
        public ReportQueryParams Server { get; set; }
        public CreditPrestoQuery Client { get; set; }
    }

    /// <summary>
    /// Report query parameters - currently supporting report_v4
    /// </summary>
    public class ReportQueryParams
    {
        public string PostReportFilter { get; set; }
        public string Comment { get; set; }
        public Dictionary<string, object> Queries { get; set; }
        public bool RowBased { get; set; }
        public bool StreamResponse { get; set; }
        public bool KeepZeroes { get; set; }
        public bool DisablePackageLogic { get; set; }
        public bool DisableNighthawk { get; set; }
        public int MaxQueryDuration { get; set; }
        public bool? EnableLatestVersion { get; set; }
        public List<string> CurveMappings { get; set; }
        public string CurveMappingsDate { get; set; }
        public string CurveMappingsBehaviour { get; set; }
        public string LogLevel { get; set; }
        public Dictionary<string, object> ColumnExpressions { get; set; }
        public double? UpdateTolerance { get; set; }
        public bool? CacheQueryUpdates { get; set; }

        public ReportQueryParams()
        {
            Queries = new Dictionary<string, object>();
            CurveMappings = new List<string>();
            ColumnExpressions = new Dictionary<string, object>();
        }
    }
}
