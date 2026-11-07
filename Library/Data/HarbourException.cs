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
        internal HarbourException(string message, Exception innerException) : base(message, innerException) {}
    }

    public sealed class DbfHarbourOperationException : HarbourException
    {
        internal DbfHarbourOperationException(string tablePath, string operation, HarbourException inner)
            : base(operation + " failed for " + tablePath + ": " + inner.Message, inner)
        { TablePath = tablePath; Operation = operation; }
        public string TablePath { get; }
        public string Operation { get; }
    }

    public sealed class DbfMappingException : InvalidOperationException
    {
        internal DbfMappingException(string tablePath, string message) : base("Opening " + tablePath + ": " + message)
        { TablePath = tablePath; }
        public string TablePath { get; }
    }

    public sealed class DbfValueException : InvalidOperationException
    {
        internal DbfValueException(string tablePath, long recordNumber, string field, Exception inner)
            : base("Reading field " + field + " of record " + recordNumber + " in " + tablePath + " failed: " + inner.Message, inner)
        { TablePath = tablePath; RecordNumber = recordNumber; Field = field; }
        public string TablePath { get; }
        public long RecordNumber { get; }
        public string Field { get; }
    }
}
