using System;
using System.IO;
using System.Text;

namespace Game.Runtime.Core.IO
{
    /// <summary>Desktop/local text I/O; URLs and browser requests belong to another adapter.</summary>
    public static class AtomicTextFile
    {
        public static string Read(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        public static void Write(string path, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            // Same directory/filesystem ensures replacement cannot be a cross-volume copy.
            string temporary = Path.Combine(directory, "." + Path.GetFileName(fullPath)
                + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(text);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
