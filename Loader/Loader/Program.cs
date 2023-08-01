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
            /*
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            */
            //var mixed = TestLibrary.Test1("Hello", "world");
            //Console.WriteLine("C#: " + mixed);

            DbfHarbour.Create("test44", new[]
            {
                new FieldType("LOGIN", "C", 16, 0),
                new FieldType("USER", "C", 16, 0),
                new FieldType("DATA1", "M", 10, 0),
            });
            DbfHarbour.Use("test44");
            DbfHarbour.Append();
            DbfHarbour.Append();
            DbfHarbour.Append();
            DbfHarbour.Use(null);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
