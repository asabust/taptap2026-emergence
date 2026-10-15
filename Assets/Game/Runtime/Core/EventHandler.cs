using System;

namespace Game.Runtime.Core
{
    public static class EventHandler
    {
        #region 全局消息

        public static event Action<string> BeforeSceneUnloadEvent;

        public static void CallBeforeSceneUnloadEvent(string oldScene)
        {
            BeforeSceneUnloadEvent?.Invoke(oldScene);
        }

        public static event Action<string> AfterSceneLoadEvent;

        public static void CallAfterSceneLoadEvent(string newScene)
        {
            AfterSceneLoadEvent?.Invoke(newScene);
        }

        public static event Action<GamePhase> GamePhaseChangeEvent;

        public static void CallGamePhaseChangedEvent(GamePhase gamePhase)
        {
            GamePhaseChangeEvent?.Invoke(gamePhase);
        }

        #endregion



        #region 剧情

        public static event Action<int> DialogueStartdEvent;

        public static void CallDialogueStartEvent(int dialogueID)
        {
            DialogueStartdEvent?.Invoke(dialogueID);
        }

        public static event Action<int> DialogueFinishedEvent;

        public static void CallDialogueFinishedEvent(int dialogueID)
        {
            DialogueFinishedEvent?.Invoke(dialogueID);
        }

        public static event Action<int> SelectDialogueOptionEvent;

        public static void CallSelectDialogueOptionEvent(int optionId)
        {
            SelectDialogueOptionEvent?.Invoke(optionId);
        }

        #endregion

    }
}