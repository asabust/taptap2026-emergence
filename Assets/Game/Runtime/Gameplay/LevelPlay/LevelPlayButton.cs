using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    /// <summary>Space belongs to the play controller, never to a previously focused button.</summary>
    public sealed class LevelPlayButton : Button
    {
        public override void OnSubmit(BaseEventData eventData) { }
    }
}
