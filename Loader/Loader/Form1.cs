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
using Loader.External;

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

        private string Make(int number)
        {
            var test = Encoding.ASCII.GetBytes($"World: #{number}");
            return Encoding.ASCII.GetString(test);
        }
    }
}
