namespace ByteSocket.RocketCsv.SourceGenerator.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var source = @"
using ByteSocket.RocketCsv.SourceGenerator.Shared;
using ByteSocket.RocketCsv.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.SourceGenerator.Test.CsvMaps
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

        public int? Index { get; set; }
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Company { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string Email { get; set; }
        public DateOnly SubscriptionDate { get; set; }
        public string Website { get; set; }
    }

    [CsvMap]
    public partial class CustomerNameCsvMap : CsvMapBase<Customer>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<Customer> builder)
        {
            builder.MapToColumns()
                        .MapToColumn(x => x.Index, ""Index"")
                        .MapToColumn(x => x.CustomerId, ""Customer Id"")
                        .MapToColumn(x => x.FirstName, ""FirstName"")
                        .MapToColumn(x => x.LastName, ""LastName"")
                        .MapToColumn(x => x.Company, ""Company"")
                        .MapToColumn(x => x.City, ""City"")
                        .MapToColumn(x => x.Country, ""Country"")
                        .MapToColumn(x => x.Phone1, ""Phone 1"")
                        .MapToColumn(x => x.Phone2, ""Phone 2"")
                        .MapToColumn(x => x.Email, ""Email"")
                        .MapToColumn(x => x.SubscriptionDate, ""Subscription Date"")
                        .MapToColumn(x => x.Website, ""Website"");
        }
    }
}
";

            var expectedGenCode = "";

            //var source = File.ReadAllText(@"CsvMaps\Customer.cs") + File.ReadAllText(@"CsvMaps\CustomerIndexCsvMap.cs");

            // Pass the source code to our helper and snapshot test the output
            var genCode = TestHelper.GetGeneratedOutput<CsvMapGenerator>(source, true);

            Assert.True(expectedGenCode == genCode.ElementAt(0));
        }
    }
}