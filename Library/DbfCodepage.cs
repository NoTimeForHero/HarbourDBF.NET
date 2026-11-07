using System;
using System.Runtime.ExceptionServices;
using Newtonsoft.Json;

namespace HarbourDBF.NET
{
    // Harbour expression evaluation uses the VM codepage, independently of the work area's codepage.
    internal static class DbfCodepage
    {
        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "RU866";
            foreach (char c in value)
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_')
                    throw new ArgumentException("Invalid Harbour codepage identifier.", nameof(value));
            return value;
        }

        internal static T Run<T>(string codepage, Func<T> action)
        {
            string previous = JsonConvert.DeserializeObject<string>(DbfHarbour.Eval("hb_cdpSelect()"));
            bool changed = !string.Equals(previous, codepage, StringComparison.OrdinalIgnoreCase);
            Exception failure = null;
            T result = default;
            try
            {
                if (changed)
                {
                    DbfHarbour.Eval("hb_cdpSelect('" + codepage + "')");
                    string selected = JsonConvert.DeserializeObject<string>(DbfHarbour.Eval("hb_cdpSelect()"));
                    if (!string.Equals(selected, codepage, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Unknown Harbour codepage: " + codepage);
                }
                result = action();
            }
            catch (Exception error) { failure = error; }
            if (changed)
            {
                try { DbfHarbour.Eval("hb_cdpSelect('" + Normalize(previous) + "')"); }
                catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }
    }
}
