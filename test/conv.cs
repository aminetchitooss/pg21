using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class ReportParamsExtractor
{
    private static readonly HashSet<string> ExcludedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "comment",
        "queries",
        "column_expressions"
    };

    public static Dictionary<string, object> Extract(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string is null/empty.", nameof(json));

        var root = JObject.Parse(json);

        // params is an array: take the first object inside it
        var paramsToken = root["params"];
        if (paramsToken == null || paramsToken.Type != JTokenType.Array)
            throw new InvalidOperationException("Expected root.params to be an array.");

        var firstParamObj = paramsToken.First as JObject;
        if (firstParamObj == null)
            throw new InvalidOperationException("Expected params[0] to be an object.");

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in firstParamObj.Properties())
        {
            if (ExcludedKeys.Contains(prop.Name))
                continue;

            result[prop.Name] = ToClrValue(prop.Value);
        }

        return result;
    }

    private static object ToClrValue(JToken token)
    {
        if (token == null) return null;

        switch (token.Type)
        {
            case JTokenType.Null:
            case JTokenType.Undefined:
                return null;

            case JTokenType.Boolean:
                return token.Value<bool>();

            case JTokenType.Integer:
                // Json.NET uses Int64 for integers by default
                long l = token.Value<long>();
                // Optionally downcast to int when it fits
                if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                return l;

            case JTokenType.Float:
                return token.Value<double>();

            case JTokenType.String:
                return token.Value<string>();

            case JTokenType.Date:
                return token.Value<DateTime>();

            case JTokenType.Object:
                // Convert nested objects to Dictionary<string, object>
                var obj = (JObject)token;
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in obj.Properties())
                    dict[p.Name] = ToClrValue(p.Value);
                return dict;

            case JTokenType.Array:
                // Convert arrays to List<object>
                var list = new List<object>();
                foreach (var item in (JArray)token)
                    list.Add(ToClrValue(item));
                return list;

            default:
                // Fallback
                return ((JValue)token).Value;
        }
    }

    private static string santi()
    {
       

string input =
"DateToTenor(REPORT_DATE, Security.MaturityDateUsed, [(3.5, 'SCHATZ (0-3.5Y)'), (7.25, 'BOBL (3.5Y-7.25Y)'), (19.5, 'BUND (7.25Y-19.5Y)'), (1000, 'BUXL (19.5Y+)')])";

string result = Regex.Replace(input, @"'([^']*)'", m =>
{
    // m.Value includes the surrounding single quotes, e.g. 'SCHATZ (0-3.5Y)'
    string inner = m.Groups[1].Value;

    // Remove parentheses but keep their content
    inner = Regex.Replace(inner, @"\s*\(([^)]*)\)", " $1");

    // Re-wrap in quotes
    return "'" + inner + "'";
});

System.Console.WriteLine(result);

    }
}
