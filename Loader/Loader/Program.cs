using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Loader.Data;
using Loader.External;

namespace Loader
{
    internal static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DbfHarbour.Create("test44", new[]
            {
                new FieldType("USER", "C", 16, 0),
                new FieldType("AGE", "N", 8, 0),
                new FieldType("DATA1", "M", 10, 0),
            });

            // Открываем созданный справочник
            DbfHarbour.Use("test44");

            // Создаём Запись 1
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() {{"USER","Victor"},{"AGE",36}});

            // Создаём Запись 2
            DbfHarbour.Append();
            DbfHarbour.SetValues(new() { { "USER", "Sergey" }, { "AGE", 42 } });

            // Модифицируем запись 1
            DbfHarbour.GoTo(1);
            DbfHarbour.SetValues(new() {{ "DATA1", "Some example data..." }});

            for (var i = 0; i < 100; i++)
            {
                DbfHarbour.Append();
                DbfHarbour.SetValues(new() { { "USER", $"Subject #{i+1}" }, { "AGE", 18 } });
            }

            // Закрываем справочник
            DbfHarbour.Use(null);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
