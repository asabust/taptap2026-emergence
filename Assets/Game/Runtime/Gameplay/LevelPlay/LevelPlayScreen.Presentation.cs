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
        private GameObject rulesPanel, victoryCard;
        private bool completionReady;
        private RectTransform progressMarks;
        private string markedLevel;
        private static readonly Color GameInk = new Color(.34f, .40f, .53f);

        private void BuildGamePresentation()
        {
            BuildPrefabPresentation();
        }

        private void LeaveGame()
        {
            if (formalGame) ReturnToEditor();
            else if (CampaignRuntime.Instance) CampaignRuntime.Instance.ReturnToSelection();
        }
        private void RefreshGamePresentation(bool paused)
        {
            if (Session.Status != LevelPlayStatus.Won) completionReady = false;
            if (prefabView) prefabView.settings.gameObject.SetActive(paused);
            bool won = formalGame && completionReady && Session.Status == LevelPlayStatus.Won;
            if (victoryCard)
            {
                victoryCard.SetActive(won);
                if (won)
                {
                    overlay.SetActive(false);
                    var summary = victoryCard.transform.Find("Victory Summary").GetComponent<TextMeshProUGUI>();
                    var flow = CampaignRuntime.Instance;
                    summary.text = definition.name + "\n演化 " + Session.Iteration + " 轮\n\n"
                        + (flow && flow.Progress.AllCompleted ? "全部关卡已完成。可返回生命图谱重玩。" : (flow ? flow.Notice : "返回生命图谱继续探索。"));
                }
            }
            title.text = definition.name;
            counters.text = "演化  " + Session.Iteration + " / " + Session.IterationLimit + "     ·     分身  " + Session.LivingCloneCount + " / " + Session.CloneLimit;
            goals.text = DescribeGameObjectives();
            mode.text = paused ? "已暂停" : Session.Mode == LevelInteractionMode.PlaceClone ? "选择玩家周围高亮地块，放置分身"
                : Session.Mode == LevelInteractionMode.RecycleClone ? "选择高亮分身，回收名额" : "方向键移动  ·  Space 演化  ·  B 分身  ·  N 回收  ·  Z 撤销";
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
                if (goal.type == LevelIds.ActorAtPosition) text.Append("抵达蓝色交叉终点\n");
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
            bool lonely = definition.rules.Any(r => r.id == LevelIds.LonelyDeath), crowded = definition.rules.Any(r => r.id == LevelIds.CrowdedDeath);
            bool birth = definition.rules.Any(r => r.id == LevelIds.LocalBirth);
            string text = "孤独死亡：" + (lonely ? "开启，少于 2 个邻居时死亡" : "关闭")
                + "\n拥挤死亡：" + (crowded ? "开启，多于 3 个邻居时死亡" : "关闭")
                + "\n局部繁殖：" + (birth ? "开启，玩家周围八格空地恰有 3 个邻居时生成普通细胞" : "关闭")
                + "\n\n每次演化按旧状态同时结算八邻域。\n玩家参与邻居计算，自身不会死亡。\n\n分身：玩家周围八格、与玩家四向地形连通的空地，名额为同时存活数。\n浅色方砖为地面，深色方砖为墙；蓝色叉号为玩家终点。\nN 回收：上限为 1 时直接回收；上限大于 1 时点击选择，Esc 取消。不能回收初始普通细胞。\nZ 按倒序撤销演化、创建分身或回收，恢复该操作之前的棋盘；移动不单独计步。\n\nSpace 演化 · B 分身 · N 回收 · Z 撤销上一步 · R 重新开始\n方向键单次移动 · Esc 取消选择或暂停\n\n本关目标\n" + DescribeGoals(Session.State);
            CampaignRuntime.Instance.UI.ShowRules(text, CloseRules);
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
