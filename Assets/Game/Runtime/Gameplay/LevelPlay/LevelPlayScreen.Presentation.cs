using System.Linq;
using System.Text;
using Game.Runtime.Data.Levels;
using Game.Runtime.Data.Presentation;
using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    public sealed partial class LevelPlayScreen
    {
        private bool GamePresentation => !showReturnToEditor;
        private bool rulesOpen;
        private GameObject rulesPanel;
        private RectTransform progressMarks;
        private string markedLevel;
        private static readonly Color GameInk = new Color(.34f, .40f, .53f);

        private void BuildGamePresentation()
        {
            var art = Resources.Load<LevelPlayArt>("LevelPlay/Presentation");
            canvasObject = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;
            var background = Box(root, "Paper Background", new Color(.92f,.94f,.95f)); Stretch(background);
            if (art && art.background)
            {
                var paper = Picture(root, "Cell Paper", art.background, 0, 0, 0, 0); Stretch(paper.rectTransform);
                var fit = paper.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)art.background.width / art.background.height;
            }
            pause = ArtButton(root, "暂停", art ? art.pause : null, 32, 25, 190, 48, () => Queue(LevelPlayCommand.Pause));
            counters = GameLabel(root, "", 0, 0, 440, 36, 16);
            counters.rectTransform.anchorMin = counters.rectTransform.anchorMax = new Vector2(.5f, 1);
            counters.rectTransform.pivot = new Vector2(.5f, 1); counters.rectTransform.anchoredPosition = new Vector2(0,-68);
            counters.alignment = TextAlignmentOptions.Center;
            progressMarks = Box(root, "Chapter Marks", Color.clear); progressMarks.GetComponent<Image>().raycastTarget = false;
            progressMarks.anchorMin = progressMarks.anchorMax = progressMarks.pivot = new Vector2(.5f, 1);
            progressMarks.sizeDelta = new Vector2(440,32); progressMarks.anchoredPosition = new Vector2(0,-26);

            var area = Box(root, "Floating Board Area", Color.clear); area.GetComponent<Image>().raycastTarget = false;
            area.anchorMin = new Vector2(.20f,.14f); area.anchorMax = new Vector2(.84f,.89f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var br = new GameObject("Flowing Terrain Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RectTransform>();
            br.SetParent(area, false); Stretch(br);
            br.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            board = br.gameObject.AddComponent<OrganicLevelBoardView>(); board.styledTerrain = true;
            board.CellClicked += SelectCell; board.AnimationCompleted += FinishAnimation;

            var info = Box(root, "Level Objectives", Color.clear); info.GetComponent<Image>().raycastTarget = false;
            info.anchorMin = info.anchorMax = info.pivot = Vector2.zero;
            info.anchoredPosition = new Vector2(32,48); info.sizeDelta = new Vector2(405,155);
            title = GameLabel(info, "", 0, 0, 405, 45, 28);
            goals = GameLabel(info, "", 14, 54, 385, 100, 18);
            goals.overflowMode = TextOverflowModes.Ellipsis;

            var dock = Box(root, "Evolution Actions", Color.clear); dock.GetComponent<Image>().raycastTarget = false;
            dock.anchorMin = dock.anchorMax = dock.pivot = new Vector2(1,0);
            dock.anchoredPosition = new Vector2(-24,18); dock.sizeDelta = new Vector2(282,400);
            undo = ArtButton(dock, "撤销", art ? art.undo : null, 194, 20, 64, 64, () => Queue(LevelPlayCommand.Undo));
            GameLabel(dock, "撤销 Z", 188, 88, 88, 28, 15).alignment = TextAlignmentOptions.Center;
            iterate = ArtButton(dock, "演化", art ? art.evolution : null, 90, 128, 190, 236, () => Queue(LevelPlayCommand.Iterate), true);
            GameLabel(dock, "演化  Space", 92, 365, 188, 30, 18).alignment = TextAlignmentOptions.Center;
            clone = ArtButton(dock, "分身", null, 4, 144, 82, 82, () => Queue(LevelPlayCommand.Clone));
            var cloneLabel = clone.GetComponentInChildren<TextMeshProUGUI>(); if (cloneLabel) cloneLabel.gameObject.SetActive(false);
            var glyphObject = new GameObject("Paired Cells", typeof(RectTransform), typeof(CanvasRenderer), typeof(OrganicGlyphGraphic));
            var glyph = glyphObject.GetComponent<OrganicGlyphGraphic>(); glyph.paired = true; glyph.color = new Color(.55f,.66f,.76f); glyph.raycastTarget = false;
            glyphObject.transform.SetParent(clone.transform,false); Stretch((RectTransform)glyphObject.transform);
            clone.targetGraphic = glyph;
            GameLabel(dock, "分身 B", 0, 230, 90, 28, 16).alignment = TextAlignmentOptions.Center;
            recycle = TextAction(dock, "回收", 2, 264, 84, () => Queue(LevelPlayCommand.Recycle));
            ArtButton(dock, "规则说明", art ? art.rules : null, 0, 326, 58, 58, ShowRules);
            cancel = TextAction(root, "取消选择  Esc", 0, 0, 170, () => Queue(LevelPlayCommand.Cancel));
            var cr = (RectTransform)cancel.transform; cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(.5f,0);
            cr.anchoredPosition = new Vector2(0,76);
            mode = GameLabel(root, "", 0, 0, 690, 28, 15);
            mode.rectTransform.anchorMin = mode.rectTransform.anchorMax = mode.rectTransform.pivot = new Vector2(.5f,0);
            mode.rectTransform.anchoredPosition = new Vector2(0,44); mode.alignment = TextAlignmentOptions.Center;
            status = GameLabel(root, "", 0, 0, 760, 32, 14);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(.5f,0);
            status.rectTransform.anchoredPosition = new Vector2(0,10); status.alignment = TextAlignmentOptions.Center;

            var cover = Box(root, "Game State Overlay", new Color(.89f,.93f,.95f,.90f)); Stretch(cover); overlay = cover.gameObject;
            overlayText = GameLabel(cover, "", 0, 0, 650, 120, 28);
            var tr = overlayText.rectTransform; tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(.5f,.5f);
            tr.anchoredPosition = new Vector2(0,125); overlayText.alignment = TextAlignmentOptions.Center;
            overlayContinue = OverlayButton(cover, "继续", 12, () => Queue(LevelPlayCommand.Cancel));
            overlayRetry = OverlayButton(cover, "重新开始", -48, () => { controller = new LevelPlayController(Session); Queue(LevelPlayCommand.Reset); });
            overlayBack = OverlayButton(cover, "返回选关", -108, LeaveGame);
            reset = overlayRetry;
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Game EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform,false);
            }
        }

        private void LeaveGame()
        {
            if (formalGame) ReturnToEditor();
            else if (CampaignRuntime.Instance) CampaignRuntime.Instance.ReturnToSelection();
        }
        private void RefreshGamePresentation(bool paused)
        {
            title.text = definition.name;
            counters.text = "演化  " + Session.Iteration + " / " + Session.IterationLimit + "     ·     分身  " + Session.LivingCloneCount + " / " + Session.CloneLimit;
            goals.text = DescribeGameObjectives();
            mode.text = paused ? "已暂停" : Session.Mode == LevelInteractionMode.PlaceClone ? "选择玩家周围高亮地块，放置分身"
                : Session.Mode == LevelInteractionMode.RecycleClone ? "选择高亮分身，回收名额" : "方向键移动  ·  Space 演化  ·  B 分身  ·  Z 撤销";
            cancel.gameObject.SetActive(Session.Mode != LevelInteractionMode.Move && !paused);
            overlayText.text = paused ? "已暂停\n继续观察，或重新开始本关" : Session.Status == LevelPlayStatus.Won ? "目标已达成" : "演化次数已超限\n可撤销或重新开始";
            if (markedLevel != definition.id)
            {
                markedLevel = definition.id;
                foreach (Transform child in progressMarks) Destroy(child.gameObject);
                var flow = CampaignRuntime.Instance;
                var entries = flow && flow.Catalog ? flow.Catalog.levels : null;
                int count = entries == null ? 1 : Mathf.Min(entries.Count,18);
                for (int i = 0; i < count; i++)
                {
                    bool current = entries == null || entries[i].id == definition.id;
                    var go = new GameObject("Level Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(OrganicGlyphGraphic));
                    go.transform.SetParent(progressMarks,false); var rect = (RectTransform)go.transform;
                    Place(rect, 220-count*17+i*34, current ? 0 : 6, current ? 28 : 20, current ? 30 : 18);
                    var graphic = go.GetComponent<OrganicGlyphGraphic>(); graphic.raycastTarget = false;
                    graphic.color = current ? new Color(.38f,.50f,.75f) : new Color(.65f,.70f,.75f,.55f);
                }
            }
        }
        private string DescribeGameObjectives()
        {
            var text = new StringBuilder();
            foreach (var goal in definition.winConditions.conditions)
            {
                if (goal.type == LevelIds.ActorAtPosition) text.Append("抵达金色终点\n");
                else if (goal.type == LevelIds.CellCount) text.Append("存活细胞 ").Append(goal.@operator).Append(' ').Append(goal.value).Append("  ·  当前 ").Append(Session.State.Cells.Count).Append('\n');
                else
                {
                    var state = Session.State;
                    int covered = goal.positions.Count(p => state.Cells.Any(c => c.X == p.x && c.Y == p.y));
                    text.Append("覆盖紫色标记  ").Append(covered).Append(" / ").Append(goal.positions.Count).Append('\n');
                }
            }
            return text.ToString();
        }
        private void ShowRules()
        {
            if (rulesOpen || Session == null) return;
            requests.Clear(); Session.CancelSelection(); Refresh(); rulesOpen = true; board.Paused = true;
            var root = (RectTransform)canvasObject.transform;
            var dim = Box(root, "Rules Overlay", new Color(.86f,.91f,.94f,.96f)); Stretch(dim); rulesPanel = dim.gameObject;
            var window = Box(dim, "Rules Card", new Color(.97f,.98f,.98f));
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f,.5f); window.sizeDelta = new Vector2(760,650);
            GameLabel(window, "本关规则", 36, 28, 680, 50, 30);
            bool lonely = definition.rules.Any(r => r.id == LevelIds.LonelyDeath), crowded = definition.rules.Any(r => r.id == LevelIds.CrowdedDeath);
            string text = "孤独死亡：" + (lonely ? "开启，少于 2 个邻居时死亡" : "关闭")
                + "\n拥挤死亡：" + (crowded ? "开启，多于 3 个邻居时死亡" : "关闭")
                + "\n\n每次演化同时结算八邻域；不会自动生成细胞。\n玩家参与邻居计算，自身不会死亡。\n\n分身：玩家周围八格的空地，名额为同时存活数。\n回收：可选棋盘任意存活分身，不能回收初始细胞。\n\nSpace 演化 · B 分身 · Z 撤销上一轮 · R 重新开始\n方向键单次移动 · Esc 取消选择或暂停\n\n本关目标\n" + DescribeGoals(Session.State);
            var scrollRect = Box(window, "Rule Scroll", Color.clear); Place(scrollRect,36,94,688,440);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false;
            var viewport = Box(scrollRect,"Viewport",Color.clear); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Box(viewport,"Content",Color.clear); content.GetComponent<Image>().raycastTarget=false;
            content.anchorMin = new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1);
            var label = GameLabel(content,text,0,0,660,900,19); label.ForceMeshUpdate();
            float height = Mathf.Max(440,label.preferredHeight+16); label.rectTransform.sizeDelta=new Vector2(660,height); content.sizeDelta=new Vector2(0,height);
            scroll.viewport=viewport; scroll.content=content; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
            TextAction(window,"关闭 · Esc",260,570,240,CloseRules);
        }
        private void CloseRules()
        {
            if (!rulesOpen) return;
            rulesOpen=false;
            if (rulesPanel) { rulesPanel.SetActive(false); Destroy(rulesPanel); rulesPanel=null; }
            requests.Clear(); Refresh(false);
        }
        private TextMeshProUGUI GameLabel(RectTransform parent,string text,float x,float y,float w,float h,int size)
        { var label=Label(parent,text,x,y,w,h,size); label.color=GameInk; return label; }
        private RawImage Picture(RectTransform parent,string name,Texture texture,float x,float y,float w,float h)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            var rect=(RectTransform)go.transform; rect.SetParent(parent,false); Place(rect,x,y,w,h);
            var image=go.GetComponent<RawImage>(); image.texture=texture; image.raycastTarget=false; return image;
        }
        private Button ArtButton(RectTransform parent,string caption,Texture2D art,float x,float y,float w,float h,System.Action action,bool crop=false)
        {
            var rect=Box(parent,caption,Color.clear); Place(rect,x,y,w,h);
            var button=rect.gameObject.AddComponent<LevelPlayButton>(); button.targetGraphic=rect.GetComponent<Image>();
            button.navigation=new Navigation { mode=Navigation.Mode.None }; button.onClick.AddListener(()=> { if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null); action(); });
            if (art)
            {
                var image=Picture(rect,caption+" Art",art,0,0,w,h);
                if(crop) image.uvRect=new Rect(0,0,1,.73f);
                else
                {
                    var fit=image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio=(float)art.width/art.height;
                }
                button.targetGraphic=image;
            }
            else { var label=GameLabel(rect,caption,0,0,w,h,22); label.alignment=TextAlignmentOptions.Center; }
            return button;
        }
        private Button TextAction(RectTransform parent,string caption,float x,float y,float width,System.Action action)
        {
            var button=ArtButton(parent,caption,null,x,y,width,38,action);
            button.GetComponent<Image>().color=new Color(.78f,.84f,.88f,.35f);
            return button;
        }
    }
}
