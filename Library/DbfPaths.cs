using System;
using System.IO;

namespace HarbourDBF.NET
{
    // Path resolution is independent of the process working directory.
    internal static class DbfPaths
    {
        internal static string Root(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A root directory is required.", nameof(root));
            if (!IsFullPath(root)) throw new ArgumentException("The root must be an absolute drive or UNC path.", nameof(root));
            return Path.GetFullPath(root);
        }

        internal static string Table(string root, string path) => Resolve(root, path);

        internal static string Index(string tablePath, string path) => Resolve(Path.GetDirectoryName(tablePath), path);

        private static string Resolve(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
            if (Path.IsPathRooted(path) && !IsFullPath(path))
                throw new ArgumentException("Drive-relative and root-relative paths are not supported.", nameof(path));
            return Path.GetFullPath(IsFullPath(path) ? path : Path.Combine(baseDirectory, path));
        }

        private static bool IsFullPath(string path)
        {
            if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2])) return true;
            if (path.Length < 5 || !IsSeparator(path[0]) || !IsSeparator(path[1])) return false;
            int serverEnd = path.IndexOfAny(new[] { '\\', '/' }, 2);
            if (serverEnd <= 2 || serverEnd == path.Length - 1) return false;
            int shareEnd = path.IndexOfAny(new[] { '\\', '/' }, serverEnd + 1);
            return shareEnd != serverEnd + 1;
        }

        private static bool IsSeparator(char c) => c == '\\' || c == '/';
    }
}
