using System;
using UnityEngine;

namespace Game.Runtime.Data.Campaign
{
    [Serializable] public sealed class NarrativeDatabase
    {
        public DialogueLine[] lines = Array.Empty<DialogueLine>();
        public DialogueGroup[] groups = Array.Empty<DialogueGroup>();
        public ChapterArchive[] archives = Array.Empty<ChapterArchive>();
        public CampaignChapter[] chapters = Array.Empty<CampaignChapter>();
        public CampaignLevel[] levels = Array.Empty<CampaignLevel>();
    }
    [Serializable] public sealed class DialogueLine
    {
        public string lineId, dialogueId, speakerName, portraitId;
        [TextArea] public string text;
        public int order;
    }
    [Serializable] public sealed class DialogueGroup
    {
        public string dialogueId, kind, levelId, timing;
        public int order;
    }
    [Serializable] public sealed class ChapterArchive
    {
        public string archiveId, title, body, illustrationId, caption;
    }
}
