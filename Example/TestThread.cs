using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarbourDBF.NET;
using HarbourDBF.NET.Data;

namespace Example
{
    internal class TestThread
    {
        private readonly int threadCount;
        private readonly int recordCount;

        public TestThread(int threadCount, int recordCount)
        {
            this.threadCount = threadCount;
            this.recordCount = recordCount;
        }

        private static string GetData(string alias, int i) => $"{alias}_record{i + 1}";
        private static string GetFilename(int th) => $"testThread{th + 1}";
        private static string GetAlias(int th) => $"DBF{th}";

        public TestThread PrepareFiles(bool rewrite = false)
        {
            foreach (var th in Enumerable.Range(0, threadCount))
            {
                var filename = GetFilename(th);
                var alias = GetAlias(th);

                if (!rewrite && File.Exists($"{filename}.dbf"))
                {
                    Console.WriteLine("File already exists: " + filename);
                    continue;
                }

                DbfHarbour.Create(filename, new[]
                {
                    new FieldType("ID", "N", 8, 0),
                    new FieldType("DATA", "C", 32, 0),
                });
                DbfHarbour.Use(filename, alias);
                Console.WriteLine($"Generating DBF file \"{filename}\" of {recordCount} records:");
                for (var i = 0; i < recordCount; i++)
                {
                    if (i % 1000 == 0) Console.WriteLine($"Generated {i} of {recordCount}...");
                    DbfHarbour.Append();
                    DbfHarbour.SetValues(new() { { "ID", i+1 }, { "DATA", GetData(alias, i) } });
                }
                DbfHarbour.CloseArea(alias);
            }
            return this;
        }

        // BUG: Вызов данного метода приводит к аварийному завершению приложения
        public TestThread RunTests()
        {
            var threads = new List<Thread>();
            foreach (var th in Enumerable.Range(0, threadCount))
            {
                var filename = GetFilename(th);
                var alias = GetAlias(th);
                DbfHarbour.Use(filename, alias);
                var thread = new Thread(() =>
                {
                    var locker = new object();
                    Console.WriteLine("Starting thread: " + alias);
                    for (var i = 0; i < recordCount; i++)
                    {
                        lock (locker)
                        {
                            DbfHarbour.SelectArea(alias);
                            var expected = GetData(alias, i);
                            var got = DbfHarbour.GetValues(new[] { "DATA" })
                                .ToArray().OfType<string>().FirstOrDefault()?.Trim() ?? "";

                            if (expected != got)
                            {
                                Console.WriteLine($"[WARNING] [Thread #{th}]: ${expected} !== ${got}");
                            }

                            DbfHarbour.Skip(1);
                        }
                    }

                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (var thread in threads) thread.Join();
            return this;
        }
    }
}
