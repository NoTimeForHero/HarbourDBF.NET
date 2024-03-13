using HarbourDBF.NET.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET.Beta
{
    internal class InternalUniversal
    {
        // Методы
        public static FieldType[] GetFields() => ProcedureCall<FieldType[]>("LIST_FIELDS");

        public static void SetValues(DatabaseFieldsInfo fields, Dictionary<string, object> inputValues, Encoding enc = null)
        {
            var values = new List<object>();
            foreach (var pair in inputValues)
            {
                var key = pair.Key.ToLower();
                var value = pair.Value;
                if (!fields.OrderByName.TryGetValue(key, out var order))
                    throw new ArgumentException($"Unknown DBF field: {pair.Key}");
                var type = "?";
                if (value is DateTime dt)
                {
                    value = dt.ToString("dd.MM.yyyy");
                    type = "D";
                }
                values.Add(new[] { order, value, type });
            }
            var json = JsonConvert.SerializeObject(values);
            ProcedureCall<JToken>("SET_VALUES", json, enc);
        }

        // Ядро

        [DllImport(Constants.DllName, EntryPoint = "_DBF_PROCEDURE_CALL", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr _ProcedureCall(byte[] command, byte[] data);
        public static TResult ProcedureCall<TResult>(string command, string data = "", Encoding enc = null) where TResult : class
        {
            enc ??= Constants.DefaultEncoding;
            var rawCommand = enc.GetBytes(command);
            var rawData = enc.GetBytes(data);
            var rawPtr = _ProcedureCall(rawCommand, rawData);
            if (DbfHarbour.GetLastError(out var intErr)) throw new HarbourException(intErr);
            var json = UnsafeUtils.GetString(rawPtr, enc);
            var result = JsonConvert.DeserializeObject<JObject>(json);
            if (result == null) throw new NullReferenceException("Response json is null!");

            var error = result["ERROR"]?.ToObject<string>();
            if (error != null) throw new InvalidOperationException(error);

            return result["RESULT"]?.ToObject<TResult>();
        }
    }
}
