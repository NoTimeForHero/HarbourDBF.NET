using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HarbourDBF.NET.Data;
using Newtonsoft.Json;

namespace HarbourDBF.NET
{
    public static partial class DbfHarbour
    {
        [DllImport(Constants.DllName, EntryPoint = "_DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Use(databaseName, alias, exclusive, codepage)); return; }
            DbfSession.CheckCallback();
            _Use(databaseName, alias, exclusive, codepage);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_USE_READONLY", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _UseReadOnly(string databaseName, string alias, bool exclusive, string codepage);

        /// <summary>Открыть таблицу без права записи на уровне Harbour RDD.</summary>
        public static void UseReadOnly(string databaseName, string alias, bool exclusive = false, string codepage = "")
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => UseReadOnly(databaseName, alias, exclusive, codepage)); return; }
            DbfSession.CheckCallback();
            _UseReadOnly(databaseName, alias, exclusive, codepage);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_SELECT_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _SelectArea(string alias);
        /// <summary>
        /// Переключиться на указанный алиас
        /// </summary>
        /// <param name="alias">Название алиаса</param>
        public static void SelectArea(string alias)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => SelectArea(alias)); return; }
            DbfSession.CheckCallback();
            _SelectArea(alias);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_CLOSE_AREA", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _CloseArea(string alias);
        /// <summary>
        /// Закрыть файл БД для указанного алиаса
        /// </summary>
        /// <param name="alias">Название алиаса</param>
        public static void CloseArea(string alias)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => CloseArea(alias)); return; }
            DbfSession.CheckCallback();
            var success = _CloseArea(alias);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (!success) throw new HarbourException($"Alias is not exists: {alias}");
        }


        [DllImport(Constants.DllName, EntryPoint = "_DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Append();
        /// <summary>
        /// Добавить новую запись в конец файла
        /// </summary>
        public static void Append()
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(Append); return; }
            DbfSession.CheckCallback();
            _Append();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_DELETE", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _Delete();

        /// <summary>Пометить текущую запись удалённой.</summary>
        public static void Delete()
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(Delete); return; }
            DbfSession.CheckCallback();
            _Delete();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_RECALL", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _Recall();

        /// <summary>Снять отметку удаления с текущей записи.</summary>
        public static void Recall()
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(Recall); return; }
            DbfSession.CheckCallback();
            _Recall();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(Constants.DllName, EntryPoint = "_DBF_COMMIT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Commit();
        /// <summary>
        /// Записать изменения на жесткий диск
        /// </summary>
        public static void Commit()
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(Commit); return; }
            DbfSession.CheckCallback();
            _Commit();
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_GOTO64", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GoTo(long recordNumber);
        /// <summary>
        /// Перейти к указанной записи
        /// </summary>
        /// <param name="recordNumber">Номер записи от 0 до 4294967295 включительно.</param>
        public static void GoTo(long recordNumber)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => GoTo(recordNumber)); return; }
            DbfSession.CheckCallback();
            if (recordNumber < 0 || recordNumber > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(recordNumber));
            _GoTo(recordNumber);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_SKIP", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Skip(int count);
        /// <summary>
        /// Сдвинуть указатель к следующей записи
        /// </summary>
        /// <param name="count">Количество записей для пропуска</param>
        public static void Skip(int count = 1)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Skip(count)); return; }
            DbfSession.CheckCallback();
            _Skip(count);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }


        [DllImport(Constants.DllName, EntryPoint = "_DBF_GET_LAST_ERROR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetLastError();

        /// <summary>
        /// Получить сообщение об ошибке последней операции
        /// </summary>
        /// <returns>Сообщение об ошибке</returns>
        public static bool GetLastError(out string error)
        {
            if (!DbfRuntime.IsExecutorThread) { string message = null; bool found = DbfRuntime.Invoke(() => GetLastError(out message)); error = message; return found; }
            DbfSession.CheckCallback();
            error = UnsafeUtils.GetString(_GetLastError());
            return !string.IsNullOrEmpty(error);
        }


        [DllImport(Constants.DllName, EntryPoint = "_DBF_EVAL", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Eval(string code);

        /// <summary>
        /// Выполнить указанный код через & оператор Harbour-а
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string Eval(string code)
        {
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Eval(code));
            DbfSession.CheckCallback();
            var raw =_Eval(code);
            var result = UnsafeUtils.GetString(raw);
            if (GetLastError(out var error)) throw new HarbourException(error);
            return result;
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_GET_ALIAS", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetAlias();

        public static string Alias => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return UnsafeUtils.GetString(GetAlias()); });

        [DllImport(Constants.DllName, EntryPoint = "_DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Create(string databaseName, string json);
        /// <summary>
        /// Создать новый DBF файл с указанной структурой
        /// </summary>
        /// <param name="databaseName">Имя базы данных</param>
        /// <param name="fields">Список полей</param>
        public static void Create(string databaseName, IEnumerable<FieldType> fields)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Create(databaseName, fields)); return; }
            DbfSession.CheckCallback();
            var json = JsonConvert.SerializeObject(fields.Select(x => x.ToJson()));
            _Create(databaseName, json);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

         [DllImport(Constants.DllName, EntryPoint = "_DBF_SET_VALUES", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr _SetValues(byte[] json);
        /// <summary>
        /// Установить значения для указанных полей на активной записи
        /// </summary>
        /// <param name="values">Словарь поле=значение</param>
        /// <param name="enc">Кодировка в которую будет сконвертирован текст</param>
        public static void SetValues(Dictionary<string, object> values, Encoding enc = null)
        {
            if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => SetValues(values, enc)); return; }
            DbfSession.CheckCallback();
            enc = enc ?? Constants.DefaultEncoding;
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var raw = values.Select(x => new[] { x.Key, x.Value, x.Value.GetType().FullName });
            var json = JsonConvert.SerializeObject(raw);
            var converted = UnsafeUtils.GetNullTerminatedBytes(json, enc);
            _SetValues(converted);
            if (GetLastError(out var error)) throw new HarbourException(error);
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_GET_VALUES_RANGE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => GetValuesRange(fieldsNames, begin, end, enc));
            DbfSession.CheckCallback();
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var inputJson = JsonConvert.SerializeObject(fieldsNames);
            var raw = _GetValuesRange(inputJson, begin, end);
            var outputJson = UnsafeUtils.GetString(raw, enc);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (outputJson == null) throw new NullReferenceException("GetValues response is empty!");
            var entries = JsonConvert.DeserializeObject<List<object[]>>(outputJson);
            return entries;
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_GET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => GetValues(fieldsNames, enc));
            DbfSession.CheckCallback();
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var inputJson = JsonConvert.SerializeObject(fieldsNames);
            var raw = _GetValues(inputJson);
            var outputJson = UnsafeUtils.GetString(raw, enc);
            if (GetLastError(out var error)) throw new HarbourException(error);
            if (outputJson == null) throw new NullReferenceException("GetValues response is empty!");
            var entries = JsonConvert.DeserializeObject<object[]>(outputJson);
            return entries ?? Array.Empty<object>();
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_RECORD_LOCK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _RecordLock(int recordNumber, bool unlock);
        /// <summary>
        /// Установить блокировку на конкретной записи
        /// </summary>
        /// <param name="recordNumber">Номер записи, если -1 значит берём последнюю из RecNo()</param>
        /// <param name="unlock">Если true то запись будет разблокирована</param>
        public static bool RecordLock(int recordNumber = -1, bool unlock = false)
        {
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => RecordLock(recordNumber, unlock));
            DbfSession.CheckCallback();
            var result = _RecordLock(recordNumber, unlock);
            if (GetLastError(out var error)) throw new HarbourException(error);
            return result;
        }

        [DllImport(Constants.DllName, EntryPoint = "_DBF_RECORDS", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int RecordsCounters(bool current);

        [DllImport(Constants.DllName, EntryPoint = "_DBF_EOF", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _EndOfFile();

        [DllImport(Constants.DllName, EntryPoint = "_DBF_IS_DELETED", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool _IsRecordDeleted();

        [DllImport(Constants.DllName, EntryPoint = "_DBF_RECORD_UPDATED", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool _IsRecordUpdated();

        internal static bool IsRecordUpdated()
        {
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(IsRecordUpdated);
            DbfSession.CheckCallback();
            var updated = _IsRecordUpdated();
            if (GetLastError(out var error)) throw new HarbourException(error);
            return updated;
        }

        /// <summary>
        /// Была ли удалена запись
        /// </summary>
        public static bool IsRecordDeleted => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return _IsRecordDeleted(); });

        /// <summary>
        /// Определение был ли достигнут конец файла
        /// </summary>
        public static bool EndOfFile => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return _EndOfFile(); });

        /// <summary>
        /// Получить суммарное количество записей в БД
        /// </summary>
        public static int TotalRecords => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return RecordsCounters(false); });

        /// <summary>
        /// Получить номер записи на которой находится указатель
        /// </summary>
        public static int ActiveRecord => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return RecordsCounters(true); });

        /// <summary>
        /// Класс со статичекими индексами
        /// </summary>
        public static class Indexes
        {
            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_LOAD", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern IntPtr _Load(string indexName);
            /// <summary>
            /// Загрузить указанный индекс (например test0.cdx)
            /// </summary>
            /// <param name="indexName">Путь до файла с индексом</param>
            public static void Load(string indexName)
            {
                if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Load(indexName)); return; }
                DbfSession.CheckCallback();
                _Load(indexName);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SELECT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern IntPtr _Select(int order);
            /// <summary>
            /// Выбрать указанный индекс для дальнейших операций (например Seek)
            /// </summary>
            /// <param name="order">Порядковый номер индекса</param>
            /// <exception cref="HarbourException"></exception>
            public static void Select(int order)
            {
                if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Select(order)); return; }
                DbfSession.CheckCallback();
                _Select(order);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SELECT_STR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern IntPtr _Select(string order);
            /// <summary>
            /// Выбрать указанный индекс для дальнейших операций (например Seek)
            /// </summary>
            /// <param name="order">Порядковый номер индекса</param>
            /// <exception cref="HarbourException"></exception>
            public static void Select(string order)
            {
                if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Select(order)); return; }
                DbfSession.CheckCallback();
                _Select(order);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SEEK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
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
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Seek(searchValue, softSeek, findLast));
                DbfSession.CheckCallback();
                var value = _Seek(searchValue, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return value;
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SEEK_NUMBER", CallingConvention = CallingConvention.Cdecl)]
            private static extern bool _SeekNumber(double key, bool softSeek, bool findLast);

            /// <summary>Поиск числового ключа с представлением Harbour/CDX double.</summary>
            public static bool Seek(double key, bool softSeek = false, bool findLast = false)
            {
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Seek(key, softSeek, findLast));
                DbfSession.CheckCallback();
                if (double.IsNaN(key) || double.IsInfinity(key)) throw new ArgumentOutOfRangeException(nameof(key));
                var found = _SeekNumber(key, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return found;
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SEEK_TEXT", CallingConvention = CallingConvention.Cdecl)]
            private static extern bool _SeekText(byte[] searchValue, bool softSeek, bool findLast);

            /// <summary>Поиск строкового ключа в кодировке открытой таблицы (по умолчанию CP866).</summary>
            public static bool Seek(string searchValue, bool softSeek = false, bool findLast = false, Encoding encoding = null)
            {
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Seek(searchValue, softSeek, findLast, encoding));
                DbfSession.CheckCallback();
                if (searchValue == null) throw new ArgumentNullException(nameof(searchValue));
                if (searchValue.IndexOf('\0') >= 0) throw new ArgumentException("A key cannot contain a null character.", nameof(searchValue));
                var strictEncoding = (Encoding)(encoding ?? Constants.DefaultEncoding).Clone();
                strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
                var key = UnsafeUtils.GetNullTerminatedBytes(searchValue, strictEncoding);
                var value = _SeekText(key, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return value;
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SEEK_TEMPORAL", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern bool _SeekTemporal(string key, bool timestamp, bool softSeek, bool findLast);

            /// <summary>Поиск ключа типа Date; время в значении не используется.</summary>
            public static bool SeekDate(DateTime key, bool softSeek = false, bool findLast = false)
            {
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => SeekDate(key, softSeek, findLast));
                DbfSession.CheckCallback();
                return SeekTemporal(key.ToString("yyyyMMdd", CultureInfo.InvariantCulture), false, softSeek, findLast);
            }

            /// <summary>Поиск ключа типа Timestamp с точностью до миллисекунды, без преобразования часового пояса.</summary>
            public static bool SeekTimestamp(DateTime key, bool softSeek = false, bool findLast = false)
            {
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => SeekTimestamp(key, softSeek, findLast));
                DbfSession.CheckCallback();
                if (key.Ticks % TimeSpan.TicksPerMillisecond != 0)
                    throw new ArgumentException("Timestamp precision is limited to milliseconds.", nameof(key));
                return SeekTemporal(key.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), true, softSeek, findLast);
            }

            private static bool SeekTemporal(string key, bool timestamp, bool softSeek, bool findLast)
            {
                var found = _SeekTemporal(key, timestamp, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return found;
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_INDEX_SEEK_LOGICAL", CallingConvention = CallingConvention.Cdecl)]
            private static extern bool _SeekLogical(bool key, bool softSeek, bool findLast);

            /// <summary>Поиск логического ключа индекса.</summary>
            public static bool Seek(bool key, bool softSeek = false, bool findLast = false)
            {
                if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Seek(key, softSeek, findLast));
                DbfSession.CheckCallback();
                var found = _SeekLogical(key, softSeek, findLast);
                if (GetLastError(out var error)) throw new HarbourException(error);
                return found;
            }

            /// <summary>
            /// Создание нового индекса в памяти
            /// </summary>
            /// <param name="alias">Алиас базы данных</param>
            /// <param name="tag">Название тэга индекса</param>
            /// <param name="codeIndex">Harbour-код индекса</param>
            /// <param name="codeFor">Harbour-код условия</param>
            /// <exception cref="HarbourException"></exception>
            public static void Create(string alias, string tag, string codeIndex, string codeFor)
            {
                if (!DbfRuntime.IsExecutorThread) { DbfRuntime.Invoke(() => Create(alias, tag, codeIndex, codeFor)); return; }
                DbfSession.CheckCallback();
                _Create(alias, tag, codeIndex, codeFor);
                if (GetLastError(out var error)) throw new HarbourException(error);
            }

            [DllImport(Constants.DllName, EntryPoint = "_DBF_MAKE_INDEX", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern void _Create(string alias, string tag, string codeIndex, string codeFor);

            /// <summary>
            /// Проверка, была ли найдена запись после <see cref="Seek(int)"/>
            /// </summary>
            [DllImport(Constants.DllName, EntryPoint = "_DBF_FOUND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern bool _Found();
            public static bool Found() => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return _Found(); });

            /// <summary>
            /// Получить количество записей попадающих в индекс
            /// </summary>
            [DllImport(Constants.DllName, EntryPoint = "_DBF_ORD_KEY_COUNT", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern int _OrdKeyCount();
            public static int OrdKeyCount() => DbfRuntime.Invoke(() => { DbfSession.CheckCallback(); return _OrdKeyCount(); });

        }
    }
}
