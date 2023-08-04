﻿using System;
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
            DbfHarbour.Create("test45",
                new[] { new FieldType("TEST", "C", 16, 0), });

            // Открываем созданный справочник
            DbfHarbour.Use("test44", "TEST", false);
            DbfHarbour.Use("test45", "TEST3", false);

            DbfHarbour.SelectArea("TEST");


            // Создаём Запись 1
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Victor" }, { "AGE", 36 } });

            // Создаём Запись 2
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Sergey" }, { "AGE", 42 } });

            DbfHarbour.Commit();
            Console.WriteLine("Three records write?");
            Console.ReadKey();

            for (var i = 0; i < 100; i++)
            {
                DbfHarbour.Append();
                DbfHarbour.SetValues(new() { { "USER", $"Subject #{i + 1}" }, { "AGE", 18 } });
            }

            // Модифицируем запись 21
            DbfHarbour.GoTo(21);
            DbfHarbour.RecordLock();
            DbfHarbour.SetValues(new() { { "DATA1", "Some example data..." } });
            DbfHarbour.RecordLock(unlock: true);

            Console.WriteLine("Have not memo data?");
            Console.ReadKey();
            DbfHarbour.Commit();

            // Закрываем справочник
            //DbfHarbour.Use(null);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
