#if UNITY_EDITOR
using System;
using System.IO;
using Game.Runtime.Gameplay.Campaign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class ChapterScanSetup
    {
        private const string UI = "Assets/Resources/UI/";
        private const string Art = "Assets/Arts/UI/LevelSelect/";
        [MenuItem("Tools/UI/Preview Chapter Selection (Play Mode)")]
        public static void Preview()
        {
            if (!EditorApplication.isPlaying || !CampaignRuntime.Instance)
                throw new InvalidOperationException("请进入 Play Mode 后预览。");
            CampaignRuntime.Instance.UI.ShowSelection();
        }
        [MenuItem("Tools/UI/Configure Chapter X-Ray Scan")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play Mode。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("请保存并退出 Prefab Mode 后执行，避免覆盖未保存的编辑。");
            var skeleton = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "选关界面上层线稿骨骼图.png");
            var anatomy = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "选关界面下层无关卡版.png");
            if (!skeleton || !anatomy) throw new InvalidOperationException("两张背景图必须已导入为 Sprite。");
            string mapPath = UI + "LevelSelectPanel.prefab";
            var map = PrefabUtility.LoadPrefabContents(mapPath);
            try
            {
                var image = map.transform.Find("Anatomy").GetComponent<Image>();
                image.sprite = skeleton; image.color = Color.white;
                PrefabUtility.SaveAsPrefabAsset(map, mapPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(map); }
            string chapterPath = UI + "ChapterSelectPanel.prefab";
            var chapter = PrefabUtility.LoadPrefabContents(chapterPath);
            try
            {
                var view = chapter.GetComponent<CampaignPanelView>();
                if (!view.chapterScan)
                {
                    // Replace only the legacy left preview. The artist's list, labels and button styles stay intact.
                    view.artwork.gameObject.SetActive(false);
                    var frame = Rect(chapter.transform, "ScanViewport", new Vector2(80,-150), new Vector2(850,850));
                    frame.gameObject.AddComponent<RectMask2D>();
                    frame.SetSiblingIndex(view.artwork.transform.GetSiblingIndex() + 1);
                    var scan = frame.gameObject.AddComponent<ChapterScanView>(); scan.viewport = frame;
                    scan.skeleton = Picture(frame,"SkeletonLayer",skeleton);
                    var lens = Rect(frame,"ScanCircle",new Vector2(425,-445),new Vector2(650,650));
                    lens.pivot = new Vector2(.5f,.5f);
                    var maskGraphic = lens.gameObject.AddComponent<ScanCircleGraphic>(); maskGraphic.raycastTarget = false;
                    var mask = lens.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
                    scan.scanCircle = lens; scan.anatomy = Picture(lens,"AnatomyLayer",anatomy);
                    var ring = Rect(frame,"ScanOutline",Vector2.zero,Vector2.zero); ring.SetParent(lens,false);
                    ring.anchorMin=Vector2.zero;ring.anchorMax=Vector2.one;ring.offsetMin=ring.offsetMax=Vector2.zero;
                    var outline = ring.gameObject.AddComponent<ScanCircleGraphic>(); outline.outlineWidth=6;outline.color=Color.white;outline.raycastTarget=false;
                    // Reticle overlays the revealed image and shares the lens center.
                    var reticle = Rect(frame,"Reticle",Vector2.zero,Vector2.zero);reticle.SetParent(lens,false);
                    reticle.anchorMin=reticle.anchorMax=reticle.pivot=new Vector2(.5f,.5f);reticle.sizeDelta=new Vector2(130,130);reticle.anchoredPosition=Vector2.zero;
                    var cross = reticle.gameObject.AddComponent<ScanCircleGraphic>();cross.outlineWidth=4;cross.color=Color.white;cross.raycastTarget=false;
                    Bar(reticle,"CrosshairHorizontal",new Vector2(160,4));Bar(reticle,"CrosshairVertical",new Vector2(4,160));
                    view.chapterScan = scan;
                    // Top-right navigation supersedes the earlier top-left draft.
                    var back=(RectTransform)view.back.transform;back.anchorMin=back.anchorMax=back.pivot=new Vector2(1,1);
                    back.anchoredPosition=new Vector2(-60,-35);
                    if(view.progress){view.progress.rectTransform.anchoredPosition=new Vector2(1380,-50);}
                }
                foreach (var graphic in chapter.GetComponentsInChildren<ScanCircleGraphic>(true))
                    if (!graphic.GetComponent<CanvasRenderer>()) graphic.gameObject.AddComponent<CanvasRenderer>();
                PrefabUtility.SaveAsPrefabAsset(chapter,chapterPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(chapter); }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("PASS: skeleton overview and aligned circular X-ray scan configured. Existing right-side UI preserved.");
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        private static Image Picture(Transform parent,string name,Sprite sprite)
        {
            var r=Rect(parent,name,Vector2.zero,new Vector2(425,850));r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=false;image.raycastTarget=false;return image;
        }
        private static void Bar(Transform parent,string name,Vector2 size)
        {
            var r=Rect(parent,name,Vector2.zero,size);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            var image=r.gameObject.AddComponent<Image>();image.color=Color.white;image.raycastTarget=false;
        }
        [MenuItem("Tools/UI/Validate Chapter X-Ray Scan")]
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UI+"ChapterSelectPanel.prefab");
            var instance=UnityEngine.Object.Instantiate(prefab);int checks=0;
            try
            {
                var scan=instance.GetComponent<CampaignPanelView>().chapterScan;
                if(!scan||!scan.scanCircle.GetComponent<Mask>()||scan.scanCircle.GetComponent<Mask>().showMaskGraphic)throw new Exception("Scan stencil missing.");
                var a=scan.skeleton.sprite.rect;var b=scan.anatomy.sprite.rect;
                if(Mathf.Abs(a.width/a.height-b.width/b.height)>.001f)throw new Exception("Source images must share an aspect ratio.");
                foreach(var point in new[]{new Vector2(.4f,.35f),new Vector2(.6f,.53f),new Vector2(.52f,.61f),new Vector2(.59f,.73f),new Vector2(.53f,.91f)})
                {
                    scan.previewFocus=point;
                    scan.Focus(point);
                    var first=scan.skeleton.rectTransform;var second=scan.anatomy.rectTransform;
                    foreach(var uv in new[]{Vector2.zero,new Vector2(.5f,.5f),Vector2.one})
                    {
                        Vector2 p=Vector2.Scale(uv-new Vector2(.5f,.5f),first.rect.size);
                        if((first.TransformPoint(p)-second.TransformPoint(p)).sqrMagnitude>.001f)throw new Exception("Layer alignment mismatch.");
                        checks++;
                    }
                    Vector2 focusLocal=Vector2.Scale(point-new Vector2(.5f,.5f),first.rect.size);
                    if((first.TransformPoint(focusLocal)-scan.scanCircle.TransformPoint(scan.scanCircle.rect.center)).sqrMagnitude>.001f)
                        throw new Exception("Chapter position does not align to lens center.");
                }
                Debug.Log("PASS: circular stencil and "+checks+" paired layer alignment checks; five chapter positions.");
                File.WriteAllText("Temp/ChapterScanValidation.txt","PASS "+checks);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
#endif
