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
            alias = DbfHarbour.Alias;
            this.recNo = recNo == -1 ? DbfHarbour.ActiveRecord : recNo;
            var locked = DbfHarbour.RecordLock(this.recNo);
            if (!locked) throw new InvalidOperationException($"Не удалось заблокировать запись!");
        }

        public void Dispose()
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
