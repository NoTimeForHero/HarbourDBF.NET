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
