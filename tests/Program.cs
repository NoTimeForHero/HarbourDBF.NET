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

    private static int Main()
    {
        try { PathResolution(); MappingDeclarations(); }
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
