using System;
using System.Collections.Generic;

namespace CreditReport.Models
{
    /// <summary>
    /// Represents a single substitution element with display name and text
    /// </summary>
    public class CreditSubstitutionElement
    {
        public string DisplayName { get; set; }
        public string SubstitutionText { get; set; }
    }

    /// <summary>
    /// Represents a text substitution configuration
    /// </summary>
    public class CreditTextSubstitution
    {
        public string Token { get; set; }
        public string Description { get; set; }
        public bool Multi { get; set; }
        public List<CreditSubstitutionElement> SubstitutionElements { get; set; }

        public CreditTextSubstitution()
        {
            SubstitutionElements = new List<CreditSubstitutionElement>();
        }
    }

    /// <summary>
    /// Represents a key-value substitution map
    /// </summary>
    public class CreditTextSubstitutionMap
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public bool Multi { get; set; }
    }

    /// <summary>
    /// Represents query text substitutions with override capability
    /// </summary>
    public class CreditQueryTextSubstitutions
    {
        public List<CreditTextSubstitutionMap> Override { get; set; }
        public Dictionary<string, List<CreditTextSubstitutionMap>> AdditionalSubstitutions { get; set; }

        public CreditQueryTextSubstitutions()
        {
            Override = new List<CreditTextSubstitutionMap>();
            AdditionalSubstitutions = new Dictionary<string, List<CreditTextSubstitutionMap>>();
        }
    }
}
