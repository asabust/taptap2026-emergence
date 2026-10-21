using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Game.Runtime.Gameplay.Levels;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Stable ID progress; display order belongs to the catalog, never the save.</summary>
    public sealed class CampaignProgress
    {
        private readonly string[] ids;
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        private string[][] chapters;

        public void ConfigureChapters(IEnumerable<IEnumerable<string>> orderedChapters)
        {
            var next = orderedChapters.Select(c => c.ToArray()).ToArray();
            if (next.Length == 0 || next.Any(c => c.Length == 0)
                || !next.SelectMany(c => c).SequenceEqual(ids))
                throw new ArgumentException("Chapters must partition the ordered catalog exactly.");
            chapters = next;
        }

        public bool IsChapterUnlocked(int index)
        {
            return chapters != null && index >= 0 && index < chapters.Length
                && (index == 0 || chapters.Take(index).All(c => c.All(IsCompleted)));
        }
        public CampaignProgress(IEnumerable<string> orderedIds, string savedJson = null)
        {
            if (orderedIds == null) throw new ArgumentNullException(nameof(orderedIds));
            ids = orderedIds.ToArray();
            if (ids.Length == 0 || ids.Any(id => !LevelRepository.IsValidId(id)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Catalog must contain unique, valid level IDs.");
            if (string.IsNullOrEmpty(savedJson)) return;
            JObject root;
            try { root = JObject.Parse(savedJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
            catch (JsonException e) { throw new FormatException("Invalid progress JSON.", e); }
            if (root.Properties().Any(p => p.Name != "version" && p.Name != "completed")
                || root["version"]?.Type != JTokenType.Integer || root["version"].ToString() != "1"
                || !(root["completed"] is JArray values)) throw new FormatException("Unsupported progress format.");
            foreach (var value in values)
            {
                if (value.Type != JTokenType.String || !LevelRepository.IsValidId((string)value)) throw new FormatException("Invalid completed level ID.");
                completed.Add((string)value); // Preserve IDs absent from today's catalog.
            }
        }
        public bool IsCompleted(string id) => completed.Contains(id);
        public bool IsUnlocked(string id)
        {
            if (chapters != null)
            {
                int chapter = Array.FindIndex(chapters, c => Array.IndexOf(c, id) >= 0);
                if (chapter < 0 || !IsChapterUnlocked(chapter)) return false;
                int slot = Array.IndexOf(chapters[chapter], id);
                return chapters[chapter].Take(slot).All(IsCompleted);
            }
            int index = Array.IndexOf(ids, id);
            return index >= 0 && (completed.Contains(id) || index == 0 || completed.Contains(ids[index - 1]));
        }
        public bool Complete(string id)
        {
            if (!IsUnlocked(id)) throw new InvalidOperationException("Cannot complete a locked or unknown level.");
            return completed.Add(id);
        }
        public bool AllCompleted => ids.All(completed.Contains);
        public string RecommendedId => ids.FirstOrDefault(id => IsUnlocked(id) && !completed.Contains(id)) ?? ids[ids.Length - 1];
        public string NextId(string id)
        {
            int index = Array.IndexOf(ids, id);
            return index >= 0 && index + 1 < ids.Length ? ids[index + 1] : null;
        }
        public string Serialize() => new JObject { ["version"] = 1, ["completed"] = new JArray(completed.OrderBy(id => id, StringComparer.Ordinal)) }.ToString(Formatting.None);
    }
}
