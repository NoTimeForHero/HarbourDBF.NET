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


        [DllImport(DllName, EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Use(string databaseName, string alias, bool exclusive = false, string codepage = "");
        /// <summary>
        /// Открытие файла DBF
        /// </summary>
        /// <param name="databaseName">Имя файла который будет открыт без расширения (например test откроет test.dbf с test.fpt)</param>
        /// <param name="alias">Внутренний алиас для открытия и переключения между несколькими открытыми файлами</param>
        /// <param name="exclusive">Открывать в эксклюзивном режиме</param>
        /// <param name="codepage">Кодировка файла (по умолчанию CP866)</param>
        public static void Use(string databaseName, string alias, bool exclusive = false, string codepage = "")
        {
            _Use(databaseName, alias, exclusive, codepage);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_SELECT_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _SelectArea(string alias);
        /// <summary>
        /// Переключиться на указанный алиас
        /// </summary>
        /// <param name="alias">Название алиаса</param>
        public static void SelectArea(string alias)
        {
            _SelectArea(alias);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_CLOSE_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _CloseArea(string alias);
        /// <summary>
        /// Закрыть файл БД для указанного алиаса
        /// </summary>
        /// <param name="alias">Название алиаса</param>
        public static void CloseArea(string alias = null)
        {
            _CloseArea(alias);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(DllName, EntryPoint = "DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Append();
        /// <summary>
        /// Добавить новую запись в конец файла
        /// </summary>
        public static void Append()
        {
            _Append();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(DllName, EntryPoint = "DBF_COMMIT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Commit();
        /// <summary>
        /// Записать изменения на жесткий диск
        /// </summary>
        public static void Commit()
        {
            _Commit();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_GOTO", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GoTo(long recordNumber);
        /// <summary>
        /// Перейти к указанной записи
        /// </summary>
        /// <param name="recordNumber">Индекс записи</param>
        public static void GoTo(long recordNumber)
        {
            _GoTo(recordNumber);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_GET_LAST_ERROR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetLastError();

        [DllImport(DllName, EntryPoint = "DBF_UNSAFE_FREE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr UnsafeFree(IntPtr target);

        /// <summary>
        /// Получить сообщение об ошибке последней операции
        /// </summary>
        /// <returns>Сообщение об ошибке</returns>
        public static bool GetLastError(out string error)
        {
            var ptrText = _GetLastError();
            error = Marshal.PtrToStringAnsi(ptrText);
            UnsafeFree(ptrText);
            return !string.IsNullOrEmpty(error);
        }

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
            if (GetLastError(out var error)) throw new HarbourException(error);
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
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_GET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetValues(string json);
        /// <summary>
        /// Получить значения полей для указанных записей
        /// <para>
        /// Строковые типы возвращаются вместе с пробелами, поэтому не забудьте применить к ним <see cref="string.Trim()"/>
        /// </para>
        /// </summary>
        /// <param name="fieldsNames">Список полей из которых нужны значения</param>
        public static IEnumerable<object> GetValues(IEnumerable<string> fieldsNames)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var inputJson = JsonConvert.SerializeObject(fieldsNames);
            var charPtr = _GetValues(inputJson);
            var outputJson = Marshal.PtrToStringAnsi(charPtr);
            UnsafeFree(charPtr);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (outputJson == null) throw new NullReferenceException("GetValues response is empty!");
            var entries = JsonConvert.DeserializeObject<object[]>(outputJson);
            return entries ?? Array.Empty<object>();
        }

        [DllImport(DllName, EntryPoint = "DBF_RECORD_LOCK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void _RecordLock(int recordNumber, bool unlock);
        /// <summary>
        /// Установить блокировку на конкретной записи
        /// </summary>
        /// <param name="recordNumber">Номер записи, если -1 значит берём последнюю из RecNo()</param>
        /// <param name="unlock">Если true то запись будет разблокирована</param>
        public static void RecordLock(int recordNumber = -1, bool unlock = false)
        {
            _RecordLock(recordNumber, unlock);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(DllName, EntryPoint = "DBF_RECORDS", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int RecordsCounters(bool current);

        /// <summary>
        /// Получить суммарное количество записей в БД
        /// </summary>
        public static int TotalRecords => RecordsCounters(false);

        /// <summary>
        /// Получить номер записи на которой находится указатель
        /// </summary>
        public static int ActiveRecord => RecordsCounters(true);
    }
}
