using UnityEngine;

namespace Game.Runtime.Data.Campaign
{
    [CreateAssetMenu(menuName = "Game/Campaign Art")]
    public sealed class CampaignArt : ScriptableObject
    {
        public Texture2D title, titleLogo, background, anatomy, skeleton, scanTexture, back, selector, victory;
    }
}
