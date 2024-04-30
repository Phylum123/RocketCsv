using ByteSocket.RocketCsv.Core.Test.CsvMaps;
using System.IO.MemoryMappedFiles;

namespace ByteSocket.RocketCsv.Core.Test
{
    public class CoreTests
    {
        [Fact]
        public async Task MapIndex()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerCsvMap = new CustomerIndexCsvMap(csvReader);

            await customerCsvMap.SkipRowsAsync(1);
            var customers = await customerCsvMap.ReadDataRowsAsync();

            Assert.True(customers.Count() == 100);
        }

        [Fact]
        public async Task AutomapIndex()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerCsvMap = new CustomerAutoIndexCsvMap(csvReader);

            await customerCsvMap.SkipRowsAsync(1);
            var customers = await customerCsvMap.ReadDataRowsAsync();

            Assert.True(customers.Count() == 100);
        }

        [Fact]
        public async Task MapName()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerCsvMap = new CustomerNameCsvMap(csvReader);

            await customerCsvMap.ReadHeaderRowAsync();
            var customers = await customerCsvMap.ReadDataRowsAsync();

            Assert.True(customers.Count() == 100);
        }

        [Fact]
        public async Task AutoMapName()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100_TooFew.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerCsvMap = new CustomerAutoNameCsvMap(csvReader);

            await customerCsvMap.ReadHeaderRowAsync();
            var customers = await customerCsvMap.ReadDataRowsAsync();

            Assert.True(customers.Count() == 100);
        }

        [Fact]
        public async Task Mixed()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerCsvMap = new CustomerMixCsvMap(csvReader);

            await customerCsvMap.ReadHeaderRowAsync();
            var customers = await customerCsvMap.ReadDataRowsAsync();

            Assert.True(customers.Count() == 100);
        }
    }
}