using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    public sealed partial class LevelPlayScreen
    {
        private GameplayHUDView prefabView;
        private void BuildPrefabPresentation()
        {
            var prefab = Resources.Load<GameplayHUDView>("UI/GameplayHUDPanel");
            if (!prefab) throw new System.InvalidOperationException("GameplayHUDPanel.prefab missing. Run Tools/UI/Create Editable UI.");
            canvasObject = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 30;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            prefabView = Instantiate(prefab, canvasObject.transform, false);
            board = prefabView.board; board.CellClicked += SelectCell; board.AnimationCompleted += FinishAnimation;
            title = prefabView.title; counters = prefabView.counters; goals = prefabView.goals; mode = prefabView.mode;
            status = prefabView.status; overlayText = prefabView.overlayText; overlay = prefabView.overlay;
            undo = prefabView.undo; iterate = prefabView.iterate; clone = prefabView.clone; recycle = prefabView.recycle;
            cancel = prefabView.cancel; pause = prefabView.pause; reset = overlayRetry = prefabView.retry;
            overlayContinue = prefabView.overlayContinue; overlayBack = prefabView.back;
            victoryCard = prefabView.victory; progressMarks = prefabView.progressMarks;
            undo.onClick.AddListener(() => Queue(LevelPlayCommand.Undo));
            iterate.onClick.AddListener(() => Queue(LevelPlayCommand.Iterate));
            clone.onClick.AddListener(() => Queue(LevelPlayCommand.Clone));
            recycle.onClick.AddListener(() => Queue(LevelPlayCommand.Recycle));
            cancel.onClick.AddListener(() => Queue(LevelPlayCommand.Cancel));
            pause.onClick.AddListener(() => Queue(LevelPlayCommand.Pause));
            overlayRetry.onClick.AddListener(ResetFromPanel);
            overlayBack.onClick.AddListener(() => CampaignRuntime.Instance.UI.Confirm("返回章节选关？", "退出本局状态不会保存", LeaveGame));
            prefabView.rules.onClick.AddListener(ShowRules);
            prefabView.settings.onClick.AddListener(() => CampaignRuntime.Instance.ShowSettings());
            prefabView.victoryContinue.onClick.AddListener(() =>
            {
                prefabView.victoryContinue.interactable = false;
                CampaignRuntime.Instance.UI.AfterLevel(definition.id, LeaveGame);
            });
            prefabView.victoryReplay.onClick.AddListener(ResetFromPanel);
            prefabView.failure.primary.onClick.AddListener(() => Queue(LevelPlayCommand.Undo));
            prefabView.failure.secondary.onClick.AddListener(ResetFromPanel);
            prefabView.failure.back.onClick.AddListener(() => CampaignRuntime.Instance.UI.Confirm("返回章节选关？", "退出本局状态不会保存", LeaveGame));
            victoryCard.SetActive(false);
            CampaignUI.EnsureInput();
        }
        private void ResetFromPanel()
        {
            completionReady = false; victoryReported = false;
            if (prefabView) prefabView.victoryContinue.interactable = true;
            controller = new LevelPlayController(Session); Queue(LevelPlayCommand.Reset);
        }
    }
}
