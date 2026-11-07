using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarbourDBF.NET;
using HarbourDBF.NET.Beta;
using HarbourDBF.NET.Data;

// Small runnable examples: all writes happen on temporary fixture copies.
internal static class Program
{
    private static readonly Encoding Cp866 = Encoding.GetEncoding(866);

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--hold-lock") return HoldLock(args[1]);
        if (args.Length == 3 && args[0] == "--concurrent-update") return ConcurrentUpdate(args[1], int.Parse(args[2], CultureInfo.InvariantCulture));
        if (args.Length == 1 && args[0] == "--runtime-lifecycle") return RuntimeLifecycle();
        try { PathResolution(); MappingDeclarations(); ExactIntegerConversion(); ExecutorQueue(); RuntimeLifecycleProcess(); }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
        if (IntPtr.Size != 4) { Console.Error.WriteLine("The current native DLL requires an x86 process."); return 1; }
        if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hbdbf.dll"))) { Console.Error.WriteLine("Build HarbourLibrary/hbdbf.dll, then rebuild LightTests."); return 1; }
        string fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");
        string working = Path.Combine(Path.GetTempPath(), "harbourdbf-light-" + Guid.NewGuid().ToString("N"));
        try
        {
            var before = Hashes(fixtures);
            CheckFixtureSize(Path.Combine(fixtures, "basic.dbf"));
            CheckFixtureSize(Path.Combine(fixtures, "special.dbf"));
            Directory.CreateDirectory(working);
            foreach (string source in Directory.GetFiles(fixtures)) File.Copy(source, Path.Combine(working, Path.GetFileName(source)));

            // Harbour settings are shared within the process. Save and restore what these examples change.
            string oldAutoOpen = DbfHarbour.Eval("Set(45)");
            string oldDeleted = DbfHarbour.Eval("Set(11)");
            try
            {
                DbfHarbour.Eval("Set(45,.F.)");
                DbfHarbour.Eval("Set(11,.F.)");
                ReadBasic(working);
                TypedRead(working);
                TypedNestedPaths(working);
                TypedDeleted(working);
                TypedDeletionWrite(working);
                TypedInsert(working);
                TypedIndexEnumeration(working);
                TypedUpdate(working);
                TypedLockContention(working);
                TypedConcurrentWrites(working);
                TypedExpressionIndexMaintenance(working);
                VerifyTypedWithHarbour(working);
                WriteBasic(working);
                SpecialTypes(working);
            }
            finally
            {
                DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)");
                DbfHarbour.Eval(oldAutoOpen == "true" ? "Set(45,.T.)" : "Set(45,.F.)");
            }
            Require(before.SequenceEqual(Hashes(fixtures)), "Versioned fixture files changed");
            Console.WriteLine("PASS: all light examples; versioned fixtures unchanged");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
        finally { if (Directory.Exists(working)) Directory.Delete(working, true); }
    }

    private static void PathResolution()
    {
        string root = DbfPaths.Root(@"D:\SampleData");
        string table = DbfPaths.Table(root, @"catalog\..\sales\orders.dbf");
        Equal(@"D:\SampleData\sales\orders.dbf", table, "relative DBF path");
        Equal(@"D:\SampleData\sales\indexes\orders.cdx", DbfPaths.Index(table, @"indexes\orders.cdx"), "relative CDX path");
        Equal(@"D:\Archive\orders.dbf", DbfPaths.Table(root, @"D:\Archive\orders.dbf"), "absolute DBF path");
        Equal(@"D:\Shared\orders.cdx", DbfPaths.Index(table, @"D:\Shared\orders.cdx"), "absolute CDX path");
        Equal(@"\\server\share\orders.dbf", DbfPaths.Table(root, @"\\server\share\orders.dbf"), "UNC path");
        foreach (string invalid in new[] { @"D:orders.dbf", @"\orders.dbf", @"\\server" })
        {
            bool rejected = false;
            try { DbfPaths.Table(root, invalid); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "ambiguous path accepted: " + invalid);
        }
        Console.WriteLine("PASS: DBF and CDX path resolution");
    }

    private sealed class MappingModel
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string ReadOnly => Name;
    }

    private sealed class TypedModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? Birth { get; set; }
        public bool Active { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class TypedSpecial
    {
        public int Id { get; set; }
        public DateTime? Changed { get; set; }
        public long Version { get; set; }
        public DateTime? Stamp { get; set; }
        public string Name { get; set; }
    }

    private sealed class ComplexModel
    {
        private string name;
        public static string ObservedCodepage;
        public int Id { get; set; }
        public string Name { get { ObservedCodepage = DbfHarbour.Eval("hb_cdpSelect()"); return name; } set { name = value; } }
        public int View { get; set; }
        public DateTime? Birth { get; set; }
    }

    private sealed class NumericModel
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public decimal Rate { get; set; }
    }

    private sealed class NumericMapping : DbfMapping<NumericModel>
    {
        public NumericMapping()
        {
            Field(row => row.Id, "ID");
            Field(row => row.Amount, "AMOUNT");
            Field(row => row.Rate, "RATE");
        }
    }

    private sealed class WideModel { public long Number { get; set; } }
    private sealed class WideMapping : DbfMapping<WideModel>
    {
        public WideMapping() { Field(row => row.Number, "NUM"); }
    }
    private sealed class NarrowModel { public int Number { get; set; } }
    private sealed class NarrowMapping : DbfMapping<NarrowModel>
    {
        public NarrowMapping() { Field(row => row.Number, "NUM"); }
    }

    private sealed class NestedMapping : DbfMapping<TypedModel>
    {
        public DbfIndexMapping<TypedModel, int> ById { get; }
        public NestedMapping()
        {
            Field(row => row.Id, "ID");
            Field(row => row.Name, "NAME").TrimEnd();
            ById = Index(row => row.Id, @"indexes\basic_id.cdx", "ID");
            IndexBag(@"indexes\basic_name.cdx");
        }
    }

    private static void TypedNestedPaths(string dir)
    {
        string root = Path.Combine(dir, "nested");
        foreach (string subdir in new[] { "catalog", "sales" })
        {
            string tableDir = Path.Combine(root, subdir);
            string indexDir = Path.Combine(tableDir, "indexes");
            Directory.CreateDirectory(indexDir);
            foreach (string file in new[] { "basic.dbf", "basic.fpt" }) File.Copy(Path.Combine(dir, file), Path.Combine(tableDir, file));
            foreach (string file in new[] { "basic_id.cdx", "basic_name.cdx" }) File.Copy(Path.Combine(dir, file), Path.Combine(indexDir, file));
        }
        var mapping = new NestedMapping();
        string previous = Directory.GetCurrentDirectory();
        try
        {
            using (var session = DbfSession.Open(root))
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                using (var catalog = session.OpenTable(@"catalog\basic.dbf", mapping, "UA866", Cp866))
                using (var sales = session.OpenTable(Path.Combine(root, "sales", "basic.dbf"), mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
                {
                    var salesRecord = sales.Index(mapping.ById).FindFirst(1);
                    sales.Update(salesRecord.Reference, row => row.Name = "SALES");
                    Equal("ALICE", catalog.Index(mapping.ById).FindFirst(1).Value.Name, "catalog lookup");
                    Equal("SALES", sales.Index(mapping.ById).FindFirst(1).Value.Name, "sales lookup");
                }
            }
        }
        finally { Directory.SetCurrentDirectory(previous); }
        Console.WriteLine("PASS: nested paths, mapping reuse and current directory independence");
    }

    private sealed class ComplexMapping : DbfMapping<ComplexModel>
    {
        public DbfIndexMapping<ComplexModel, string> ByName { get; }
        public DbfIndexMapping<ComplexModel, ComplexKey> ByViewName { get; }
        public DbfIndexMapping<ComplexModel, ComplexKey> ByCategory { get; }
        public DbfIndexMapping<ComplexModel, DateTime?> ByDate { get; }
        public DbfIndexMapping<ComplexModel, DateViewKey> ByDateView { get; }
        public ComplexMapping()
        {
            Field(x => x.Id, "ID");
            Field(x => x.Name, "NAME").TrimEnd();
            Field(x => x.View, "VIEW");
            Field(x => x.Birth, "BIRTH");
            ByName = Index(x => x.Name, "complex.cdx", "NAME", DbfKeyFormat.AllTrim().ThenPadRight(20).Upper());
            ByViewName = CompositeIndex<ComplexKey>("complex.cdx", "VIEWNAME")
                .Part(key => key.View, row => row.View, DbfKeyFormat.Str(4))
                .Part(key => key.Name, row => row.Name, DbfKeyFormat.PadRight(30).Upper());
            ByCategory = Index<ComplexKey>("complex.cdx", "CATEGORY")
                .SearchKey("{|view, name| IIF(view = 1, 'A', 'B') + UPPER(PADR(name,30)) }", key => new object[] { key.View, key.Name });
            ByDate = Index(x => x.Birth, "complex.cdx", "DATEKEY", DbfKeyFormat.Dtos());
            ByDateView = CompositeIndex<DateViewKey>("complex.cdx", "DATEVIEW")
                .Part(key => key.Date, row => row.Birth, DbfKeyFormat.Dtos())
                .Part(key => key.View, row => row.View, DbfKeyFormat.Str(4));
        }
    }

    private sealed class ComplexKey
    {
        public int View { get; set; }
        public string Name { get; set; }
    }

    private sealed class DateViewKey
    {
        public DateTime? Date { get; set; }
        public int View { get; set; }
    }

    private sealed class ShortBlockMapping : DbfMapping<ComplexModel>
    {
        public DbfIndexMapping<ComplexModel, int> IndexKey { get; }
        public ShortBlockMapping()
        {
            Field(row => row.Id, "ID");
            IndexKey = Index<int>("complex.cdx", "CATEGORY").SearchKey("{|id| 'A' }", id => new object[] { id });
        }
    }

    private sealed class UnsupportedTagMapping : DbfMapping<ComplexModel>
    {
        public UnsupportedTagMapping(string tag)
        {
            Field(row => row.Id, "ID");
            Index(row => row.Id, "complex.cdx", tag);
        }
    }

    private sealed class TypedSpecialMapping : DbfMapping<TypedSpecial>
    {
        public TypedSpecialMapping()
        {
            Field(x => x.Id, "IDZ");
            Field(x => x.Changed, "TSZ");
            Field(x => x.Version, "VMZ");
            Field(x => x.Stamp, "STAMP");
            Field(x => x.Name, "NAME").TrimEnd();
            IndexBag("special_id.cdx");
            IndexBag("special_time.cdx");
        }
    }

    private sealed class PartialSpecialMapping : DbfMapping<TypedSpecial>
    {
        public PartialSpecialMapping()
        {
            Field(x => x.Stamp, "STAMP");
            Field(x => x.Name, "NAME");
            IndexBag("special_id.cdx");
            IndexBag("special_time.cdx");
        }
    }

    private sealed class TypedMapping : DbfMapping<TypedModel>
    {
        public DbfIndexMapping<TypedModel, int> ById { get; }
        public DbfIndexMapping<TypedModel, string> ByName { get; }
        public TypedMapping()
        {
            Field(x => x.Id, "ID");
            Field(x => x.Name, "NAME").TrimEnd();
            Field(x => x.Birth, "BIRTH");
            Field(x => x.Active, "ACTIVE");
            Field(x => x.Amount, "AMOUNT");
            ById = Index(x => x.Id, "basic_id.cdx", "ID");
            ByName = Index(x => x.Name, "basic_name.cdx", "NAME");
        }
    }

    private sealed class BadIndexMapping : DbfMapping<TypedModel>
    {
        public BadIndexMapping()
        {
            Field(x => x.Id, "ID");
            Index(x => x.Id, "basic_name.cdx", "NAME");
        }
    }

    private sealed class BagMapping : DbfMapping<TypedModel>
    {
        public BagMapping(string bag)
        {
            Field(x => x.Id, "ID");
            Index(x => x.Id, "basic_id.cdx", "ID");
            IndexBag(bag);
        }
    }

    private static void TypedRead(string dir)
    {
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(dir))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
        {
            var first = table.ReadAt(1);
            Equal(1, first.Value.Id, "typed id");
            Equal("ALICE", first.Value.Name, "typed name");
            Equal(12.50m, first.Value.Amount, "typed decimal");
            Equal(new DateTime(1990, 1, 2), first.Value.Birth.Value, "typed date");
            Require(table.ReadAt(4) == null, "missing typed record");
            Equal("BOB", table.Index(mapping.ById).FindFirst(2).Value.Name, "typed numeric seek");
            Equal(3, table.Index(mapping.ByName).FindFirst("CAROL").Value.Id, "typed text seek");
            Require(table.Index(mapping.ByName).FindFirst("CAR") == null, "prefix was accepted as exact key");
            Require(table.Index(mapping.ById).FindFirst(999) == null, "missing index key");
            bool rejected = false;
            try { table.Index(new TypedMapping().ById); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "foreign index descriptor accepted");
            rejected = false;
            try { session.OpenTable("basic.dbf", new BadIndexMapping(), "UA866", Cp866); }
            catch (DbfMappingException error) { rejected = error.TablePath == Path.Combine(dir, "basic.dbf") && error.Message.Contains("NAME"); }
            Require(rejected, "mismatched index expression accepted");
            using (session.OpenTable("basic.dbf", new BagMapping("basic_name.cdx"), "UA866", Cp866))
                Equal("2", DbfHarbour.Eval("OrdCount()"), "non-searchable index bag opened");
            rejected = false;
            try { session.OpenTable("basic.dbf", new BagMapping("missing.cdx"), "UA866", Cp866); }
            catch (FileNotFoundException) { rejected = true; }
            Require(rejected, "missing index bag accepted");
            Equal(1, table.Read(first.Reference).Value.Id, "table usable after failed open");
            var rows = table.Scan().Select(x => { Require(table.Read(first.Reference).Value.Id == 1, "nested read"); return x; }).ToList();
            Equal(3, rows.Count, "typed scan count");
            Require(!ReferenceEquals(rows[0].Value, first.Value), "typed values must be independent");
            using (var otherTable = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
            {
                var threads = Enumerable.Range(0, 4).Select(worker => System.Threading.Tasks.Task.Run(() =>
                {
                    var selected = worker % 2 == 0 ? table : otherTable;
                    for (int i = 0; i < 25; i++)
                        Require(selected.Index(mapping.ById).FindFirst(1).Value.Name == "ALICE" && selected.ReadAt(2).Value.Id == 2, "concurrent indexed read");
                })).ToArray();
                System.Threading.Tasks.Task.WaitAll(threads);
            }
        }
        bool otherThreadOpened = false;
        var otherThread = new System.Threading.Thread(() =>
        {
            using (DbfSession.Open(dir)) { otherThreadOpened = true; }
        });
        otherThread.Start(); otherThread.Join();
        Require(otherThreadOpened, "session could not be opened from another thread");
        var closingSession = DbfSession.Open(dir);
        var closingTable = closingSession.OpenTable("basic.dbf", mapping, "UA866", Cp866);
        var active = closingTable.Scan().GetEnumerator();
        Require(active.MoveNext(), "scan before session close");
        var materialized = active.Current;
        closingSession.Dispose();
        bool closed = false;
        try { active.MoveNext(); } catch (ObjectDisposedException) { closed = true; }
        finally { active.Dispose(); }
        Require(closed && materialized.Value.Id == 1, "session close did not invalidate cursor or retain data");
        closed = false;
        try { closingTable.Read(materialized.Reference); } catch (ObjectDisposedException) { closed = true; }
        Require(closed, "closed table accepted a reference");
        closingTable.Dispose();
        closingSession.Dispose();
        Console.WriteLine("PASS: typed read, scan and concurrent callers");
    }

    private static void TypedDeleted(string dir)
    {
        string copy = Path.Combine(dir, "typed-deleted");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        DbfHarbour.Use(Path.Combine(copy, "basic.dbf"), "DELETE_TEST", true, "UA866");
        try
        {
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_name.cdx"));
            DbfHarbour.GoTo(2);
            DbfHarbour.Delete();
            DbfHarbour.Commit();
        }
        finally { DbfHarbour.CloseArea("DELETE_TEST"); }
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
        using (var other = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
        using (var including = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, DbfDeletedRecords.Include))
        {
            Require(table.ReadAt(2) == null && table.Index(mapping.ById).FindFirst(2) == null, "deleted record visible");
            Equal(2, table.Scan().Count(), "deleted record in scan");
            var deleted = including.ReadAt(2);
            Require(deleted != null && deleted.IsDeleted && including.Read(deleted.Reference).IsDeleted, "deleted record not returned");
            Require(including.Index(mapping.ById).FindFirst(2).IsDeleted, "deleted indexed record not returned");
            Equal(3, including.Scan().Count(), "scan including deleted records");
            Require(table.Index(mapping.ById).FindFirst(2) == null, "deleted visibility leaked between tables");
            bool rejected = false;
            try { other.Read(table.ReadAt(1).Reference); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "foreign record reference accepted");
        }
        Console.WriteLine("PASS: deleted records and reference ownership");
    }

    private static void TypedDeletionWrite(string dir)
    {
        string copy = Path.Combine(dir, "typed-delete-write");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        {
            using (var readOnly = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
            {
                bool rejected = false;
                try { readOnly.MarkDeleted(readOnly.ReadAt(2).Reference); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "read-only delete accepted");
            }
            using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
            {
                var reference = table.ReadAt(2).Reference;
                table.MarkDeleted(reference);
                table.MarkDeleted(reference);
                Require(table.Read(reference) == null && table.Index(mapping.ById).FindFirst(2) == null, "delete not visible");
                table.Recall(reference);
                table.Recall(reference);
                Equal(2, table.Index(mapping.ById).FindFirst(2).Value.Id, "recalled index entry");
                table.MarkDeleted(reference);
            }
        }
        DbfHarbour.UseReadOnly(Path.Combine(copy, "basic.dbf"), "CHECK_DELETE", true, "UA866");
        try
        {
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_name.cdx"));
            DbfHarbour.GoTo(2);
            Require(DbfHarbour.IsRecordDeleted, "delete not persisted after reopening");
            DbfHarbour.Indexes.Select("NAME");
            Require(DbfHarbour.Indexes.Seek("BOB", encoding: Cp866), "index not maintained");
        }
        finally { DbfHarbour.CloseArea("CHECK_DELETE"); }
        Console.WriteLine("PASS: typed delete, recall and index reopening");
    }

    private static void TypedInsert(string dir)
    {
        string copy = Path.Combine(dir, "typed-insert");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var model = new TypedModel { Id = 9, Name = "НОВАЯ", Birth = new DateTime(2000, 1, 2), Active = true, Amount = 42.75m };
            Require(DbfHarbour.RecordLock(1), "could not arrange existing lock");
            bool existingLockRejected = false;
            try { table.Insert(model); } catch (InvalidOperationException) { existingLockRejected = true; }
            Require(existingLockRejected && DbfHarbour.Eval("DbRLockList()") == "[1]", "insert altered an existing lock");
            DbfHarbour.RecordLock(1, true);
            bool rejected = false;
            try { table.Insert(model, strict: true); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "strict insert with incomplete mapping");
            rejected = false;
            try { table.Insert(new TypedModel { Id = 9, Name = new string('A', 200) }); } catch (ArgumentException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "oversized insert changed table");
            rejected = false;
            try { table.Insert(new TypedModel { Id = 9, Name = "🙂" }); } catch (EncoderFallbackException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "unencodable insert changed table");
            rejected = false;
            try { table.Insert(new TypedModel { Id = 9, Name = "BAD", Amount = 1.001m }); } catch (ArgumentException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "decimal rounding accepted");
            rejected = false;
            try { table.Insert(new TypedModel { Id = 9, Name = "BAD", Amount = 10000000m }); } catch (ArgumentException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "decimal overflow accepted");
            var added = table.Insert(model);
            Equal(4L, added.RecordNumber, "insert record number");
            Equal("НОВАЯ", added.Value.Name, "insert value");
            Equal(42.75m, added.Value.Amount, "insert decimal");
            Require(!ReferenceEquals(model, added.Value), "insert returned input model");
            Equal(9, table.Index(mapping.ById).FindFirst(9).Value.Id, "insert numeric index");
            Equal(9, table.Index(mapping.ByName).FindFirst("НОВАЯ").Value.Id, "insert text index");
        }
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
        {
            Equal(4, table.Scan().Count(), "insert persisted");
            Equal("НОВАЯ", table.Index(mapping.ById).FindFirst(9).Value.Name, "reopened insert index");
            Equal(42.75m, table.ReadAt(4).Value.Amount, "reopened decimal");
        }
        foreach (string file in new[] { "special.dbf", "special_id.cdx", "special_time.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("special.dbf", new PartialSpecialMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            bool rejected = false;
            try { table.Insert(new TypedSpecial { Name = "FOUR" }, strict: true); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && table.Scan().Count() == 3, "strict insert accepted unmapped generated fields");
        }
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("special.dbf", new TypedSpecialMapping(), "UA866", Cp866, mode: DbfOpenMode.ExclusiveReadWrite))
        {
            long previousVersion = table.Scan().Max(row => row.Value.Version);
            var input = new TypedSpecial { Id = 999, Version = 999, Changed = new DateTime(2000, 1, 1), Name = "FOUR", Stamp = new DateTime(2020, 1, 2, 3, 4, 5) };
            var result = table.Insert(input, strict: true);
            Equal(previousVersion + 1, result.Value.Version, "exclusive insert version");
            Require(result.Value.Id != 999 && result.Value.Version != 999 && result.Value.Changed != input.Changed, "generated fields written from model");
            Equal(input.Stamp, result.Value.Stamp, "insert timestamp");
        }
        string sharedDir = Path.Combine(copy, "shared-version");
        Directory.CreateDirectory(sharedDir);
        foreach (string file in new[] { "special.dbf", "special_id.cdx", "special_time.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(sharedDir, file));
        using (var session = DbfSession.Open(sharedDir))
        using (var table = session.OpenTable("special.dbf", new TypedSpecialMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            long before = table.Scan().Max(row => row.Value.Version);
            var result = table.Insert(new TypedSpecial { Name = "FOUR", Stamp = new DateTime(2020, 1, 2, 3, 4, 5) }, strict: true);
            Equal(before + 2, result.Value.Version, "shared Harbour append version");
        }
        Console.WriteLine("PASS: typed insert validation and reopened indexes");
    }

    private static void TypedUpdate(string dir)
    {
        string copy = Path.Combine(dir, "typed-update");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx", "special.dbf", "special_id.cdx", "special_time.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var reference = table.ReadAt(1).Reference;
            int calls = 0;
            var updated = table.Update(reference, row => { calls++; row.Name = "ИЗМЕНЕНО"; });
            Equal(1, calls, "update callback count");
            Equal("ИЗМЕНЕНО", updated.Value.Name, "updated name");
            Equal(1, table.Index(mapping.ByName).FindFirst("ИЗМЕНЕНО").Value.Id, "updated index");
            bool rejected = false;
            try { table.Update(reference, row => { row.Name = "bad🙂"; }); } catch (EncoderFallbackException) { rejected = true; }
            Require(rejected && table.Read(reference).Value.Name == "ИЗМЕНЕНО", "invalid update changed row");
            rejected = false;
            try { table.Update(reference, row => { row.Name = "NO"; throw new ApplicationException("callback"); }); }
            catch (ApplicationException) { rejected = true; }
            Require(rejected && table.Read(reference).Value.Name == "ИЗМЕНЕНО", "failed callback changed row");
            rejected = false;
            try { table.Update(reference, row => table.ReadAt(1)); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && table.Read(reference).Value.Name == "ИЗМЕНЕНО", "callback reentry accepted");
            rejected = false;
            try { table.Update(reference, row => DbfHarbour.SelectArea("UNRELATED")); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && table.Read(reference).Value.Name == "ИЗМЕНЕНО", "low-level callback reentry accepted");
            rejected = false;
            Action<TypedModel> asyncUpdate = async row => { await System.Threading.Tasks.Task.Yield(); row.Name = "LATE"; };
            try { table.Update(reference, asyncUpdate); } catch (ArgumentException) { rejected = true; }
            Require(rejected && table.Read(reference).Value.Name == "ИЗМЕНЕНО", "asynchronous update callback accepted");
            table.MarkDeleted(reference);
            rejected = false;
            try { table.Update(reference, row => row.Name = "NO"); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "deleted row updated");
        }
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("special.dbf", new TypedSpecialMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var before = table.ReadAt(1);
            var unchanged = table.Update(before.Reference, row => { row.Id = 999; row.Version = 999; });
            Equal(before.Value.Version, unchanged.Value.Version, "no-op advanced generated version");
            Equal(before.Value.Id, unchanged.Value.Id, "generated id changed");
            var changed = table.Update(before.Reference, row => row.Name = "UPDATED");
            Require(changed.Value.Version > before.Value.Version, "update did not advance generated version");
        }
        DbfHarbour.UseReadOnly(Path.Combine(copy, "basic.dbf"), "CHECK_UPDATE", true, "UA866");
        try
        {
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(copy, "basic_name.cdx"));
            DbfHarbour.Indexes.Select("NAME");
            Require(DbfHarbour.Indexes.Seek("ИЗМЕНЕНО", encoding: Cp866), "updated index missing after reopen");
            Equal(12.50m, Convert.ToDecimal(DbfHarbour.GetValues(new[] { "AMOUNT" }, Cp866).Single(), CultureInfo.InvariantCulture), "unmapped field changed");
        }
        finally { DbfHarbour.CloseArea("CHECK_UPDATE"); }
        Console.WriteLine("PASS: typed update, validation, no-op and reopened indexes");
    }

    private static void TypedIndexEnumeration(string dir)
    {
        string copy = Path.Combine(dir, "typed-index-scan");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var second = table.Insert(new TypedModel { Id = 1, Name = "ALICE" });
            var third = table.Insert(new TypedModel { Id = 1, Name = "ALICE" });
            table.Insert(new TypedModel { Id = 10, Name = "ALICE" });
            var index = table.Index(mapping.ById);
            var names = table.Index(mapping.ByName);
            Equal(3, index.EqualTo(1).Count(), "duplicate integer keys");
            Equal(4, names.EqualTo("ALICE").Count(), "duplicate string keys");
            Require(index.Scan().Select(x => x.Value.Id).SequenceEqual(new[] { 1, 1, 1, 2, 3, 10 }), "index scan order");
            Equal(5, index.Range(1, 3, DbfRangeBounds.IncludeBoth).Count(), "inclusive range");
            Equal(4, index.Range(1, 3, DbfRangeBounds.IncludeStart).Count(), "range excludes end");
            Equal(2, index.Range(1, 3, DbfRangeBounds.IncludeEnd).Count(), "range excludes start");
            Equal(1, index.Range(1, 3, DbfRangeBounds.ExcludeBoth).Count(), "exclusive range");
            Equal(5, index.Range(0, 4, DbfRangeBounds.IncludeBoth).Count(), "missing range boundaries");
            Equal(0, index.Range(4, 9, DbfRangeBounds.IncludeBoth).Count(), "empty range");
            Equal(6, index.Range(0, 99, DbfRangeBounds.IncludeBoth).Count(), "range outside index");
            Equal(0, index.Range(11, 99, DbfRangeBounds.IncludeBoth).Count(), "range after index");
            Equal(0, index.Range(3, 1, DbfRangeBounds.IncludeBoth).Count(), "reversed range");
            Equal(0, names.EqualTo("ALI").Count(), "prefix enumeration");
            using (var a = index.EqualTo(1).GetEnumerator())
            using (var b = index.EqualTo(1).GetEnumerator())
            {
                Require(a.MoveNext() && b.MoveNext(), "first independent cursors");
                Equal(a.Current.RecordNumber, b.Current.RecordNumber, "cursor start");
                Require(a.MoveNext(), "first cursor second step");
                Require(names.FindFirst("BOB") != null && b.MoveNext(), "nested search changed cursor");
                Equal(a.Current.RecordNumber, b.Current.RecordNumber, "cursor position");
            }
            table.MarkDeleted(second.Reference);
            Equal(2, index.EqualTo(1).Count(), "deleted duplicate excluded");
            table.Recall(second.Reference);
            table.MarkDeleted(third.Reference);
            Equal(2, index.EqualTo(1).Count(), "last duplicate excluded");
            Equal(5, index.Scan().Count(), "deleted record in index scan");
            Equal(4, index.Range(1, 3, DbfRangeBounds.IncludeBoth).Count(), "deleted record in range");
            Equal(second.RecordNumber, index.FindLast(1).RecordNumber, "last visible duplicate");
            using (var all = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, DbfDeletedRecords.Include))
                Equal(3, all.Index(mapping.ById).EqualTo(1).Count(), "deleted duplicate included");
            var enumerator = index.EqualTo(1).GetEnumerator();
            Require(enumerator.MoveNext(), "cursor before close");
            table.Dispose();
            bool closedRejected = false;
            try { enumerator.MoveNext(); } catch (ObjectDisposedException) { closedRejected = true; }
            finally { enumerator.Dispose(); }
            Require(closedRejected, "enumeration continued after table close");
        }
        Console.WriteLine("PASS: duplicate index enumeration and nested searches");
    }

    private static int HoldLock(string path)
    {
        try
        {
            DbfHarbour.Use(path, "LOCK_CHILD", false, "UA866");
            try
            {
                if (!DbfHarbour.RecordLock(1)) return 2;
                Console.WriteLine("READY");
                Console.Out.Flush();
                Console.ReadLine();
                DbfHarbour.RecordLock(1, true);
            }
            finally { DbfHarbour.CloseArea("LOCK_CHILD"); }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static int ConcurrentUpdate(string dir, int iterations)
    {
        try
        {
            using (var session = DbfSession.Open(dir))
            using (var table = session.OpenTable("basic.dbf", new TypedMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
            {
                var reference = table.ReadAt(1).Reference;
                for (int i = 0; i < iterations; i++)
                {
                    int attempts = 0;
                    while (true)
                    {
                        try { table.Update(reference, row => row.Id++); break; }
                        catch (DbfLockException)
                        {
                            if (++attempts > 10000) throw;
                            System.Threading.Thread.Sleep(1);
                        }
                    }
                }
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void TypedConcurrentWrites(string dir)
    {
        string copy = Path.Combine(dir, "concurrent");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var children = new List<System.Diagnostics.Process>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                    Arguments = "--concurrent-update \"" + copy + "\" 25",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Require(child != null, "concurrent update helper did not start");
                children.Add(child);
            }
            foreach (var child in children)
                Require(child.WaitForExit(30000) && child.ExitCode == 0, "concurrent update helper failed");
            var mapping = new TypedMapping();
            using (var session = DbfSession.Open(copy))
            using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866))
            {
                Equal(51, table.ReadAt(1).Value.Id, "concurrent increments");
                Equal(1L, table.Index(mapping.ById).FindFirst(51).RecordNumber, "concurrent index maintenance");
            }
        }
        finally
        {
            foreach (var child in children)
            {
                if (!child.HasExited) { child.Kill(); child.WaitForExit(); }
                child.Dispose();
            }
        }
        Console.WriteLine("PASS: concurrent updates in separate processes");
    }

    private static void VerifyTypedWithHarbour(string dir)
    {
        string verifier = Environment.GetEnvironmentVariable("HARBOUR_TYPED_VERIFIER");
        if (string.IsNullOrEmpty(verifier)) return;
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = verifier,
            Arguments = "\"" + dir + "\"",
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            Require(process != null, "independent Harbour verifier did not start");
            if (!process.WaitForExit(10000)) { process.Kill(); process.WaitForExit(); throw new Exception("Independent Harbour verifier timed out"); }
            Require(process.ExitCode == 0, "independent Harbour verifier failed with exit code " + process.ExitCode);
        }
        Console.WriteLine("PASS: independent Harbour verifier");
    }

    private static void TypedExpressionIndexMaintenance(string dir)
    {
        string generator = Environment.GetEnvironmentVariable("HARBOUR_COMPLEX_GENERATOR");
        if (string.IsNullOrEmpty(generator)) return;
        string copy = Path.Combine(dir, "typed-complex");
        Directory.CreateDirectory(copy);
        RunComplexHelper(generator, copy, "");
        string originalCodepage = DbfHarbour.Eval("hb_cdpSelect()");
        var mapping = new ComplexMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("complex.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            foreach (string tag in new[] { "BADFOR", "BADUNIQUE", "BADDESC" })
            {
                bool unsupported = false;
                try { session.OpenTable("complex.dbf", new UnsupportedTagMapping(tag), "UA866", Cp866); }
                catch (InvalidOperationException) { unsupported = true; }
                Require(unsupported, "unsupported tag accepted: " + tag);
            }
            Equal(3, table.Index(mapping.ByName).EqualTo("  алиса ").Count(), "formatted name search");
            Equal(2, table.Index(mapping.ByDate).EqualTo(new DateTime(2000, 1, 2)).Count(), "DTOS date search");
            var dateView = table.Index(mapping.ByDateView);
            Equal(2, dateView.EqualTo(new DateViewKey { Date = new DateTime(2000, 1, 2), View = 1 }).Count(), "DTOS composite key");
            Equal(2, dateView.Prefix(key => key.Date, new DateTime(2000, 1, 2)).Scan().Count(), "DTOS leading prefix");
            bool badDate = false;
            try { table.Index(mapping.ByDate).FindFirst(new DateTime(2000, 1, 2, 12, 0, 0)); } catch (ArgumentException) { badDate = true; }
            Require(badDate, "date key silently discarded time");
            Equal(1, table.Index(mapping.ByName).FindFirst("алиса").Value.Id, "formatted first key");
            var complex = table.Index(mapping.ByViewName);
            var key = new ComplexKey { View = 1, Name = "алиса" };
            Equal(2, complex.EqualTo(key).Count(), "composite duplicate keys");
            Equal(1, complex.FindFirst(key).Value.Id, "composite first key");
            Equal(2, complex.FindLast(key).Value.Id, "composite last key");
            Require(complex.FindFirst(new ComplexKey { View = 3, Name = "алиса" }) == null, "missing composite key");
            Equal(3, complex.Range(key, new ComplexKey { View = 1, Name = "борис" }, DbfRangeBounds.IncludeBoth).Count(), "composite full-key range");
            var viewOne = complex.Prefix(k => k.View, 1);
            Equal(3, viewOne.Scan().Count(), "leading part prefix");
            Equal(2, viewOne.StartingWith(k => k.Name, "али").Count(), "text prefix");
            Equal(3, viewOne.StartingWith(k => k.Name, "").Count(), "empty text prefix");
            Equal(2, viewOne.Prefix(k => k.Name, "алиса").Scan().Count(), "full composite prefix");
            bool nonLeading = false;
            try { complex.Prefix(k => k.Name, "алиса"); } catch (ArgumentException) { nonLeading = true; }
            Require(nonLeading, "non-leading prefix accepted");
            bool widePrefix = false;
            try { viewOne.StartingWith(k => k.Name, new string('A', 31)).ToList(); } catch (ArgumentException) { widePrefix = true; }
            Require(widePrefix, "oversized text prefix accepted");
            Require(mapping.ByViewName.HarbourExpression == "STR(VIEW,4,0)+UPPER(PADR(NAME,30))", "composite expression");
            var arbitrary = table.Index(mapping.ByCategory);
            bool unsupportedPrefix = false;
            try { arbitrary.Prefix(k => k.View, 1); } catch (NotSupportedException) { unsupportedPrefix = true; }
            Require(unsupportedPrefix, "SearchKey prefix accepted");
            bool unsupportedRange = false;
            try { arbitrary.Range(key, key, DbfRangeBounds.IncludeBoth); } catch (NotSupportedException) { unsupportedRange = true; }
            Require(unsupportedRange, "SearchKey range accepted");
            Equal(2, arbitrary.EqualTo(key).Count(), "SearchKey duplicate keys");
            Equal(1, arbitrary.FindFirst(key).Value.Id, "SearchKey first key");
            Equal(4, arbitrary.FindFirst(new ComplexKey { View = 2, Name = "алиса" }).Value.Id, "SearchKey category");
            Require(arbitrary.FindFirst(new ComplexKey { View = 1, Name = "x' + 'y" }) == null, "SearchKey argument was not passed as data");
            bool blockEncoding = false;
            try { arbitrary.FindFirst(new ComplexKey { View = 1, Name = "🙂" }); } catch (EncoderFallbackException) { blockEncoding = true; }
            Require(blockEncoding, "unrepresentable SearchKey argument accepted");
            var shortMapping = new ShortBlockMapping();
            using (var shortTable = session.OpenTable("complex.dbf", shortMapping, "UA866", Cp866))
            {
                bool shortKey = false;
                try { shortTable.Index(shortMapping.IndexKey).FindFirst(1); } catch (ArgumentException) { shortKey = true; }
                Require(shortKey, "short SearchKey result accepted as exact key");
            }
            bool overflowKey = false;
            try { complex.FindFirst(new ComplexKey { View = 10000, Name = "алиса" }); } catch (InvalidOperationException) { overflowKey = true; }
            Require(overflowKey, "composite STR overflow accepted");
            bool encodingKey = false;
            try { complex.FindFirst(new ComplexKey { View = 1, Name = "🙂" }); } catch (EncoderFallbackException) { encodingKey = true; }
            Require(encodingKey, "unrepresentable composite key accepted");
            using (var first = complex.EqualTo(key).GetEnumerator())
            using (var second = complex.EqualTo(key).GetEnumerator())
            {
                Require(first.MoveNext() && second.MoveNext(), "composite cursor start");
                Require(table.Index(mapping.ByName).FindFirst("борис") != null && first.MoveNext() && second.MoveNext(), "composite nested search");
                Equal(first.Current.RecordNumber, second.Current.RecordNumber, "composite cursor independence");
            }
            using (var cursor = complex.EqualTo(key).GetEnumerator())
            {
                Require(cursor.MoveNext(), "mutable key cursor start");
                key.View = 2;
                Require(cursor.MoveNext() && cursor.Current.Value.View == 1, "enumeration key changed mid-scan");
            }
            key.View = 1;
            bool badKey = false;
            try { table.Index(mapping.ByName).FindFirst(new string('X', 21)); } catch (InvalidOperationException) { badKey = true; }
            Require(badKey, "formatted key truncation accepted");
            table.Insert(new ComplexModel { Id = 5, Name = "алиса", View = 3 }, strict: true);
            Equal("\"UA866\"", ComplexModel.ObservedCodepage, "codepage during typed operation");
            table.Update(table.ReadAt(1).Reference, row => row.Name = "борис");
            bool rejected = false;
            try { table.Insert(new ComplexModel { Id = 6, Name = "🙂", View = 1 }); } catch (EncoderFallbackException) { rejected = true; }
            Require(rejected, "invalid expression index input accepted");
            Equal(originalCodepage, DbfHarbour.Eval("hb_cdpSelect()"), "VM codepage restored after failure");
            rejected = false;
            try { session.OpenTable("complex.dbf", new ComplexMapping(), "UNKNOWN_CDP", Cp866); }
            catch (DbfHarbourOperationException error) { rejected = error.Operation == "OpenTable" && error.InnerException is HarbourException; }
            Require(rejected, "unknown VM codepage accepted");
            Equal(originalCodepage, DbfHarbour.Eval("hb_cdpSelect()"), "VM codepage restored after invalid codepage");
        }
        using (var session = DbfSession.Open(copy))
        using (var numeric = session.OpenTable("numeric.dbf", new NumericMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var first = numeric.ReadAt(1);
            var second = numeric.ReadAt(2);
            Equal(999999999999.99m, first.Value.Amount, "decimal positive boundary");
            Equal(999999999.99999m, first.Value.Rate, "decimal positive precision");
            Equal(-99999999999.99m, second.Value.Amount, "decimal negative boundary");
            Equal(-99999999.99999m, second.Value.Rate, "decimal negative precision");
            Equal(0.00001m, numeric.ReadAt(3).Value.Rate, "decimal smallest unit");
            numeric.Update(first.Reference, row => { row.Amount = 999999999999.98m; row.Rate = 999999999.99998m; });
            numeric.Update(second.Reference, row => { row.Amount = -99999999999.98m; row.Rate = -99999999.99998m; });
            numeric.Insert(new NumericModel { Id = 4, Amount = 0.01m, Rate = 0.00001m }, strict: true);
        }
        using (var session = DbfSession.Open(copy))
        using (var wide = session.OpenTable("wide.dbf", new WideMapping(), "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        {
            var first = wide.ReadAt(1);
            Equal(9007199254740993L, first.Value.Number, "wide integer round trip");
            bool rejected = false;
            try { wide.Update(first.Reference, row => row.Number = long.MaxValue); } catch (ArgumentException) { rejected = true; }
            Require(rejected && wide.Read(first.Reference).Value.Number == 9007199254740993L, "unsafe integer write modified record");
            Equal(9007199254740991L, wide.Update(first.Reference, row => row.Number = 9007199254740991L).Value.Number, "safe integer boundary");
        }
        using (var session = DbfSession.Open(copy))
        using (var narrow = session.OpenTable("wide.dbf", new NarrowMapping(), "UA866", Cp866))
        {
            bool contextual = false;
            try { narrow.ReadAt(1); }
            catch (DbfValueException error)
            {
                contextual = error.Field == "NUM" && error.RecordNumber == 1 && error.TablePath == Path.Combine(copy, "wide.dbf") && error.InnerException is OverflowException;
            }
            Require(contextual, "conversion error lost its field or cause");
        }
        Equal(originalCodepage, DbfHarbour.Eval("hb_cdpSelect()"), "VM codepage restored");
        RunComplexHelper(generator, copy, "verify");
        Console.WriteLine("PASS: expression index maintenance with table codepage");
    }

    private static void RunComplexHelper(string helper, string dir, string mode)
    {
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = helper,
            Arguments = "\"" + dir + "\" " + mode,
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            Require(process != null, "complex fixture helper did not start");
            if (!process.WaitForExit(10000)) { process.Kill(); process.WaitForExit(); throw new Exception("Complex fixture helper timed out"); }
            Require(process.ExitCode == 0, "complex fixture helper failed with exit code " + process.ExitCode);
        }
    }

    private static void TypedLockContention(string dir)
    {
        string copy = Path.Combine(dir, "typed-lock");
        Directory.CreateDirectory(copy);
        foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
            File.Copy(Path.Combine(dir, file), Path.Combine(copy, file));
        var mapping = new TypedMapping();
        using (var session = DbfSession.Open(copy))
        using (var table = session.OpenTable("basic.dbf", mapping, "UA866", Cp866, mode: DbfOpenMode.SharedReadWrite))
        using (var child = new System.Diagnostics.Process())
        {
            var reference = table.ReadAt(1).Reference;
            child.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                Arguments = "--hold-lock \"" + Path.Combine(copy, "basic.dbf") + "\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            Require(child.Start(), "lock helper did not start");
            try
            {
                var ready = child.StandardOutput.ReadLineAsync();
                Require(ready.Wait(10000) && ready.Result == "READY", "lock helper did not acquire record lock");
                int callbacks = 0;
                bool rejected = false;
                try { table.Update(reference, row => callbacks++); }
                catch (DbfLockException error)
                {
                    rejected = error.RecordNumber == 1 && error.Operation == "Update" && error.TablePath == Path.Combine(copy, "basic.dbf");
                }
                Require(rejected && callbacks == 0, "update did not report lock contention before callback");
                rejected = false;
                try { table.MarkDeleted(reference); } catch (DbfLockException) { rejected = true; }
                Require(rejected && table.Read(reference) != null, "delete did not report lock contention");
                child.StandardInput.WriteLine("release");
                Require(child.WaitForExit(10000) && child.ExitCode == 0, "lock helper did not release record lock");
                table.Update(reference, row => row.Name = "AFTER LOCK");
            }
            finally
            {
                if (!child.HasExited) { child.Kill(); child.WaitForExit(); }
            }
        }
        Console.WriteLine("PASS: lock contention across processes");
    }

    private sealed class MappingProbe : DbfMapping<MappingModel>
    {
        public void AddName() => Field(x => x.Name, "NAME").TrimEnd();
        public void AddDuplicate() => Field(x => x.Id, "name");
        public void AddReadOnly() => Field(x => x.ReadOnly, "OTHER");
        public void TrimNumber() => Field(x => x.Id, "ID").TrimEnd();
    }

    private static void MappingDeclarations()
    {
        var mapping = new MappingProbe();
        mapping.AddName();
        bool duplicate = false, readOnly = false, badTrim = false;
        try { mapping.AddDuplicate(); } catch (ArgumentException) { duplicate = true; }
        try { mapping.AddReadOnly(); } catch (ArgumentException) { readOnly = true; }
        try { mapping.TrimNumber(); } catch (InvalidOperationException) { badTrim = true; }
        Require(duplicate && readOnly && badTrim, "invalid field mapping was accepted");
        Console.WriteLine("PASS: field mapping declarations");
    }

    private static void ExactIntegerConversion()
    {
        var convert = typeof(DbfTable<TypedModel>).GetMethod("ConvertValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Func<object, Type, object> read = (value, type) => convert.Invoke(null, new[] { value, type, "N" });
        Equal(42, (int)read(42L, typeof(int)), "integer conversion");
        Equal(42L, (long)read(42.0, typeof(long)), "exact JSON double conversion");
        foreach (object invalid in new object[] { null, 1.5, 9007199254740992d, double.NaN })
        {
            bool rejected = false;
            try { read(invalid, typeof(long)); }
            catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, "inexact integer conversion was accepted");
        }
        bool overflow = false;
        try { read((long)int.MaxValue + 1, typeof(int)); }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is OverflowException) { overflow = true; }
        Require(overflow, "integer overflow was accepted");
        Console.WriteLine("PASS: exact integer conversion");
    }

    private static void ExecutorQueue()
    {
        int initThread = 0, shutdownThread = 0;
        using (var started = new System.Threading.ManualResetEventSlim())
        using (var release = new System.Threading.ManualResetEventSlim())
        {
            var executor = new DbfExecutor(1,
                () => initThread = System.Threading.Thread.CurrentThread.ManagedThreadId,
                () => shutdownThread = System.Threading.Thread.CurrentThread.ManagedThreadId);
            var first = executor.InvokeAsync(() => { started.Set(); release.Wait(); return System.Threading.Thread.CurrentThread.ManagedThreadId; }, System.Threading.CancellationToken.None);
            Require(started.Wait(10000), "executor did not start first operation");
            var second = executor.InvokeAsync(() => 2, System.Threading.CancellationToken.None);
            using (var cancel = new System.Threading.CancellationTokenSource())
            {
                var waiting = executor.InvokeAsync(() => 3, cancel.Token);
                Require(!waiting.IsCompleted, "full queue did not wait");
                cancel.Cancel();
                bool cancelled = false;
                try { waiting.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled, "queue wait ignored cancellation");
            }
            var shutdownWaiter = executor.InvokeAsync(() => 4, System.Threading.CancellationToken.None);
            var disposing = System.Threading.Tasks.Task.Run(() => executor.Dispose());
            bool closed = false;
            try { shutdownWaiter.GetAwaiter().GetResult(); } catch (ObjectDisposedException) { closed = true; }
            Require(closed, "shutdown did not release queue waiter");
            release.Set();
            Equal(initThread, first.GetAwaiter().GetResult(), "executor worker thread");
            Equal(2, second.GetAwaiter().GetResult(), "queued work on shutdown");
            Require(disposing.Wait(10000), "executor shutdown timed out");
            Equal(initThread, shutdownThread, "executor shutdown thread");
            bool disposed = false;
            try { executor.Invoke(() => 1); } catch (ObjectDisposedException) { disposed = true; }
            Require(disposed, "disposed executor accepted work");
        }
        DbfExecutor reentrant = null;
        using (reentrant = new DbfExecutor(1, null, null))
        {
            bool rejected = reentrant.Invoke(() =>
            {
                try { reentrant.InvokeAsync(() => 1, System.Threading.CancellationToken.None).GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { return true; }
                return false;
            });
            Require(rejected, "executor accepted reentrant queue operation");
            bool propagated = false;
            try { reentrant.Invoke<int>(() => throw new ApplicationException("work")); } catch (ApplicationException) { propagated = true; }
            Require(propagated && reentrant.Invoke(() => 7) == 7, "executor did not survive operation failure");
        }
        bool initFailed = false, shutdownFailed = false;
        try { new DbfExecutor(1, () => throw new ApplicationException("init"), null); } catch (ApplicationException) { initFailed = true; }
        var failingShutdown = new DbfExecutor(1, null, () => throw new ApplicationException("shutdown"));
        try { failingShutdown.Dispose(); } catch (ApplicationException) { shutdownFailed = true; }
        Require(initFailed && shutdownFailed, "executor lifecycle failure was lost");
        Console.WriteLine("PASS: bounded executor, cancellation and shutdown");
    }

    [System.Runtime.InteropServices.DllImport("hbdbf.dll", EntryPoint = "_DBF_VM_INIT_THREAD", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern uint VmInitThread();
    [System.Runtime.InteropServices.DllImport("hbdbf.dll", EntryPoint = "_DBF_VM_QUIT_THREAD", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern uint VmQuitThread();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static int RuntimeLifecycle()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "harbour-lifecycle-" + Guid.NewGuid().ToString("N"));
        try
        {
            Require(VmInitThread() == 0, "VM initialized in DLL loader");
            DbfRuntime.Configure(1);
            bool invalidAsync = false;
            try { _ = DbfRuntime.ExecuteAsync(async () => { await System.Threading.Tasks.Task.Yield(); }); }
            catch (ArgumentException) { invalidAsync = true; }
            Require(invalidAsync, "asynchronous executor delegate accepted");
            using (var started = new System.Threading.ManualResetEventSlim())
            using (var release = new System.Threading.ManualResetEventSlim())
            {
                var first = DbfRuntime.ExecuteAsync(() => { started.Set(); release.Wait(); return GetCurrentThreadId(); });
                Require(started.Wait(10000), "runtime operation did not start");
                var second = DbfRuntime.ExecuteAsync(() => DbfHarbour.Eval("1"));
                using (var cancel = new System.Threading.CancellationTokenSource())
                {
                    var waiter = DbfRuntime.ExecuteAsync(() => 3, cancel.Token);
                    Require(!waiter.IsCompleted, "runtime queue was not bounded");
                    cancel.Cancel();
                    bool cancelled = false;
                    try { waiter.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                    Require(cancelled, "runtime queue wait did not cancel");
                }
                release.Set();
                uint worker = first.GetAwaiter().GetResult();
                Equal("1", second.GetAwaiter().GetResult(), "native operation on executor");
                Equal(worker, VmInitThread(), "VM initialized on executor thread");
                bool rejected = DbfRuntime.ExecuteAsync(() =>
                {
                    try { DbfRuntime.ExecuteAsync(() => 1).GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { return true; }
                    return false;
                }).GetAwaiter().GetResult();
                Require(rejected, "reentrant runtime submission accepted");
                Directory.CreateDirectory(temporary);
                string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");
                foreach (string file in new[] { "basic.dbf", "basic.fpt", "basic_id.cdx", "basic_name.cdx" })
                    File.Copy(Path.Combine(source, file), Path.Combine(temporary, file));
                var leaked = DbfSession.Open(temporary);
                leaked.OpenTable("basic.dbf", new TypedMapping(), "UA866", Cp866);
                using (var holding = new System.Threading.ManualResetEventSlim())
                using (var finish = new System.Threading.ManualResetEventSlim())
                {
                    var running = DbfRuntime.ExecuteAsync(() => { holding.Set(); finish.Wait(); return DbfHarbour.Eval("2"); });
                    Require(holding.Wait(10000), "runtime shutdown test did not start");
                    var accepted = DbfRuntime.ExecuteAsync(() => DbfHarbour.Eval("3"));
                    var waiting = DbfRuntime.ExecuteAsync(() => 4);
                    var stopping = System.Threading.Tasks.Task.Run(() => DbfRuntime.Shutdown());
                    bool closed = false;
                    try { waiting.GetAwaiter().GetResult(); } catch (ObjectDisposedException) { closed = true; }
                    Require(closed, "runtime shutdown did not reject waiting submission");
                    finish.Set();
                    Equal("2", running.GetAwaiter().GetResult(), "running operation during shutdown");
                    Equal("3", accepted.GetAwaiter().GetResult(), "accepted operation during shutdown");
                    Require(stopping.Wait(10000), "runtime shutdown timed out");
                }
                Equal(worker, VmQuitThread(), "VM shut down on executor thread");
                using (File.Open(Path.Combine(temporary, "basic.dbf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                rejected = false;
                try { DbfHarbour.Eval("1"); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "runtime restarted after shutdown");
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static void RuntimeLifecycleProcess()
    {
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
            Arguments = "--runtime-lifecycle",
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            Require(process != null, "runtime lifecycle helper did not start");
            if (!process.WaitForExit(15000)) { process.Kill(); process.WaitForExit(); throw new Exception("Runtime lifecycle helper timed out"); }
            Require(process.ExitCode == 0, "runtime lifecycle helper failed");
        }
        Console.WriteLine("PASS: VM lifecycle and bounded runtime on executor thread");
    }

    private static void ReadBasic(string dir)
    {
        // Open read-only and explicitly load two independent CDX files.
        DbfHarbour.UseReadOnly(Path.Combine(dir, "basic.dbf"), "BASIC", true, "UA866");
        try
        {
            Equal(3, DbfHarbour.TotalRecords, "basic record count");
            DbfHarbour.GoTo(1);
            object[] row = DbfHarbour.GetValues(new[] { "ID", "NAME", "BIRTH", "ACTIVE", "AMOUNT", "NOTE" }, Cp866).ToArray();
            Equal(1L, row[0], "ID"); Equal("ALICE", ((string)row[1]).TrimEnd(), "NAME");
            Equal("19900102", row[2], "date"); Equal(true, row[3], "logical");
            Equal(12.50m, Convert.ToDecimal(row[4], CultureInfo.InvariantCulture), "amount"); Equal("First memo", row[5], "memo");
            DbfHarbour.Indexes.Load(Path.Combine(dir, "basic_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(dir, "basic_name.cdx"));
            DbfHarbour.Indexes.Select("ID");
            Require(DbfHarbour.Indexes.Seek(2) && DbfHarbour.ActiveRecord == 2, "numeric seek");
            DbfHarbour.Indexes.Select("NAME");
            Require(DbfHarbour.Indexes.Seek("CAROL", encoding: Cp866) && DbfHarbour.ActiveRecord == 3, "text seek");
            Require(!DbfHarbour.Indexes.Seek("MISSING", encoding: Cp866), "missing key");
            DbfHarbour.GoTo(1);
            bool rejected = false;
            try { DbfHarbour.SetValues(new Dictionary<string, object> { { "NAME", "WRONG" } }, Cp866); }
            catch (HarbourException) { rejected = true; }
            Require(rejected, "read-only update was accepted");
        }
        finally { DbfHarbour.CloseArea("BASIC"); }
        Console.WriteLine("PASS: basic fields, FPT, read-only and two CDX files");
    }

    private static void WriteBasic(string dir)
    {
        // Beta uses IDisposable and formats DateTime using the field's DBF type.
        using (var db = new Database(Path.Combine(dir, "basic.dbf"), exclusive: true, codepage: "UA866") { DatabaseEncoding = Cp866 })
        {
            db.RegisterIndex("ID", Path.Combine(dir, "basic_id.cdx"));
            db.RegisterIndex("NAME", Path.Combine(dir, "basic_name.cdx"));
            using (db.MakeLock(2))
            {
                db.SetValues(2, new Dictionary<string, object> {
                    { "NAME", "Ёлка" }, { "BIRTH", new DateTime(2001, 2, 3) },
                    { "ACTIVE", true }, { "AMOUNT", 42.75m }, { "NOTE", "Привет, memo!" }
                });
                DbfHarbour.Commit();
            }
            Equal("Ёлка", db.GetValue<string>(2, "NAME"), "updated text");
            DbfHarbour.Append();
            int added = DbfHarbour.ActiveRecord;
            Equal(4, added, "appended record");
            db.SetValues(added, new Dictionary<string, object> {
                { "ID", 4 }, { "NAME", "DAVE" }, { "BIRTH", null },
                { "ACTIVE", true }, { "AMOUNT", 9.50m }, { "NOTE", "Added memo" }
            });
            DbfHarbour.Commit();
            Equal(4, db.Search("ID", 4), "Beta ID search");
            DbfHarbour.GoTo(3);
            using (db.MakeLock(3)) { DbfHarbour.Delete(); DbfHarbour.Commit(); }
            DbfHarbour.GoTo(3); Require(DbfHarbour.IsRecordDeleted, "delete marker");
            using (db.MakeLock(3)) { DbfHarbour.Recall(); DbfHarbour.Commit(); }
            DbfHarbour.GoTo(3); Require(!DbfHarbour.IsRecordDeleted, "recall marker");
        }
        DbfHarbour.UseReadOnly(Path.Combine(dir, "basic.dbf"), "VERIFY", true, "UA866");
        try
        {
            DbfHarbour.Indexes.Load(Path.Combine(dir, "basic_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(dir, "basic_name.cdx"));
            DbfHarbour.Indexes.Select("NAME");
            Require(DbfHarbour.Indexes.Seek("Ёлка", encoding: Cp866) && DbfHarbour.ActiveRecord == 2, "updated name index");
            object[] row = DbfHarbour.GetValues(new[] { "BIRTH", "ACTIVE", "AMOUNT", "NOTE" }, Cp866).ToArray();
            Equal("20010203", row[0], "updated date"); Equal(true, row[1], "updated logical");
            Equal(42.75m, Convert.ToDecimal(row[2], CultureInfo.InvariantCulture), "updated amount"); Equal("Привет, memo!", row[3], "updated memo");
            DbfHarbour.Indexes.Select("ID");
            Require(DbfHarbour.Indexes.Seek(4) && DbfHarbour.ActiveRecord == 4, "appended ID index");
            var emptyDate = (string)DbfHarbour.GetValues(new[] { "BIRTH" }, Cp866).Single();
            Require(string.IsNullOrWhiteSpace(emptyDate) || emptyDate.All(c => c == '0'), "empty date");
        }
        finally { DbfHarbour.CloseArea("VERIFY"); }
        Console.WriteLine("PASS: update, append, delete/recall and reopened indexes");
    }

    private static void SpecialTypes(string dir)
    {
        DateTime changed = new DateTime(2027, 2, 3, 11, 22, 33, 789);
        using (var db = new Database(Path.Combine(dir, "special.dbf"), exclusive: true, codepage: "UA866") { DatabaseEncoding = Cp866 })
        {
            db.RegisterIndex("IDZ", Path.Combine(dir, "special_id.cdx"));
            db.RegisterIndex("STAMP", Path.Combine(dir, "special_time.cdx"));
            var before = db.GetValuesDict(1, new[] { "IDZ", "TSZ", "VMZ", "STAMP" });
            Equal(1L, before["IDZ"], "autoincrement");
            Timestamp(before["TSZ"]); Timestamp(before["STAMP"]);
            long version = Convert.ToInt64(before["VMZ"], CultureInfo.InvariantCulture);
            DbfHarbour.Indexes.Select("STAMP");
            Require(DbfHarbour.Indexes.SeekTimestamp(new DateTime(2026, 1, 1, 12, 0, 1, 123)) && DbfHarbour.ActiveRecord == 1, "initial timestamp seek");
            using (db.MakeLock(1)) { db.SetValues(1, new Dictionary<string, object> { { "NAME", "UPDATED" }, { "STAMP", changed } }); DbfHarbour.Commit(); }
            var after = db.GetValuesDict(1, new[] { "IDZ", "TSZ", "VMZ", "STAMP" });
            Equal(1L, after["IDZ"], "stable autoincrement");
            Require(Convert.ToInt64(after["VMZ"], CultureInfo.InvariantCulture) > version, "row version did not advance");
            Timestamp(after["TSZ"]); Equal("20270203112233789", after["STAMP"], "timestamp milliseconds");
            DbfHarbour.Append(); int added = DbfHarbour.ActiveRecord;
            db.SetValues(added, new Dictionary<string, object> { { "NAME", "ADDED" }, { "STAMP", null } });
            DbfHarbour.Commit();
            Equal(4L, db.GetValue<long>(added, "IDZ"), "next autoincrement");
            Equal("00000000000000000", db.GetValue<string>(added, "STAMP"), "empty timestamp");
        }
        DbfHarbour.UseReadOnly(Path.Combine(dir, "special.dbf"), "CHECKTIME", true, "UA866");
        try
        {
            DbfHarbour.Indexes.Load(Path.Combine(dir, "special_id.cdx"));
            DbfHarbour.Indexes.Load(Path.Combine(dir, "special_time.cdx"));
            DbfHarbour.Indexes.Select("STAMP");
            Require(DbfHarbour.Indexes.SeekTimestamp(changed) && DbfHarbour.ActiveRecord == 1, "updated timestamp index");
            DbfHarbour.Indexes.Select("IDZ");
            Require(DbfHarbour.Indexes.Seek(4) && DbfHarbour.ActiveRecord == 4, "generated ID index");
        }
        finally { DbfHarbour.CloseArea("CHECKTIME"); }
        Console.WriteLine("PASS: +, =, ^, @ and separate timestamp CDX");
    }

    private static void Timestamp(object value) => DateTime.ParseExact((string)value, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
    private static void Equal(object expected, object actual, string label) { if (!Equals(expected, actual)) throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual); }
    private static void CheckFixtureSize(string path)
    {
        using (var reader = new BinaryReader(File.OpenRead(path))) { reader.BaseStream.Position = 4; uint count = reader.ReadUInt32(); Require(count > 0 && count <= 20, "fixture must contain 1–20 records: " + path); }
    }
    private static string[] Hashes(string dir)
    {
        using (var sha = SHA256.Create()) return Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(path => Path.GetFileName(path) + ":" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))).ToArray();
    }
}
