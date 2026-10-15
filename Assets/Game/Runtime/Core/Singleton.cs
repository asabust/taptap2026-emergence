using UnityEngine;

namespace Game.Runtime.Core
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool _initialized = false;
        private static bool _applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting) return null;

                if (!_initialized)
                {
                    _instance = FindObjectOfType<T>();
                    _initialized = true;

                    if (_instance == null)
                        Debug.LogError($"[Singleton] {typeof(T)} instance not found in scene!");
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance is null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit() => _applicationIsQuitting = true;
    }
}