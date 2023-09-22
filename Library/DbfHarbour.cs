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
        [DllImport(Constants.DllName, EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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

        [DllImport(Constants.DllName, EntryPoint = "DBF_SELECT_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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

        [DllImport(Constants.DllName, EntryPoint = "DBF_CLOSE_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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


        [DllImport(Constants.DllName, EntryPoint = "DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Append();
        /// <summary>
        /// Добавить новую запись в конец файла
        /// </summary>
        public static void Append()
        {
            _Append();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(Constants.DllName, EntryPoint = "DBF_COMMIT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Commit();
        /// <summary>
        /// Записать изменения на жесткий диск
        /// </summary>
        public static void Commit()
        {
            _Commit();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_GOTO", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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

        [DllImport(Constants.DllName, EntryPoint = "DBF_SKIP", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Skip(int count);
        /// <summary>
        /// Сдвинуть указатель к следующей записи
        /// </summary>
        /// <param name="count">Количество записей для пропуска</param>
        public static void Skip(int count = 1)
        {
            _Skip(count);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(Constants.DllName, EntryPoint = "DBF_GET_LAST_ERROR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetLastError();

        /// <summary>
        /// Получить сообщение об ошибке последней операции
        /// </summary>
        /// <returns>Сообщение об ошибке</returns>
        public static bool GetLastError(out string error)
        {
            error = UnsafeUtils.GetString(_GetLastError());
            return !string.IsNullOrEmpty(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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

        [DllImport(Constants.DllName, EntryPoint = "DBF_SET_VALUES", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr _SetValues(byte[] json);
        /// <summary>
        /// Установить значения для указанных полей на активной записи
        /// </summary>
        /// <param name="values">Словарь поле=значение</param>
        /// <param name="enc">Кодировка в которую будет сконвертирован текст</param>
        public static void SetValues(Dictionary<string, object> values, Encoding enc = null)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var raw = values.Select(x => new[] { x.Key, x.Value, x.Value.GetType().FullName });
            var json = JsonConvert.SerializeObject(raw);
            var converted = Constants.DefaultEncoding.GetBytes(json);
            _SetValues(converted);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_GET_VALUES_RANGE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetValuesRange(string json, uint begin, uint end);

        /// <summary>
        /// Получить значения полей для указанных записей
        /// <para>
        /// Строковые типы возвращаются вместе с пробелами, поэтому не забудьте применить к ним <see cref="string.Trim()"/>
        /// </para>
        /// </summary>
        /// <param name="fieldsNames">Список полей из которых нужны значения</param>
        /// <param name="enc">Кодировка в которую будет сконвертирован текст</param>
        /// <param name="begin">Индекс начала</param>
        /// <param name="end">Индекс конца</param>
        public static IEnumerable<object[]> GetValuesRange(IEnumerable<string> fieldsNames, uint begin, uint end, Encoding enc = null)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var inputJson = JsonConvert.SerializeObject(fieldsNames);
            var raw = _GetValuesRange(inputJson, begin, end);
            var outputJson = UnsafeUtils.GetString(raw, enc);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (outputJson == null) throw new NullReferenceException("GetValues response is empty!");
            var entries = JsonConvert.DeserializeObject<List<object[]>>(outputJson);
            return entries;
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_GET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetValues(string json);
        /// <summary>
        /// Получить значения полей для указанных записей
        /// <para>
        /// Строковые типы возвращаются вместе с пробелами, поэтому не забудьте применить к ним <see cref="string.Trim()"/>
        /// </para>
        /// </summary>
        /// <param name="fieldsNames">Список полей из которых нужны значения</param>
        /// <param name="enc">Кодировка в которую будет сконвертирован текст</param>
        public static IEnumerable<object> GetValues(IEnumerable<string> fieldsNames, Encoding enc = null)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var inputJson = JsonConvert.SerializeObject(fieldsNames);
            var raw = _GetValues(inputJson);
            var outputJson = UnsafeUtils.GetString(raw, enc);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (outputJson == null) throw new NullReferenceException("GetValues response is empty!");
            var entries = JsonConvert.DeserializeObject<object[]>(outputJson);
            return entries ?? Array.Empty<object>();
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_RECORD_LOCK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _RecordLock(int recordNumber, bool unlock);
        /// <summary>
        /// Установить блокировку на конкретной записи
        /// </summary>
        /// <param name="recordNumber">Номер записи, если -1 значит берём последнюю из RecNo()</param>
        /// <param name="unlock">Если true то запись будет разблокирована</param>
        public static bool RecordLock(int recordNumber = -1, bool unlock = false)
        {
            var result = _RecordLock(recordNumber, unlock);
            if (GetLastError(out var error)) throw new HarbourException(error);
            return result;
        }

        [DllImport(Constants.DllName, EntryPoint = "DBF_RECORDS", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int RecordsCounters(bool current);

        [DllImport(Constants.DllName, EntryPoint = "DBF_EOF", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _EndOfFile();

        [DllImport(Constants.DllName, EntryPoint = "DBF_IS_DELETED", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _IsRecordDeleted();

        /// <summary>
        /// Была ли удалена запись
        /// </summary>
        public static bool IsRecordDeleted => _IsRecordDeleted();

        /// <summary>
        /// Определение был ли достигнут конец файла
        /// </summary>
        public static bool EndOfFile => _EndOfFile();

        /// <summary>
        /// Получить суммарное количество записей в БД
        /// </summary>
        public static int TotalRecords => RecordsCounters(false);

        /// <summary>
        /// Получить номер записи на которой находится указатель
        /// </summary>
        public static int ActiveRecord => RecordsCounters(true);

        /// <summary>
        /// Класс со статичекими индексами
        /// </summary>
        public static class Indexes
        {
            [DllImport(Constants.DllName, EntryPoint = "DBF_INDEX_LOAD", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern IntPtr _Load(string indexName);
            /// <summary>
            /// Загрузить указанный индекс (например test0.cdx)
            /// </summary>
            /// <param name="indexName">Путь до файла с индексом</param>
            public static void Load(string indexName)
            {
                _Load(indexName);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "DBF_INDEX_SELECT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern IntPtr _Select(int order);
            /// <summary>
            /// Выбрать указанный индекс для дальнейших операций (например Seek)
            /// </summary>
            /// <param name="order">Порядковый номер индекса</param>
            /// <exception cref="HarbourException"></exception>
            public static void Select(int order)
            {
                _Select(order);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "DBF_INDEX_SEEK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern bool _Seek(int searchValue, bool softSeek = false, bool findLast = false);
            /// <summary>
            /// Найти для активного индекса <see cref="Select(int)"/> нужное значение колонки (например UserId=4)
            /// </summary>
            /// <param name="searchValue">Значение поиска</param>
            /// <param name="softSeek">Если true, то когда запись не найдена указатель будет указывать на ближайшую похожее значение (???) вместо EOF</param>
            /// <param name="findLast">Вести поиск с конца для одинаковых значений</param>
            /// <returns><see cref="bool"/>: Результат поиска записи в справочнике</returns>
            /// <exception cref="HarbourException"></exception>
            public static bool Seek(int searchValue, bool softSeek = false, bool findLast = false)
            {
                var value = _Seek(searchValue, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return value;
            }

            /// <summary>
            /// Проверка, была ли найдена запись после <see cref="Seek(int)"/>
            /// </summary>
            [DllImport(Constants.DllName, EntryPoint = "DBF_FOUND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern bool Found();

        }
    }
}
