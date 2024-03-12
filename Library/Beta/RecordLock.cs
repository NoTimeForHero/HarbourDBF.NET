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

        public RecordLock(int recNo)
        {
            this.recNo = recNo;
            var locked = DbfHarbour.RecordLock(recNo);
            if (!locked) throw new InvalidOperationException($"Не удалось заблокировать запись!");
        }

        public void Dispose()
        {
            DbfHarbour.RecordLock(recNo, true);
        }
    }
}
