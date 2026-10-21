using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Data.Campaign
{
    [Serializable]
    public sealed class CampaignLevel
    {
        public string id;
        public string title;
        public string bodyPart;
        [TextArea(3, 8)] public string description;
        // Normalized coordinates on the complete anatomy image; origin is bottom-left.
        public Vector2 bodyPosition = new Vector2(.3f, .5f);
        public Texture2D preview;
    }

    [Serializable]
    public sealed class CampaignLocation
    {
        public string label;
        public Vector2 position;
    }

    [CreateAssetMenu(menuName = "Game/Campaign Catalog")]
    public sealed class CampaignCatalog : ScriptableObject
    {
        public Texture2D anatomy;
        public List<CampaignLocation> locations = new List<CampaignLocation>();
        public List<CampaignLevel> levels = new List<CampaignLevel>();
        public List<CampaignChapter> chapters = new List<CampaignChapter>();
    }

    [Serializable]
    public sealed class CampaignChapter
    {
        public string id, title, bodyPart, archiveId;
        public Vector2 bodyPosition;
        public Sprite artwork;
        public List<string> levelIds = new List<string>();
    }
}
