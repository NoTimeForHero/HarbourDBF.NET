using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HarbourDBF.NET.Data;
using Newtonsoft.Json;

namespace HarbourDBF.NET
{
    public static class DbfHarbour
    {
        private const string DllName = "hbdbf.dll";

        // TODO: Добавить GetLastError для каждого поля

        // TODO: Добавить следующие методы
        // 1. DbSelectArea(cAliasName) - переключиться на указанный алиас
        // 2. DbCommit() - сброс изменений на диск
        // 3. DbCloseArea() - закрыть активную DbArea

        /// <summary>
        /// Открытие файла DBF
        /// </summary>
        /// <param name="databaseName">Имя файла который будет открыт без расширения (например test откроет test.dbf с test.fpt)</param>
        /// <param name="cAlias">Внутренний алиас для открытия и переключения между несколькими открытыми файлами</param>
        /// <param name="lExclusive">Открывать в эксклюзивном режиме</param>
        /// <param name="cCodepage">Кодировка файла (по умолчанию CP866)</param>
        [DllImport(DllName, EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Use(string databaseName, string cAlias, bool lExclusive = false, string cCodepage = "");

        /// <summary>
        /// Добавить новую запись в конец файла
        /// </summary>
        [DllImport(DllName, EntryPoint = "DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Append();

        /// <summary>
        /// Перейти к указанной записи
        /// </summary>
        /// <param name="recordNumber">Индекс записи</param>
        [DllImport(DllName, EntryPoint = "DBF_GOTO", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr GoTo(long recordNumber);

        [DllImport(DllName, EntryPoint = "DBF_GET_LAST_ERROR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetLastError();
        /// <summary>
        /// Получить сообщение об ошибке последней операции
        /// </summary>
        /// <returns>Сообщение об ошибке</returns>
        public static string GetLastError() => Marshal.PtrToStringAnsi(_GetLastError());

        [DllImport(DllName, EntryPoint = "DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Create(string databaseName, string json);
        /// <summary>
        /// Создать новый DBF файл с указанной структурой
        /// </summary>
        /// <param name="databaseName">Имя базы данных</param>
        /// <param name="fields">Список полей</param>
        public static void Create(string databaseName, IEnumerable<FieldType> fields)
        {
            var json = JsonConvert.SerializeObject(fields.Select(x => x.ToJson()));
            _Create(databaseName, json);
        }

        [DllImport(DllName, EntryPoint = "DBF_SET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _SetValues(string json);
        /// <summary>
        /// Установить значения для указанных полей на активной записи
        /// </summary>
        /// <param name="values">Словарь поле=значение</param>
        public static void SetValues(Dictionary<string, object> values)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var raw = values.Select(x => new[] { x.Key, x.Value, x.Value.GetType().FullName });
            var json = JsonConvert.SerializeObject(raw);
            _SetValues(json);
        }

        /// <summary>
        /// Установить блокировку на конкретной записи
        /// </summary>
        /// <param name="recordNumber">Номер записи, если -1 значит берём последнюю из RecNo()</param>
        /// <param name="unlock">Если true то запись будет разблокирована</param>
        [DllImport(DllName, EntryPoint = "DBF_RECORD_LOCK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void RecordLock(int recordNumber = -1, bool unlock = false);
    }
}
