using Game.Runtime.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    [RequireComponent(typeof(Button))]
    public sealed class UISoundFeedback : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;
        private void Awake() { button = GetComponent<Button>(); }
        private void OnEnable() { if (!button) button = GetComponent<Button>(); button.onClick.AddListener(Click); }
        private void OnDisable() { if (button) button.onClick.RemoveListener(Click); }
        private void Click() { AudioManager.Emit(AudioName.Click); }
        public void OnPointerEnter(PointerEventData data)
        { if (button && button.IsActive() && button.IsInteractable()) AudioManager.Emit(AudioName.Hover); }
    }
}
