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
            DbfHarbour.Create("test44", new[]
            {
                new FieldType("USER", "C", 16, 0),
                new FieldType("AGE", "N", 8, 0),
                new FieldType("DATA1", "M", 10, 0),
            });

            // ReSharper disable once JoinDeclarationAndInitializer
            string error;

            // Открываем созданный справочник
            DbfHarbour.Use("test44", "TEST", false);

            error = DbfHarbour.GetLastError();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            // Создаём Запись 1
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Victor" }, { "AGE", 36 } });
            error = DbfHarbour.GetLastError();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            // Создаём Запись 2
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Sergey" }, { "AGE", 42 } });
            error = DbfHarbour.GetLastError();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            for (var i = 0; i < 100; i++)
            {
                DbfHarbour.Append();
                DbfHarbour.SetValues(new() { { "USER", $"Subject #{i + 1}" }, { "AGE", 18 } });
            }

            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            // Модифицируем запись 21
            DbfHarbour.GoTo(21);
            DbfHarbour.RecordLock();
            DbfHarbour.SetValues(new() { { "DATA1", "Some example data..." } });
            DbfHarbour.RecordLock(unlock: true);
            error = DbfHarbour.GetLastError();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            // Закрываем справочник
            //DbfHarbour.Use(null);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
