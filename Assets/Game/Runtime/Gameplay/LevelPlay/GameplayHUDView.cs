using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Runtime.Gameplay.Campaign;

namespace Game.Runtime.Gameplay.LevelPlay
{
    public sealed class GameplayHUDView : MonoBehaviour
    {
        public OrganicLevelBoardView board;
        public TextMeshProUGUI title, counters, goals, mode, status, overlayText;
        public Button undo, iterate, clone, recycle, cancel, pause, retry, overlayContinue, back, rules, settings, victoryContinue, victoryReplay;
        public GameObject overlay, victory;
        public RectTransform progressMarks;
        public CampaignPanelView failure;
    }
}
