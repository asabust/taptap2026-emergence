using System;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.Levels;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    public static class BoardPresentationChecks
    {
        private static int checks;
        private static void Check(bool value,string message) { if(!value) throw new Exception("FAILED: "+message); checks++; }
        public static int Run()
        {
            checks=0;
            var level=new LevelDefinition { grid=new GridSize { width=30,height=30 } };
            level.tiles.Add(new LevelTile { x=12,y=7,type=LevelIds.Floor });
            level.tiles.Add(new LevelTile { x=16,y=10,type=LevelIds.Wall });
            string original=LevelJson.Serialize(level);
            var frame=LevelBoardFrame.FromDefinition(level,true,.7f);
            Check(frame.OriginX==12 && frame.OriginY==7 && frame.Width==5 && frame.Height==4,"frame fits terrain including walls");
            bool centers=true;
            for(int y=7;y<=10;y++) for(int x=12;x<=16;x++)
            {
                float u=(x-12+.5f+.7f)/frame.DisplayWidth, v=(y-7+.5f+.7f)/frame.DisplayHeight;
                centers &= frame.TryPick(u,v,out int pickedX,out int pickedY) && pickedX==x && pickedY==y;
            }
            Check(centers,"all framed cell centers preserve original logical coordinates");
            Check(!frame.TryPick(0,.5f,out _,out _) && !frame.TryPick(.999f,.5f,out _,out _),"left and right shadow padding are not cells");
            Check(!frame.TryPick(.5f,0,out _,out _) && !frame.TryPick(.5f,.999f,out _,out _),"bottom and top shadow padding are not cells");
            Check(!frame.TryPick(float.NaN,.5f,out _,out _) && !frame.TryPick(.5f,float.PositiveInfinity,out _,out _),"invalid pointer input rejected");
            Check(!frame.TryPick(1,.5f,out _,out _) && !frame.TryPick(-.01f,.5f,out _,out _),"outer boundaries rejected");
            var full=LevelBoardFrame.FromDefinition(level,false,0);
            Check(full.Width==30 && full.Height==30 && full.OriginX==0 && full.OriginY==0,"diagnostic mode keeps full grid");
            Check(full.TryPick(0,0,out int zeroX,out int zeroY) && zeroX==0 && zeroY==0,"un-padded lower corner preserved");
            Check(full.TryPick(.999f,.999f,out int lastX,out int lastY) && lastX==29 && lastY==29,"30x30 far corner preserved");
            level.tiles.Clear(); level.tiles.Add(new LevelTile { x=29,y=29,type=LevelIds.Floor });
            var single=LevelBoardFrame.FromDefinition(level,true,.7f);
            Check(single.TryPick(.5f,.5f,out lastX,out lastY) && lastX==29 && lastY==29 && single.Width==1,"single tile at far corner remains centered and clickable");
            level.tiles.Clear(); var empty=LevelBoardFrame.FromDefinition(level,true,.7f);
            Check(empty.Width==30 && empty.Height==30,"empty draft has finite framing");
            level.tiles.Add(new LevelTile { x=12,y=7,type=LevelIds.Floor }); level.tiles.Add(new LevelTile { x=16,y=10,type=LevelIds.Wall });
            Check(LevelJson.Serialize(level)==original,"framing and picking never mutate source level");
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Board Presentation Checks")]
        public static void Menu() { Debug.Log("Board presentation checks passed: "+Run()); }
#endif
    }
}
