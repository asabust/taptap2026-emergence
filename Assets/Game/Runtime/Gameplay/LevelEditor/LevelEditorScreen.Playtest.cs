using System;
using System.Linq;
using Game.Runtime.Gameplay.LevelPlay;
using Game.Runtime.Gameplay.Levels;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.Gameplay.LevelEditor
{
    public sealed partial class LevelEditorScreen
    {
        private GameObject playtestObject;

        private void BeginPlaytest()
        {
            if (modal || playtestObject) return;
            // Do not resize implicitly, write JSON, or modify the editor's saved identity.
            if (!PrepareSave()) return;
            var candidate = LevelJson.DeepCopy(Session.Draft);
            candidate.id = idInput.text; candidate.name = nameInput.text;
            var validation = LevelValidator.ValidateForPlay(candidate);
            if (!validation.CanPlay)
            {
                ShowDialog("无法试玩", string.Join("\n", validation.Errors.Select(e => "[错误] " + e)));
                return;
            }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            var go = new GameObject("Editor Playtest");
            go.transform.SetParent(transform, false);
            try
            {
                var screen = go.AddComponent<LevelPlayScreen>();
                screen.loadOnStart = false;
                screen.showReturnToEditor = true;
                screen.uiFont = uiFont;
                screen.Initialize(candidate);
                screen.ExitRequested += EndPlaytest;
                playtestObject = go;
                // Keep all editor controls, brush selection and scroll positions alive but inactive.
                canvasObject.SetActive(false);
            }
            catch (Exception e)
            {
                go.SetActive(false); Destroy(go);
                SetStatus("无法开始试玩：" + e.Message, true);
            }
        }

        private void EndPlaytest()
        {
            if (!playtestObject) return;
            var screen = playtestObject.GetComponent<LevelPlayScreen>();
            screen.ExitRequested -= EndPlaytest;
            playtestObject.SetActive(false); // Stop input immediately, before deferred destruction.
            Destroy(playtestObject); playtestObject = null;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            if (canvasObject) canvasObject.SetActive(true);
            // No disk reload or control rebuild: preserve draft, file identity and pending text.
            SetStatus("已返回编辑；试玩状态和历史已丢弃，原草稿保持不变。");
        }
    }
}
