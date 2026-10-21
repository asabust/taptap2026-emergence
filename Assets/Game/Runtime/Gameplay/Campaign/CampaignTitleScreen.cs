using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Scene-owned presentation; authored TitleScene remains available for later art edits.</summary>
    public sealed class CampaignTitleScreen : MonoBehaviour
    {
        private Sprite logoSprite;
        private void OnDestroy() { if (logoSprite) Destroy(logoSprite); }
        public void Build(CampaignRuntime flow)
        {
            var root = CampaignUI.Canvas(transform, "Title Presentation");
            var art = flow.Art;
            var bg = CampaignUI.Box(root, "Background", 0, 0, 0, 0, new Color(.92f,.94f,.95f)); CampaignUI.Stretch(bg);
            if (art && art.title) CampaignUI.FittedPicture(root, art.title, true);
            if (art && art.titleLogo)
            {
                var logo = CampaignUI.Box(root, "Title Logo", 0, 0, 1000, 440, Color.white);
                logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(.5f,.5f);
                logo.anchoredPosition = new Vector2(0,100);
                var image = logo.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
                logoSprite = Sprite.Create(art.titleLogo, new Rect(0,0,art.titleLogo.width,art.titleLogo.height), new Vector2(.5f,.5f));
                image.sprite = logoSprite;
            }
            else CampaignUI.Label(root, "格命", 560, 230, 400, 140, 80);
            var menu = CampaignUI.Box(root, "Title Actions", 0, 0, 320, 220, Color.clear);
            menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(.5f,0);
            menu.anchoredPosition = new Vector2(0,50);
            CampaignUI.Button(menu, flow.Progress != null && flow.Catalog.levels.Exists(l => flow.Progress.IsCompleted(l.id)) ? "继续游戏" : "开始游戏", 0, 0, 320, flow.BeginGame);
            CampaignUI.Button(menu, "设置", 0, 66, 320, flow.ShowSettings);
            CampaignUI.Button(menu, "退出", 0, 132, 320, flow.ConfirmQuit);
        }
    }
}
