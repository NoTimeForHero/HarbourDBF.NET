using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET.Beta
{
    public readonly ref struct RecordLock
    {
        private readonly int recNo;
        private readonly string alias;

        public RecordLock(int recNo)
        {
            var acquired = DbfRuntime.Invoke(() => Acquire(recNo));
            alias = acquired.Item1;
            this.recNo = acquired.Item2;
        }

        internal RecordLock(string alias, int recNo) { this.alias = alias; this.recNo = recNo; }

        internal static Tuple<string, int> Acquire(int recNo)
        {
            string alias = DbfHarbour.Alias;
            int number = recNo == -1 ? DbfHarbour.ActiveRecord : recNo;
            var locked = DbfHarbour.RecordLock(number);
            if (!locked) throw new InvalidOperationException($"Не удалось заблокировать запись!");
            return Tuple.Create(alias, number);
        }

        public void Dispose()
        {
            var owner = alias;
            var number = recNo;
            DbfRuntime.Invoke(() => Release(owner, number));
        }

        private static void Release(string alias, int recNo)
        {
            var previousAlias = DbfHarbour.Alias;
            bool restore = !string.IsNullOrEmpty(previousAlias) &&
                           !string.Equals(previousAlias, alias, StringComparison.OrdinalIgnoreCase);
            DbfHarbour.SelectArea(alias);
            try
            {
                DbfHarbour.RecordLock(recNo, true);
            }
            finally
            {
                if (restore) DbfHarbour.SelectArea(previousAlias);
            }
        }
    }
}
