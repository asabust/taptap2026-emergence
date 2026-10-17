using System;
using System.IO;
using Game.Runtime.Gameplay.Levels;

namespace Game.Runtime.Gameplay.LevelEditor
{
    /// <summary>Persistence boundary; failed I/O never replaces the editor's current draft.</summary>
    public sealed class LevelEditorDocument : IDisposable
    {
        public LevelEditorSession Session { get; }
        public LevelRepository Repository { get; }
        public string SavedId { get; private set; }
        public bool IsDirty { get; private set; } = true;
        public LevelEditorDocument(LevelEditorSession session, LevelRepository repository)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Session.Changed += MarkDirty;
        }
        private void MarkDirty() { IsDirty = true; }
        public void New(int width, int height)
        {
            Session.New(width, height); SavedId = null; IsDirty = true;
        }
        public void Load(string id)
        {
            var loaded = Repository.Load(id);
            Session.ReplaceDraft(loaded);
            SavedId = id; IsDirty = false;
        }
        public string Save(string id, string name, bool saveAs)
        {
            string path = Repository.PathFor(id);
            if (saveAs)
            {
                if (id == SavedId) throw new InvalidOperationException("另存为需要一个新的关卡 ID。");
                if (File.Exists(path)) throw new IOException("该 ID 已存在，请换一个新 ID，或加载后使用保存。");
            }
            else if (SavedId != null && id != SavedId)
                throw new InvalidOperationException("关卡 ID 已改变，请使用另存为。");
            var candidate = LevelJson.DeepCopy(Session.Draft);
            candidate.id = id; candidate.name = name;
            Repository.Save(candidate);
            int selected = Session.SelectedGoalIndex;
            Session.ReplaceDraft(candidate);
            Session.SelectGoal(selected);
            SavedId = id; IsDirty = false;
            return path;
        }
        public void Dispose() { Session.Changed -= MarkDirty; }
    }
}
