using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Only data is refreshed at runtime; the serialized prefab owns layout and artwork.</summary>
    public sealed class CampaignPanelView : MonoBehaviour
    {
        public TextMeshProUGUI heading, body, detail, progress, speaker;
        public Image artwork, portrait;
        public Button primary, secondary, back, settings, archive;
        public Slider music, sfx;
        public RectTransform content;
        public Button itemTemplate;
        public Button[] chapterButtons;
        public TextMeshProUGUI[] chapterLabels;
    }
}
