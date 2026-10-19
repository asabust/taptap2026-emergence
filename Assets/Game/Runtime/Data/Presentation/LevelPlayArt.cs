using UnityEngine;

namespace Game.Runtime.Data.Presentation
{
    [CreateAssetMenu(menuName = "Game/Level Play Art")]
    public sealed class LevelPlayArt : ScriptableObject
    {
        public Texture2D background;
        public Texture2D evolution;
        public Texture2D undo;
        public Texture2D pause;
        public Texture2D rules;
    }
}
