using ByteSocket.RocketCsv.SourceGenerator.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Core.Test.CsvMaps
{
    public class Customer
    {
        private Customer(string customerId)
        {
            CustomerId = customerId;
        }
        public Customer(string customerId, string firstName)
        {
            CustomerId = customerId;
            FirstName = firstName;
        }

        public int? Index { get; init; } = 0;
        public string CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Company { get; set; } = "";
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public string Phone1 { get; set; } = "";
        public string Phone2 { get; set; } = "";
        public string Email { get; set; } = "";
        public DateOnly SubscriptionDate { get; set; } = DateOnly.MinValue;
        public string Website { get; set; } = "";
    }

    [CsvMap]
    public partial class CustomerIndexCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder.MapToColumns()
                        .MapToColumn(x => x.Index, 0)
                        .MapToColumn(x => x.CustomerId, 1)
                        .MapToColumn(x => x.FirstName, 2)
                        .MapToColumn(x => x.LastName, 3)
                        .MapToColumn(x => x.Company, 4)
                        .MapToColumn(x => x.City, 5)
                        .MapToColumn(x => x.Country, 6)
                        .MapToColumn(x => x.Phone1, 7)
                        .MapToColumn(x => x.Phone2, 8)
                        .MapToColumn(x => x.Email, 9)
                        .MapToColumn(x => x.SubscriptionDate, 10)
                        .MapToColumn(x => x.Website, 11);
        }
    }

    [CsvMap]
    public partial class CustomerNameCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder.MapToColumns()
                        .MapToColumn(x => x.Index, "Index")
                        .MapToColumn(x => x.CustomerId, "Customer Id")
                        .MapToColumn(x => x.FirstName, "FirstName")
                        .MapToColumn(x => x.LastName, "LastName")
                        .MapToColumn(x => x.Company, "Company")
                        .MapToColumn(x => x.City, "City")
                        .MapToColumn(x => x.Country, "Country")
                        .MapToColumn(x => x.Phone1, "Phone 1")
                        .MapToColumn(x => x.Phone2, "Phone 2")
                        .MapToColumn(x => x.Email, "Email")
                        .MapToColumn(x => x.SubscriptionDate, "Subscription Date")
                        .MapToColumn(x => x.Website, "Website");
        }
    }

    [CsvMap]
    public partial class CustomerMixCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder.MapToColumns()
                        .MapToColumn(x => x.Index, 0)
                        .MapToColumn(x => x.CustomerId, 1)
                        .MapToColumn(x => x.FirstName, 2)
                        .MapToColumn(x => x.LastName, 3)
                        .MapToColumn(x => x.Company, "Company")
                        .MapToColumn(x => x.City, "City")
                        .MapToColumn(x => x.Country, "Country")
                        .MapToColumn(x => x.Phone1, "Phone 1")
                        .MapToColumn(x => x.Phone2, 8)
                        .MapToColumn(x => x.Email, "Email")
                            .CustomParse(ParseEmail)
                        .MapToColumn(x => x.SubscriptionDate, "Subscription Date")
                            .CustomParse(static (colSpan, rowIndex, colIndex) => DateOnly.Parse(colSpan))
                        .MapToColumn(x => x.Website, "Website")
                            .CustomParse(static (colSpan, rowIndex, colIndex) => { return colSpan.ToString(); });
        }

        public static string ParseEmail(scoped ReadOnlySpan<char> colSpan, long rowIndex, int colIndex)
        {
            return "";
        }
    }

    [CsvMap]
    public partial class CustomerAutoIndexCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder.AutoMapByIndex();
        }
    }

    [CsvMap]
    public partial class CustomerAutoNameCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder
                .ChooseConstructor<string>()
                .AllowTooFewColumns()
                .AutoMapByName(StringComparison.OrdinalIgnoreCase);
        }
    }
}
