using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;

namespace OdataUtils
{
    /// <summary>
    /// Helper class for programmitcally generating OData request URLs and headers.
    /// Version: 1.0.0
    /// Change Log: First public release.
    /// </summary>
    public class RequestHelper
    {
        private readonly string _odataVersion = "4.0";
        private readonly string _odataMaxVersion = "4.0";

        public RequestHelper(string odataVersion = null, string odataMaxVersion = null)
        {
            if (odataVersion != null) { _odataVersion = odataVersion; }
            if (odataMaxVersion != null) { _odataMaxVersion = odataMaxVersion; }
        }

        public (string, Dictionary<string, string>) GetEntityUrlAndHeaders(string urlBase, string entityCollectionName, List<Filter> filters, List<string> addQueries, List<string> orderBys, List<string> selects, int top = 100, int page = 0, bool useSkipTokens = false)
        {
            var queryParts = new List<string>();
            var headers = GetBaseOdataHeaders();

            var filterQuery = GetFilterQueryByFilters(filters);
            if (filterQuery != String.Empty) { queryParts.Add(filterQuery); }

            if (selects.Count > 0) { queryParts.Add("$select=" + String.Join(",", selects)); }

            // TOP AND PAGINATION (ALL PAGE NUMBERS ARE ZERO-BASED INDEXES)
            queryParts.Add("$count=true");
            if (top != 0)
            {
                queryParts.Add("$top=" + top.ToString());

                if (page > 0)
                {
                    if (useSkipTokens) // HAT TIP: https://vjcity.blogspot.com/2020/06/how-to-get-specific-page-number-in.html
                    {
                        headers.Add("Prefer", "odata.maxpagesize=" + top);
                        queryParts.Add("$skiptoken=<cookie pagenumber='" + (page + 1).ToString() + "' />");

                    }
                    else { queryParts.Add("$skip=" + ((page + 1) * top).ToString()); }
                }
            }

            if (addQueries.Count > 0) { queryParts.AddRange(addQueries); }

            if (orderBys.Count > 0) { queryParts.Add("$orderby=" + String.Join(",", orderBys)); } // EXAMPLE ORDER BY = "title asc"

            return (urlBase + "/" + entityCollectionName + "?" + String.Join("&", queryParts), headers);
        }

        public string GetIntersectEntityCollectionName(string targetLcn, string targetId, string intersectEln)
        {
            // EXAMPLE: msdyncrm_keywords(4db0ac76-cfcd-ea11-a812-000d3a33febe)/msdyncrm_msdyncrm_keyword_msdyncrm_file
            return targetLcn + "(" + targetId + ")/" + intersectEln;
        }

        public string GetEntityMetadataUrl(string urlBase, string entityLogicalName)
        {
            return urlBase + "/" + "EntityDefinitions?$count=true&$filter=LogicalName eq '" + entityLogicalName + "'";
        }

        public string GetPicklistUrl(string urlBase, string metaDataId, string attributeLogicalName, bool isMultiSelect)
        {
            return urlBase + "/" + "EntityDefinitions(" + metaDataId + ")/Attributes/Microsoft.Dynamics.CRM." + (isMultiSelect ? "MultiSelectPicklistAttributeMetadata" : "PicklistAttributeMetadata") + "?$filter=LogicalName eq '" + attributeLogicalName + "'&$count=true&$expand=OptionSet";
        }

        public Dictionary<string, string> GetBaseOdataHeaders()
        {
            return new Dictionary<string, string>() { { "OData-Version", _odataVersion }, { "OData-MaxVersion", _odataMaxVersion } };
        }

        public (string, string) ParseGuidUrl(string url, string baseUrlSuffix)
        {
            int ind = ((baseUrlSuffix == null) || (baseUrlSuffix == String.Empty)) ? -1 : url.IndexOf(baseUrlSuffix);
            string baseUrl = (ind == -1) ? String.Empty : url.Substring(0, ind + baseUrlSuffix.Length);

            Regex rx = new Regex(@"\(([^)]+)\)"); // HAT TIP: https://crmchap.co.uk/extract-new-record-guid-from-dynamics-365-customer-engagement-create-web-api-request-c/
            Match match = rx.Match(url);
            string id = (match.Success && (match.Groups.Count > 1) ? match.Groups[1].Value : String.Empty);

            return (baseUrl, id);
        }

        public string GetFilterQueryByFilters(List<Filter> filters)
        {
            string filtersValue = GetFilterQueryValueByFilters(filters);
            return (filtersValue == String.Empty) ? String.Empty : "$filter=" + filtersValue;
        }

        public string GetFilterQueryValueByFilters(List<Filter> filters, string collectionIteratorPrefix = null)
        {
            string groupFilters = "";
            for (int f = 0; f < filters.Count; f++)
            {
                Filter filter = filters[f];

                if (filter.RawFilterString != String.Empty) { groupFilters += filter.RawFilterString; }
                else
                {
                    string filterOperator = String.Empty;
                    if (f > 0) // ALWAYS IGNORE THE FIRST OPERATOR IN THE GROUP/LIST
                    {
                        switch (filter.Operator)
                        {
                            case Operators.And: filterOperator = " and "; break;
                            case Operators.Or: filterOperator = " or "; break;
                            case Operators.Not: filterOperator = " not "; break;
                            case Operators.AndNot: filterOperator = " and not "; break;
                            case Operators.OrNot: filterOperator = " or not "; break;
                        }
                    }

                    // THREE POSSIBILITIES: STANDARD FILTER, STANDARD FILTER GROUP, COLLECTION FILTER
                    string filterString = String.Empty;
                    bool wrap = true;
                    if (filter.FilterGroup == null) { (filterString, wrap) = GetFilterString(filter, collectionIteratorPrefix); }
                    else if (filter.CollectionOperator != CollectionOperators.None) { filterString = GetCollectionFilterString(filter); }
                    else { filterString = GetFilterQueryValueByFilters(filter.FilterGroup); }

                    groupFilters += filterOperator + (wrap ? "(" : "") + filterString + (wrap ? ")" : "");
                }
            }

            return (groupFilters == String.Empty) ? String.Empty : groupFilters;
        }

        public (string filterString, bool wrap) GetFilterString(Filter filter, string collectionIteratorPrefix = null)
        {
            string str;
            bool wrap = true;

            if (collectionIteratorPrefix != null) { filter.FieldLogicalName = collectionIteratorPrefix + filter.FieldLogicalName; }

            switch (filter.Comparator)
            {
                case Comparators.StartsWith: str = "startswith(" + GetStringFieldName(filter.FieldLogicalName, filter.CaseInsensitive) + "," + GetStringValue(filter.SearchValue, filter.CaseInsensitive) + ")"; wrap = false; break;
                case Comparators.EndsWith: str = "endswith(" + GetStringFieldName(filter.FieldLogicalName, filter.CaseInsensitive) + "," + GetStringValue(filter.SearchValue, filter.CaseInsensitive) + ")"; wrap = false; break;
                case Comparators.StringEquals: str = GetStringFieldName(filter.FieldLogicalName, filter.CaseInsensitive) + " eq " + GetStringValue(filter.SearchValue, filter.CaseInsensitive) + ""; break;
                case Comparators.StringNotEquals: str = GetStringFieldName(filter.FieldLogicalName, filter.CaseInsensitive) + " ne " + GetStringValue(filter.SearchValue, filter.CaseInsensitive) + ""; break;

                case Comparators.NumericEquals: str = filter.FieldLogicalName + " eq " + filter.SearchValue; break;
                case Comparators.NumericNotEquals: str = filter.FieldLogicalName + " ne " + filter.SearchValue; break;
                case Comparators.DateTimeEquals: str = filter.FieldLogicalName + " eq " + filter.SearchValue; break;
                case Comparators.DateTimeNotEquals: str = filter.FieldLogicalName + " ne " + filter.SearchValue; break;
                case Comparators.GreaterThan: str = filter.FieldLogicalName + " gt " + filter.SearchValue; break;
                case Comparators.GreaterThanOrEqual: str = filter.FieldLogicalName + " ge " + filter.SearchValue; break;
                case Comparators.LessThan: str = filter.FieldLogicalName + " lt " + filter.SearchValue; break;
                case Comparators.LessThanOrEqual: str = filter.FieldLogicalName + " le " + filter.SearchValue; break;

                case Comparators.IsNull: str = filter.FieldLogicalName + " eq null"; break;
                case Comparators.IsNotNull: str = filter.FieldLogicalName + " ne null"; break;

                case Comparators.MultiSelectContains: str = GetMultiSelectFilterString(false, filter.FieldLogicalName, filter.MultiSelectValues); wrap = false; break;
                case Comparators.MultiSelectDoesNotContain: str = GetMultiSelectFilterString(true, filter.FieldLogicalName, filter.MultiSelectValues); wrap = false; break;

                default: str = "contains(" + GetStringFieldName(filter.FieldLogicalName, filter.CaseInsensitive) + "," + GetStringValue(filter.SearchValue, filter.CaseInsensitive) + ")"; wrap = false; break;
            }

            return (str, wrap);
        }

        // EXAMPLE: $filter=lead_customer_contacts/any(i: (i/cmcps_webvisitorid eq '1611987051.8992') and (i/cmcps_campaign eq 'googlecpc;11664542764'))
        public string GetCollectionFilterString(Filter filter)
        {
            string collectionIterator = "i";
            string filterString = GetFilterQueryValueByFilters(filter.FilterGroup, collectionIterator + "/");

            string filterCollectionOperator = String.Empty;
            switch (filter.CollectionOperator)
            {
                case CollectionOperators.Any: filterCollectionOperator = "any"; break;
                case CollectionOperators.All: filterCollectionOperator = "all"; break;
            }

            return filter.FieldLogicalName + "/" + filterCollectionOperator + "(" + collectionIterator + ": " + filterString + ")";
        }

        // LOOKS LIKE: $filter=Microsoft.Dynamics.CRM.ContainValues(PropertyName='mshied_contacttype',PropertyValues=['494280010','494280011'])
        public string GetMultiSelectFilterString(bool isNotContains, string ln, List<string> vals)
        {
            string valuesTypeName = isNotContains ? "Microsoft.Dynamics.CRM.DoesNotContainValues" : "Microsoft.Dynamics.CRM.ContainValues";
            string propVals = (vals != null) && (vals.Count > 0) ? "'" + String.Join("','", vals.ToArray()) + "'" : "";
            return valuesTypeName + "(PropertyName='" + ln + "',PropertyValues=[" + propVals + "])";
        }

        private string GetStringFieldName(string name, bool isCaseInsensitive)
        {
            if (isCaseInsensitive) { return "tolower(" + name + ")"; }
            return name;
        }

        private string GetStringValue(string val, bool isCaseInsensitive)
        {
            val = val.Replace("'", "''"); // SINGLE QUOTES ARE ESCAPED WITH AN ADDITIONAL SINGLE QUOTE, EX: 'O''Neal'
            return "'" + HttpUtility.UrlEncode(isCaseInsensitive ? val.ToLower() : val) + "'";
        }
    }
}
