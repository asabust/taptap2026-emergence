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
        [TextArea(3, 8)] public string description;
        // Normalized coordinates on the complete anatomy image; origin is bottom-left.
        public Vector2 bodyPosition = new Vector2(.3f, .5f);
        public Texture2D preview;
    }

    [CreateAssetMenu(menuName = "Game/Campaign Catalog")]
    public sealed class CampaignCatalog : ScriptableObject
    {
        public Texture2D anatomy;
        public List<CampaignLevel> levels = new List<CampaignLevel>();
    }
}
