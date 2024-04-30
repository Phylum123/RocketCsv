using ByteSocket.RocketCsv.SourceGenerator.Shared;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Core.Benchmarks.CsvMaps
{
    public class Customer
    {
        public Customer()
        {
        }

        public Customer(string customerId)
        {
            CustomerId = customerId;
        }

        public int Index { get; init; } = 0;
        public string CustomerId { get; set; } = "";
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

    //CSV Helper Class Map
    public sealed class CustomerClassIndexMap : ClassMap<Customer>
    {
        public CustomerClassIndexMap()
        {
            Map(x => x.Index).Index(0);
            Map(x => x.CustomerId).Index(1);
            Map(x => x.FirstName).Index(2);
            Map(x => x.LastName).Index(3);
            Map(x => x.Company).Index(4);
            Map(x => x.City).Index(5);
            Map(x => x.Country).Index(6);
            Map(x => x.Phone1).Index(7);
            Map(x => x.Phone2).Index(8);
            Map(x => x.Email).Index(9);
            Map(x => x.SubscriptionDate).Index(10);
            Map(x => x.Website).Index(11);
        }
    }

    //CSV Helper Class Map
    public sealed class CustomerClassNameMap : ClassMap<Customer>
    {
        public CustomerClassNameMap()
        {
            Map(x => x.Index).Name("Index");
            Map(x => x.CustomerId).Name("Customer Id");
            Map(x => x.FirstName).Name("First Name");
            Map(x => x.LastName).Name("Last Name");
            Map(x => x.Company).Name("Company");
            Map(x => x.City).Name("City");
            Map(x => x.Country).Name("Country");
            Map(x => x.Phone1).Name("Phone 1");
            Map(x => x.Phone2).Name("Phone 2");
            Map(x => x.Email).Name("Email");
            Map(x => x.SubscriptionDate).Name("Subscription Date");
            Map(x => x.Website).Name("Website");
        }
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
                        .MapToColumn(x => x.FirstName, "First Name")
                        .MapToColumn(x => x.LastName, "Last Name")
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

}
