using System;
using Game.Runtime.Data.Levels;

namespace Game.Runtime.Gameplay.Levels
{
    /// <summary>Display framing only. Logical coordinates and the level definition remain unchanged.</summary>
    public readonly struct LevelBoardFrame
    {
        public readonly int OriginX, OriginY, Width, Height;
        public readonly float Padding;
        public float DisplayWidth => Width + Padding * 2;
        public float DisplayHeight => Height + Padding * 2;
        private LevelBoardFrame(int x, int y, int width, int height, float padding)
        { OriginX=x; OriginY=y; Width=width; Height=height; Padding=padding; }
        public static LevelBoardFrame FromDefinition(LevelDefinition definition, bool crop, float padding)
        {
            if (definition?.grid == null || definition.grid.width < 1 || definition.grid.height < 1
                || float.IsNaN(padding) || float.IsInfinity(padding) || padding < 0) throw new ArgumentException("Invalid board frame.");
            int minX=0, minY=0, maxX=definition.grid.width-1, maxY=definition.grid.height-1;
            if(crop && definition.tiles != null && definition.tiles.Count > 0)
            {
                minX=minY=int.MaxValue; maxX=maxY=int.MinValue;
                foreach(var tile in definition.tiles)
                { minX=Math.Min(minX,tile.x); minY=Math.Min(minY,tile.y); maxX=Math.Max(maxX,tile.x); maxY=Math.Max(maxY,tile.y); }
            }
            return new LevelBoardFrame(minX,minY,maxX-minX+1,maxY-minY+1,padding);
        }
        public bool TryPick(float u,float v,out int x,out int y)
        {
            x=y=-1;
            if(float.IsNaN(u) || float.IsNaN(v) || u<0 || v<0 || u>=1 || v>=1) return false;
            float localX=u*DisplayWidth-Padding, localY=v*DisplayHeight-Padding;
            if(localX<0 || localY<0 || localX>=Width || localY>=Height) return false;
            x=OriginX+(int)Math.Floor(localX); y=OriginY+(int)Math.Floor(localY); return true;
        }
    }
}
