using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    public sealed class LevelSelectionScreen : MonoBehaviour
    {
        public TMP_FontAsset uiFont;
        private RectTransform root, preview;
        private TextMeshProUGUI message;
        private CampaignRuntime flow;
        private void Start()
        {
            flow = CampaignRuntime.Instance;
            CampaignUI.Font = uiFont;
            CampaignUI.EnsureInput();
            root = CampaignUI.Canvas(transform, "Level Selection");
            var background = CampaignUI.Box(root, "Background", 0, 0, 0, 0, new Color(.88f, .92f, .94f)); CampaignUI.Stretch(background);
            if (!flow || !flow.Catalog || flow.Progress == null)
            {
                CampaignUI.Label(root, "关卡目录未配置，请执行 Configure Stage 7 Game Flow。", 80, 180, 1250, 160);
                CampaignUI.Button(root, "返回标题", 80, 60, 180, () => flow?.ReturnToTitle()); return;
            }
            // Nodes and anatomy share one aspect-correct rectangle, including when the window changes shape.
            var art = CampaignUI.Picture(root, flow.Catalog.anatomy, 0, 0, 1440, 810);
            CampaignUI.Stretch(art.rectTransform);
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = flow.Catalog.anatomy ? (float)flow.Catalog.anatomy.width / flow.Catalog.anatomy.height : 16f / 9;
            CampaignUI.Label(root, "人体选关", 40, 25, 400, 52, 30);
            CampaignUI.Button(root, "返回标题", 1200, 25, 180, flow.ReturnToTitle);
            message = CampaignUI.Label(root, flow.Notice + "\n选择人体上的关卡标记，查看局部预览。", 40, 100, 1300, 90, 20);
            foreach (var entry in flow.Catalog.levels)
            {
                var level = entry;
                bool unlocked = flow.Progress.IsUnlocked(level.id);
                string state = flow.Progress.IsCompleted(level.id) ? "已完成" : unlocked ? "可进入" : "未解锁";
                var button = CampaignUI.Button(art.rectTransform, level.title + "\n" + state, 0, 0, 160, () => ShowPreview(level.id));
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = level.bodyPosition; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(160, 66);
                var label = button.GetComponentInChildren<TextMeshProUGUI>(); CampaignUI.Stretch(label.rectTransform); label.fontSize = 18;
                button.interactable = unlocked;
                button.GetComponent<Image>().color = level.id == flow.HighlightId ? new Color(.6f, .35f, .12f)
                    : flow.Progress.IsCompleted(level.id) ? new Color(.12f, .42f, .32f) : new Color(.17f, .36f, .42f);
            }
        }
        private void ShowPreview(string id)
        {
            if (preview || flow.Loading) return;
            var level = flow.Catalog.levels.First(l => l.id == id);
            preview = CampaignUI.Box(root, "Preview", 0, 0, 0, 0, new Color(0, 0, 0, .65f)); CampaignUI.Stretch(preview);
            var window = CampaignUI.Box(preview, "Local Preview", 0, 0, 1000, 680, new Color(.9f, .94f, .95f));
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); window.anchoredPosition = Vector2.zero;
            CampaignUI.Label(window, level.title, 32, 24, 936, 48, 30);
            var image = CampaignUI.Picture(window, level.preview ? level.preview : flow.Catalog.anatomy, 32, 100, 500, 450);
            if (!level.preview)
            {
                float x = Mathf.Clamp(level.bodyPosition.x - .15f, 0, .7f), y = Mathf.Clamp(level.bodyPosition.y - .2f, 0, .6f);
                image.uvRect = new Rect(x, y, .3f, .4f); image.color = new Color(.75f, 1f, .9f);
                CampaignUI.Label(window, "局部扫描", 32, 560, 500, 50, 16);
            }
            CampaignUI.Label(window, "＋", 250, 270, 70, 90, 60);
            var description = CampaignUI.Label(window, level.description, 570, 110, 390, 400, 22);
            description.overflowMode = TextOverflowModes.Ellipsis;
            var enter = CampaignUI.Button(window, "进入关卡", 570, 540, 390, () => { });
            enter.onClick.RemoveAllListeners();
            enter.onClick.AddListener(() =>
            {
                if (flow.Loading) return;
                enter.interactable = false; description.text = "正在读取关卡……";
                flow.EnterLevel(id, error => { if (!description) return; description.text = error; enter.interactable = true; });
            });
            CampaignUI.Button(window, "返回人体图", 32, 614, 240, () =>
            {
                if (flow.Loading) return;
                preview.gameObject.SetActive(false); Destroy(preview.gameObject); preview = null;
            });
        }
    }
}
