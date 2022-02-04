using System;
using System.Collections.Generic;

namespace OdataUtils
{
    public class Filter
    {
        public Operators Operator { get; set; } = Operators.None;
        public Comparators Comparator { get; set; } = Comparators.Contains;
        public string FieldLogicalName { get; set; } = "Id";
        public string SearchValue { get; set; } = "---NEVER-MATCH-ANYTHING---";
        public List<string> MultiSelectValues { get; set; }
        public string RawFilterString { get; set; } = String.Empty;
        public bool CaseInsensitive { get; set; } = false;
        public CollectionOperators CollectionOperator { get; set; } = CollectionOperators.None;
        public List<Filter> FilterGroup { get; set; }
    }
}
