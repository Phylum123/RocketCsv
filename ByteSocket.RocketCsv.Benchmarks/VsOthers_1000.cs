using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ByteSocket.RocketCsv.Core;
using ByteSocket.RocketCsv.Core.Benchmarks.CsvMaps;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class VsOthers_1000
    {
        public VsOthers_1000()
        {
        }

        [Benchmark]
        public async Task Test_1000_RocketCsvIndexMap()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-1000.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerIndexCsvMap = new CustomerIndexCsvMap(csvReader);

            await customerIndexCsvMap.SkipRowsAsync(1).ConfigureAwait(false);
            var customers = await customerIndexCsvMap.ReadDataRowsAsync().ConfigureAwait(false);
        }

        [Benchmark]
        public async Task Test_1000_RocketCsvNameMap()
        {
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-1000.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var customerNameCsvMap = new CustomerNameCsvMap(csvReader);

            await customerNameCsvMap.ReadHeaderRowAsync().ConfigureAwait(false);

            var customers = await customerNameCsvMap.ReadDataRowsAsync().ConfigureAwait(false);
        }

        [Benchmark]
        public async Task Test_1000_CsvHelperIndex()
        {
            var recordList = new List<Customer>();

            using (var reader = new StreamReader(@"CsvFiles\customers-1000.csv"))
            using (var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<CustomerClassIndexMap>();

                await foreach (var record in csv.GetRecordsAsync<Customer>().ConfigureAwait(false))
                {
                    recordList.Add(record);
                }

            }

        }

        [Benchmark]
        public async Task Test_1000_CsvHelperName()
        {
            var recordList = new List<Customer>();

            using (var reader = new StreamReader(@"CsvFiles\customers-1000.csv"))
            using (var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<CustomerClassNameMap>();

                await foreach (var record in csv.GetRecordsAsync<Customer>().ConfigureAwait(false))
                {
                    recordList.Add(record);
                }

            }

        }

    }
}
