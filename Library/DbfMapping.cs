using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HarbourDBF.NET
{
    /// <summary>Описывает соответствие свойств модели существующим полям DBF.</summary>
    public abstract class DbfMapping<T> where T : class, new()
    {
        private readonly List<DbfFieldMapping> fields = new List<DbfFieldMapping>();
        private readonly List<DbfIndexDefinition> indexes = new List<DbfIndexDefinition>();
        private readonly List<string> indexBags = new List<string>();
        private bool frozen;

        protected DbfFieldOptions Field<TValue>(Expression<Func<T, TValue>> property, string name)
        {
            EnsureMutable();
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A DBF field name is required.", nameof(name));
            var member = property.Body as MemberExpression;
            var info = member?.Member as PropertyInfo;
            if (info == null || member.Expression != property.Parameters[0] || info.GetIndexParameters().Length != 0 ||
                info.GetGetMethod() == null || info.GetSetMethod() == null)
                throw new ArgumentException("Select a public readable and writable property of the model.", nameof(property));
            foreach (var field in fields)
                if (field.Property == info || string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The property or DBF field is already mapped.", nameof(property));
            var result = new DbfFieldMapping(info, name);
            fields.Add(result);
            return new DbfFieldOptions(result, EnsureMutable);
        }

        internal IReadOnlyList<DbfFieldMapping> Freeze()
        {
            if (fields.Count == 0) throw new InvalidOperationException("The mapping contains no fields.");
            frozen = true;
            return fields.AsReadOnly();
        }

        protected DbfIndexMapping<T, TKey> Index<TKey>(Expression<Func<T, TKey>> key, string file, string tag)
        {
            EnsureMutable();
            if (key == null) throw new ArgumentNullException(nameof(key));
            var member = key.Body as MemberExpression;
            var property = member?.Member as PropertyInfo;
            if (property == null || member.Expression != key.Parameters[0]) throw new ArgumentException("Select a mapped property.", nameof(key));
            var field = fields.Find(x => x.Property == property);
            if (field == null) throw new ArgumentException("The indexed property must be mapped first.", nameof(key));
            if (typeof(TKey) != typeof(int) && typeof(TKey) != typeof(string)) throw new ArgumentException("Only int and string keys are supported.", nameof(key));
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("An index file is required.", nameof(file));
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("An index tag is required.", nameof(tag));
            foreach (var existing in indexes)
                if (string.Equals(existing.Tag, tag, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Index tag names must be unique in a mapping.", nameof(tag));
            var definition = new DbfIndexMapping<T, TKey>(this, field, file, tag);
            indexes.Add(definition);
            return definition;
        }

        internal IReadOnlyList<DbfIndexDefinition> Indexes => indexes.AsReadOnly();

        /// <summary>Объявляет сопровождаемый индексный файл без поискового свойства.</summary>
        protected void IndexBag(string file)
        {
            EnsureMutable();
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("An index file is required.", nameof(file));
            indexBags.Add(file);
        }

        internal IReadOnlyList<string> IndexBags => indexBags.AsReadOnly();

        private void EnsureMutable()
        {
            if (frozen) throw new InvalidOperationException("The mapping is already in use and cannot be changed.");
        }
    }

    public sealed class DbfFieldOptions
    {
        private readonly DbfFieldMapping field;
        private readonly Action ensureMutable;
        internal DbfFieldOptions(DbfFieldMapping field, Action ensureMutable) { this.field = field; this.ensureMutable = ensureMutable; }

        public DbfFieldOptions TrimEnd()
        {
            ensureMutable();
            if (field.Property.PropertyType != typeof(string)) throw new InvalidOperationException("TrimEnd requires a string property.");
            field.TrimEnd = true;
            return this;
        }
    }

    internal sealed class DbfFieldMapping
    {
        internal DbfFieldMapping(PropertyInfo property, string name) { Property = property; Name = name; }
        internal PropertyInfo Property { get; }
        internal string Name { get; }
        internal bool TrimEnd { get; set; }
    }
}
