using System.Collections;
using Game.Runtime.Core.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime.Core
{
    public class TransitionManager : Singleton<TransitionManager>
    {
        public float fadeDuration;
        public bool IsTrasition { get; private set; }

        private string currentSceneName;
        private CanvasGroup canvasGroup;

        protected override void Awake()
        {
            base.Awake();
            canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (!canvasGroup)
            {
                Debug.LogWarning("TransitionManager 子物体中没找到 canvasGroup 遮罩层");
            }
        }

        /// <summary>
        /// 切换游戏场景（只考虑目的地）
        /// </summary>
        /// <param name="toSceneName">目标场景</param>
        public void TransitionTo(string toSceneName)
        {
            Transition(currentSceneName, toSceneName);
        }

        /// <summary>
        /// 切换游戏场景
        /// </summary>
        /// <param name="fromSceneName">需要卸载的场景</param>
        /// <param name="toSceneName">需要加载的场景</param>
        public void Transition(string fromSceneName, string toSceneName)
        {
            if (!IsTrasition)
                StartCoroutine(TransitionToScene(fromSceneName, toSceneName));
        }

        private IEnumerator TransitionToScene(string fromSceneName, string toSceneName)
        {
            Debug.Log($"开始切换场景 {fromSceneName} -> {toSceneName}");
            IsTrasition =  true;
            yield return Fade(1);
            if (!string.IsNullOrEmpty(fromSceneName))
            {
                EventHandler.CallBeforeSceneUnloadEvent(fromSceneName);
                yield return SceneManager.UnloadSceneAsync(fromSceneName);
            }

            yield return SceneManager.LoadSceneAsync(toSceneName, LoadSceneMode.Additive);

            currentSceneName = toSceneName;
            Scene newScene = SceneManager.GetSceneByName(toSceneName);
            SceneManager.SetActiveScene(newScene);

            EventHandler.CallAfterSceneLoadEvent(toSceneName);
            yield return Fade(0);
            IsTrasition = false;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            canvasGroup.blocksRaycasts = true;

            float speed = Mathf.Abs(canvasGroup.alpha - targetAlpha) / fadeDuration;
            while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
                yield return null;
            }

            canvasGroup.blocksRaycasts = false;
            
        }
    }
}