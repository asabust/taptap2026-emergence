using TMPro;
using UnityEngine;

namespace Game.Runtime.Gameplay.Campaign
{
    public sealed class LevelSelectionScreen : MonoBehaviour
    {
        public TMP_FontAsset uiFont;
        private void Start()
        {
            if (CampaignRuntime.Instance) CampaignRuntime.Instance.UI.ShowSelection();
        }
    }
}
