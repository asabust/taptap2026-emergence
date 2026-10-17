using System;
using System.IO;
using System.Linq;
using Game.Runtime.Gameplay.LevelEditor;
using Game.Runtime.Gameplay.Levels;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    public static class LevelPlaytestChecks
    {
        private static int checks;
        private static void Check(bool success,string name)
        { if(!success) throw new Exception("FAILED: "+name); checks++; }
        private static void Clone(LevelPlaySession play)
        {
            if(!play.TryBeginClonePlacement(out var message) || !play.TryCreateClone(1,0,out message)) throw new Exception(message);
        }
        public static int Run()
        {
            checks=0;
#if UNITY_5_3_OR_NEWER
            string path=Path.Combine(Application.dataPath,"StreamingAssets/Levels/stage6_probe.json");
#else
            string path="Assets/StreamingAssets/Levels/stage6_probe.json";
#endif
            var fixture=LevelJson.Deserialize(File.ReadAllText(path));
            Check(LevelValidator.ValidateForPlay(fixture).CanPlay,"committed probe is valid");
            var editor=new LevelEditorSession(); editor.ReplaceDraft(fixture);
            // A valid, never-saved draft; no repository read/write takes place during playtesting.
            editor.Draft.id="unsaved_probe";
            using(var document=new LevelEditorDocument(editor,new LevelRepository(Path.Combine(Path.GetTempPath(),"unused-playtest-checks"))))
            {
                string original=LevelJson.Serialize(editor.Draft);
                var candidate=LevelJson.DeepCopy(editor.Draft);
                var play=new LevelPlaySession(candidate);
                Check(document.SavedId==null && document.IsDirty,"play accepts never-saved draft");
                Check(play.Status==LevelPlayStatus.Playing && play.Iteration==0,"probe is not initially won");
                Clone(play);
                Check(play.LivingCloneCount==1 && play.Iteration==0 && play.State.Cells.Count==3,"probe clone added without iterating");
                Check(play.TryIterate(out _) && play.Iteration==1,"probe first iteration succeeds");
                Check(play.State.Cells.Count==2 && play.State.Cells.Any(c=>c.X==0 && c.Y==0 && !c.IsClone)
                    && play.State.Cells.Any(c=>c.X==1 && c.Y==0 && c.IsClone),"probe protects initial cell and clone while blocker dies");
                Check(play.TryMove(1,0,out _) && play.TryMove(1,0,out _) && play.TryMove(1,0,out _)
                    && play.Status==LevelPlayStatus.Won && play.LivingCloneCount==1,"recorded probe sequence wins after three right moves");
                Check(LevelJson.Serialize(editor.Draft)==original && document.IsDirty && document.SavedId==null,"playing cannot alter draft or file identity");
                Check(play.TryUndo(out _) && play.State.PlayerX==0 && play.Iteration==0 && play.State.Cells.Count==3,"probe undo restores blocker, clone and original player");
                play.Reset();
                Check(play.State.Cells.Count==2 && play.LivingCloneCount==0 && play.Iteration==0 && !play.CanUndo,"probe reset uses play-start copy");
                candidate.cells.Clear(); candidate.winConditions.conditions.Clear(); play.Reset();
                Check(play.State.Cells.Count==2 && play.Status==LevelPlayStatus.Playing,"play initial copy independent of candidate");
                var noClone=new LevelPlaySession(fixture);
                Check(noClone.TryMove(1,0,out _) && !noClone.TryMove(1,0,out _),"blocker prevents walking directly to exit");
                var wrongPosition=new LevelPlaySession(fixture); Clone(wrongPosition); wrongPosition.TryMove(1,0,out _); wrongPosition.TryIterate(out _);
                Check(wrongPosition.State.Cells.Any(c=>c.X==2 && c.Y==1),"standing next to blocker protects it instead of opening path");
                for(int i=0;i<5;i++)
                {
                    var repeat=new LevelPlaySession(editor.Draft); Clone(repeat); repeat.TryIterate(out _); repeat.Reset();
                }
                Check(LevelJson.Serialize(editor.Draft)==original && editor.SelectedGoalIndex==0,"repeated play sessions preserve draft and selected goal");
                var invalid=LevelJson.DeepCopy(editor.Draft); invalid.winConditions.conditions.Clear(); bool rejected=false;
                try { new LevelPlaySession(invalid); } catch(ArgumentException) { rejected=true; }
                Check(rejected && LevelJson.Serialize(editor.Draft)==original,"invalid play candidate cannot clear editor draft");
            }
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Playtest Checks")]
        public static void RunInEditor() { Debug.Log("Level playtest checks passed: "+Run()); }

#if UNITY_5_3_OR_NEWER
        [MenuItem("Tools/Levels/Check Organic Shader")]
        public static void CheckShader()
        {
            var shader=Resources.Load<Shader>("LevelPlay/OrganicLevelBoard");
            if(!shader || !shader.isSupported) throw new InvalidOperationException("Organic level shader missing or unsupported.");
            var material=new Material(shader);
            try
            {
                if(!material.SetPass(0)) throw new InvalidOperationException("Organic level shader pass could not be compiled.");
                var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                if(errors.Length>0) throw new InvalidOperationException(string.Join("\n",errors.Select(m=>m.message)));
                Debug.Log("Organic level shader checks passed. Please also verify the visual result in Play Mode.");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
#endif
#endif
    }
}
