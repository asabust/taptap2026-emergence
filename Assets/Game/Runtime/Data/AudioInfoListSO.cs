using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Data
{
    [CreateAssetMenu(fileName = "AudioInfoListSO", menuName = "Audio Info List")]
    public class AudioInfoListSO : ScriptableObject
    {
        public List<AudioInf> audioInfos = new List<AudioInf>();

        public AudioInf GetAudioInfo(AudioName audioName)
        {
            return audioInfos.Find(x => x.audioName == audioName);
        }
    }


    [System.Serializable]
    public class AudioInf
    {
        public AudioName audioName;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume;
        public bool loop;
    }

    public enum AudioName
    {
        None,
        Click,
        BGM,
    }
}