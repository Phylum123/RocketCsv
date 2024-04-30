// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using ByteSocket.RocketCsv.Benchmarks;
using System.Text;

#if DEBUG
var test = new VsOthers_100();

await test.Test_100_CsvHelperName().ConfigureAwait(false);

Console.WriteLine("Done!!!!!");

#else
BenchmarkRunner.Run<VsOthers_10000>();
BenchmarkRunner.Run<VsOthers_100000>();
#endif

Console.ReadLine();