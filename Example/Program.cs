using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarbourDBF.NET;
using HarbourDBF.NET.Data;

namespace Example
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Test1.Run();

            //var thTest = new TestThread(4, 10000);
            //thTest.PrepareFiles().RunTests();

            //if (true) return;
            var dayName = DbfHarbour.Eval("CDoW(Date())");
            Console.WriteLine($"День недели: {dayName}");
            Console.WriteLine("Alias: " + DbfHarbour.Alias);

            //DbfHarbour.CloseArea();

            DbfHarbour.Use("city", "REAL1", false);
            Console.WriteLine("Alias: " + DbfHarbour.Alias);
            DbfHarbour.SelectArea("REAL1");
            DbfHarbour.Indexes.Load("City0.cdx");
            DbfHarbour.Indexes.Load("City2.cdx");
            DbfHarbour.Indexes.Select(1);
            DbfHarbour.Indexes.Seek(4);
            var found = DbfHarbour.Indexes.Found();
            Console.WriteLine("Запись удалена: " + (DbfHarbour.IsRecordDeleted ? "ДА" : "НЕТ"));
            Console.WriteLine("Нашли запись: " + (found ? "ДА" : "НЕТ"));
            var foundValues = DbfHarbour.GetValues(new[] { "CITY", "KCITY" });
            Console.WriteLine("Нашли: " + string.Join(", ", foundValues.Select(x => x.ToString())));

            DbfHarbour.Indexes.Seek(8);
            if (!DbfHarbour.Indexes.Found())
            {
                DbfHarbour.Append();
                DbfHarbour.SetValues(new()
                {
                    { "KCITY", 8 }, { "CITY", "Кострома" }, { "KVIEW", 1 }
                });
                DbfHarbour.Commit();
                DbfHarbour.CloseArea("REAL1");
            }

            DbfHarbour.Create("test44", new[]
            {
                new FieldType("USER", "C", 16, 0),
                new FieldType("AGE", "N", 8, 0),
                new FieldType("DATA1", "M", 10, 0),
            });
            DbfHarbour.Create("test45",
                new[] { new FieldType("TEST", "C", 16, 0), });

            // Открываем созданный справочник
            DbfHarbour.Use("test44", "TEST", true);
            Console.WriteLine("Alias: " + DbfHarbour.Alias);

            // Баг был найден при помощи другого бенчмарка
            //DbfHarbour.Use("columns20", "TEST99", true);
            //var test = DbfHarbour.GetValues(new[] { "Age" });

            DbfHarbour.SelectArea("TEST");
            Console.WriteLine("Alias: " + DbfHarbour.Alias);

            // Создаём Запись 1
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Victor" }, { "AGE", 36 } });

            // Создаём Запись 2
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Sergey" }, { "AGE", 42 } });

            for (var i = 0; i < 100; i++)
            {
                DbfHarbour.Append();
                DbfHarbour.SetValues(new() { { "USER", $"Subject #{i + 1}" }, { "AGE", 18 } });
            }

            // Модифицируем запись 21
            DbfHarbour.GoTo(21);
            DbfHarbour.RecordLock();
            DbfHarbour.SetValues(new() { { "DATA1", "Some example data... " } });
            DbfHarbour.RecordLock(unlock: true);

            DbfHarbour.GoTo(1);
            var values = DbfHarbour.GetValues(new[] { "USER", "AGE" });
            Console.WriteLine("Запись 1: " + string.Join(", ", values.Select(x => x.ToString())));

            DbfHarbour.GoTo(2);
            var values2 = DbfHarbour.GetValues(new[] { "USER", "AGE" });
            Console.WriteLine("Запись 2: " + string.Join(", ", values2.Select(x => x.ToString())));

            var total = DbfHarbour.TotalRecords;
            var active = DbfHarbour.ActiveRecord;

            Console.WriteLine($"Указатель находится на записе {active} из {total}");

            DbfHarbour.GoTo(1);

            var range = DbfHarbour.GetValuesRange(
                new[] { "USER", "AGE" }, 1, (uint)DbfHarbour.TotalRecords);

            DbfHarbour.CloseArea("TEST");
            //DbfHarbour.CloseArea("TEST2");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
