using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET
{
    /// <summary>
    /// Исключения которые выдаёт HarbourVM
    /// </summary>
    public class HarbourException : Exception
    {
        internal HarbourException(string message) : base(message) {}
    }
}
