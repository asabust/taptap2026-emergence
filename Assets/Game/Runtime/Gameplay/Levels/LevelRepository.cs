using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Runtime.Core.IO;
using Game.Runtime.Data.Levels;

namespace Game.Runtime.Gameplay.Levels
{
    /// <summary>Developer desktop repository. WebGL packaged levels use a future request adapter.</summary>
    public sealed class LevelRepository
    {
        public string DirectoryPath { get; }
        public LevelRepository(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("Directory is required.");
            DirectoryPath = Path.GetFullPath(directoryPath);
        }
        public static bool IsValidId(string id)
        {
            // Portable filenames, including Windows device-name restrictions.
            if (id == null || !Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z")) return false;
            return !Regex.IsMatch(id, @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase);
        }
        public string PathFor(string id)
        {
            if (!IsValidId(id)) throw new ArgumentException("Invalid level ID: " + id);
            return Path.Combine(DirectoryPath, id + ".json");
        }
        public LevelDefinition Load(string id)
        {
            var level = LevelJson.Deserialize(AtomicTextFile.Read(PathFor(id)));
            if (level.id != id) throw new FormatException("Level ID differs from filename.");
            return level;
        }
        // Saving is deliberately not gated by ValidateForPlay: unfinished drafts are valid files.
        public void Save(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            string path = PathFor(level.id);
            string json = LevelJson.Serialize(level); // Serialize before touching the old file.
            AtomicTextFile.Write(path, json);
        }
#if UNITY_5_3_OR_NEWER
        public static LevelRepository DeveloperDefault()
        {
            return new LevelRepository(Path.Combine(UnityEngine.Application.streamingAssetsPath, "Levels"));
        }
#endif
    }
}
