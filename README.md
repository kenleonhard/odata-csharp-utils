# odata-csharp-utils
C# Utils for interacting with OData endpoints.

## Example

### Helper Class
```
using System.Collections.Generic;
using OdataUtils;

namespace App.Helpers
{
    public class ContactsHelper
    {
        private const string apiBaseUrl = "https://my-odata-api.net";
        private const string lcn = "contacts"; // LOGICAL COLLECTION NAME
        private const string stateCodeLn = "statecode";
        private const string typeLn = "mshied_contacttype";
        private const string firstNameLn = "firstname";
        private const string lastNameLn = "lastname";
        private const string mobileLn = "mobilephone";
        private const string stateLn = "address1state";
        private const string email1Ln = "emailaddress1";
        private const string email2Ln = "emailaddress2";
        private const string leadsIntersectLn = "lead_customer_contacts";
        private const int studentContactTypeValue = 1000001; // VALUE OF "Student" PICKLIST ITEM

        public string GetStudentsOdataUrlByEmailAddress(string email)
        {
            RequestHelper odata = new();

            List<string> selects = new() { firstNameLn, lastNameLn, mobileLn, stateLn };
            List<string> orderBys = new() { lastNameLn + " asc", stateLn + " asc" };
            List<string> additionalQueries = new() { "$expand=" + leadsIntersectLn }; // ALSO RETRIEVE CUSTOMER LEADS

            List<Filter> filters = new()
            {
                new Filter() { Comparator = Comparators.NumericEquals, FieldLogicalName = stateCodeLn, SearchValue = "0" }, // ONLY GET ACTIVE ITEMS
                new Filter() { Operator = Operators.And, Comparator = Comparators.MultiSelectContains, FieldLogicalName = typeLn, MultiSelectValues = new List<string> { studentContactTypeValue.ToString() } }, // ONLY GET STUDENTS
                new Filter() // MATCH AT LEAST ONE EMAIL
                {
                    Operator = Operators.And,
                    FilterGroup = new List<Filter>() {
                        new Filter () { Comparator = Comparators.StringEquals, FieldLogicalName = email1Ln, SearchValue = email },
                        new Filter () { Operator = Operators.Or, Comparator = Comparators.StringEquals, FieldLogicalName = email2Ln, SearchValue = email }
                    }
                }
            };

            (string url, Dictionary<string, string> headers) = odata.GetEntityUrlAndHeaders(apiBaseUrl, lcn, filters, additionalQueries, orderBys, selects);

            return url;
        }
    }
}
```
### Usage
```
new ContactsHelper().GetStudentsOdataUrlByEmailAddress("test@test.com")
```

### Returns
```
https://my-odata-api.net/contacts?$filter=(statecode eq 0) and Microsoft.Dynamics.CRM.ContainValues(PropertyName='mshied_contacttype',PropertyValues=['1000001']) and ((emailaddress1 eq 'test%40test.com') or (emailaddress2 eq 'test%40test.com'))&$select=firstname,lastname,mobilephone,address1state&$count=true&$top=100&$expand=lead_customer_contacts&$orderby=lastname asc,address1state asc
```
