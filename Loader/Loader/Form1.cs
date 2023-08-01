using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Loader
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Win32.AllocConsole();
            var handle = Win32.GetConsoleWindow();
            Win32.ShowWindow(handle, 0);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var totalLength = 0; // Переменная для защиты от оптимизации компилятора

            for (var i = 0; i <= 500 * 1000; i++)
            {
                var combined = TestLibrary.Test1("Proccess name!", $"Text{i}");
                totalLength += combined?.Length ?? 0;
            }

            MessageBox.Show($"Test: {totalLength}");
        }

        private string Make(int number)
        {
            var test = Encoding.ASCII.GetBytes($"World: #{number}");
            return Encoding.ASCII.GetString(test);
        }
    }
}
