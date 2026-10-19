using System;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.LevelEditor;
using Game.Runtime.Gameplay.Levels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    /// <summary>OrganicGridDemo membrane/nuclei look, driven only by the shared rules session.</summary>
    [RequireComponent(typeof(RawImage), typeof(AspectRatioFitter))]
    public sealed class OrganicLevelBoardView : MonoBehaviour, IPointerClickHandler
    {
        [Range(.02f,.5f)] public float fusion = .3f;
        public bool styledTerrain;
        public const float TerrainPadding = .7f;
        public event Action<int,int> CellClicked;
        public event Action AnimationCompleted;
        public bool Paused { get; set; }
        public bool IsAnimating => elapsed < duration;
        private Material material;
        private Texture2D lifeTexture, boardTexture;
        private Color[] lifePixels, boardPixels;
        private LevelState previous;
        private int width, height;
        private float elapsed, duration;
        private float flowTime;
        private LevelBoardFrame frame;
        private Vector2 playerFrom, playerTo;

        public void Render(LevelDefinition definition, LevelPlaySession session, bool animate)
        {
            var state = session.State;
            if (!material || width != definition.grid.width || height != definition.grid.height)
            {
                Rebuild(definition.grid.width, definition.grid.height);
                previous = null;
            }
            frame = LevelBoardFrame.FromDefinition(definition, styledTerrain, styledTerrain ? TerrainPadding : 0);
            material.SetVector("_ViewBounds", new Vector4(frame.OriginX, frame.OriginY, frame.Width, frame.Height));
            GetComponent<AspectRatioFitter>().aspectRatio = frame.DisplayWidth / frame.DisplayHeight;
            bool changed = previous == null || !SameState(previous,state);
            if (changed)
            {
                Array.Clear(lifePixels,0,lifePixels.Length);
                var from = previous ?? state;
                foreach (var cell in from.Cells)
                {
                    int i = cell.X + cell.Y * width; lifePixels[i].r = 1; lifePixels[i].b = cell.IsClone ? 1 : 0;
                }
                foreach (var cell in state.Cells)
                {
                    int i = cell.X + cell.Y * width; lifePixels[i].g = 1; lifePixels[i].a = cell.IsClone ? 1 : 0;
                }
                playerFrom = new Vector2(from.PlayerX,from.PlayerY); playerTo = new Vector2(state.PlayerX,state.PlayerY);
                bool move = playerFrom != playerTo;
                duration = previous != null && animate ? move ? .2f : .55f : 0;
                elapsed = 0;
                lifeTexture.SetPixels(lifePixels); lifeTexture.Apply(false,false);
                previous = state;
            }
            Array.Clear(boardPixels,0,boardPixels.Length);
            foreach (var tile in definition.tiles) boardPixels[tile.x + tile.y*width].r = tile.type == LevelIds.Floor ? 1 : .5f;
            foreach (var goal in definition.winConditions.conditions)
            {
                if (goal.type == LevelIds.ActorAtPosition) boardPixels[goal.position.x+goal.position.y*width].g = 1;
                if (goal.type == LevelIds.CoverPositions)
                    foreach (var p in goal.positions) boardPixels[p.x+p.y*width].b = 1;
            }
            if (session.Mode == LevelInteractionMode.PlaceClone)
                for (int y=0;y<height;y++) for(int x=0;x<width;x++)
                    if (session.CanPlaceCloneAt(x,y)) boardPixels[x+y*width].a = 1;
            if (session.Mode == LevelInteractionMode.RecycleClone)
                foreach (var cell in state.Cells) if(cell.IsClone) boardPixels[cell.X+cell.Y*width].a = 1;
            boardTexture.SetPixels(boardPixels); boardTexture.Apply(false,false);
            Upload();
        }

        private void Update()
        {
            if (!material || Paused) return;
            if (styledTerrain) { flowTime += Time.unscaledDeltaTime; material.SetFloat("_FlowTime", flowTime); }
            if (!IsAnimating) return;
            elapsed = Mathf.Min(duration,elapsed+Time.unscaledDeltaTime);
            Upload();
            if (!IsAnimating) AnimationCompleted?.Invoke();
        }
        private void Upload()
        {
            float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed/duration);
            var player = Vector2.Lerp(playerFrom,playerTo,Mathf.SmoothStep(0,1,t));
            material.SetFloat("_Progress",t); material.SetFloat("_Fusion",fusion);
            material.SetVector("_Player",new Vector4(player.x,player.y,.46f,0));
            material.SetVector("_PlayerPrevious",new Vector4(playerFrom.x,playerFrom.y,0,0));
            material.SetVector("_PlayerTail",new Vector4(playerFrom.x,playerFrom.y,playerFrom != playerTo ? (1-t)*.4f : 0,0));
        }
        private void Rebuild(int w,int h)
        {
            Release(); width=w; height=h;
            var shader = Resources.Load<Shader>("LevelPlay/OrganicLevelBoard");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("OrganicLevelBoard Shader unavailable or unsupported.");
            material = new Material(shader) { name = "Organic Play Board (Runtime)" };
            lifeTexture = Texture(w,h); boardTexture=Texture(w,h);
            lifePixels = new Color[w*h]; boardPixels = new Color[w*h];
            material.SetTexture("_LifeTex",lifeTexture); material.SetTexture("_BoardTex",boardTexture);
            material.SetVector("_GridSize",new Vector4(w,h,0,0));
            material.SetFloat("_StyledTerrain", styledTerrain ? 1 : 0);
            material.SetFloat("_BoardPadding", styledTerrain ? TerrainPadding : 0);
            GetComponent<RawImage>().material=material;
            GetComponent<AspectRatioFitter>().aspectRatio=styledTerrain ? (w + 2 * TerrainPadding) / (h + 2 * TerrainPadding) : (float)w/h;
        }
        private static Texture2D Texture(int w,int h)
        { return new Texture2D(w,h,TextureFormat.RGBA32,false,true) { filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp }; }
        private static bool SameState(LevelState a,LevelState b)
        {
            if(a.PlayerX!=b.PlayerX || a.PlayerY!=b.PlayerY || a.Iteration!=b.Iteration || a.Cells.Count!=b.Cells.Count) return false;
            for(int i=0;i<a.Cells.Count;i++)
                if(a.Cells[i].X!=b.Cells[i].X || a.Cells[i].Y!=b.Cells[i].Y || a.Cells[i].IsClone!=b.Cells[i].IsClone) return false;
            return true;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || width==0) return;
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out var point)) return;
            var bounds=rect.rect;
            if(bounds.width<=0 || bounds.height<=0) return;
            float nx = (point.x-bounds.xMin)/bounds.width, ny = (point.y-bounds.yMin)/bounds.height;
            if(frame.TryPick(nx,ny,out int x,out int y))
                CellClicked?.Invoke(x,y);
        }
        private void OnDestroy() { Release(); }
        private void Release()
        {
            if(material) Destroy(material);
            if(lifeTexture) Destroy(lifeTexture);
            if(boardTexture) Destroy(boardTexture);
        }
    }
}
