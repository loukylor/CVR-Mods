using ABI_RC.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarHider.DataTypes
{
    public class AvatarHiderPlayer
    {
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            AvatarHiderPlayer objAsPlayerProp = (AvatarHiderPlayer)obj;
            if (objAsPlayerProp == null)
                return false;
            else
                return Equals(objAsPlayerProp);
        }
        public bool Equals(AvatarHiderPlayer playerProp)
        {
            return playerProp.Uuid == Uuid;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public bool active;

        public string Uuid;
        public string avatarId;
        public Vector3 Position => player.transform.position;
        public PlayerDescriptor player;
        public GameObject avatar;
        public List<AudioSource> audioSources = new List<AudioSource>();
        public bool hasLetAudioPlay;

        public bool isFriend;
        public bool? shownStatus;

        public void SetActive()
        {
            setActiveDelegate(this);
            if (!active)
                OnEnable?.Invoke(this);
            active = true;
        }
        public void SetInActive()
        {
            setInactiveDelegate(this);
            if (active)
                OnDisable?.Invoke(this);
            active = false;
        }

        public void StopAudio()
        {
            for (int i = 0; i < audioSources.Count; i++)
            { 
                if (audioSources[i] == null)
                {
                    audioSources.RemoveAt(i);
                    i -= 1;
                    continue;
                }
                audioSources[i].Stop();
            }
        }

        public void SetAvatar(GameObject avatar)
        {
            if (avatar != null)
            {
                this.avatar = avatar;
                active = false; // Do this so avatar sounds run on the first time
                hasLetAudioPlay = false;
            }
        }

        private static Action<AvatarHiderPlayer> setInactiveDelegate;
        private static readonly Action<AvatarHiderPlayer> setInactiveCompletelyDelegate = new Action<AvatarHiderPlayer>((playerProp) =>
        {
            if (playerProp.avatar != null && playerProp.avatar.activeSelf)
                playerProp.avatar.SetActive(false);
        });

        private static Action<AvatarHiderPlayer> setActiveDelegate;
        private static readonly Action<AvatarHiderPlayer> setActiveCompletelyDelegate = new Action<AvatarHiderPlayer>((playerProp) =>
        {
            if (playerProp.avatar != null && !playerProp.avatar.activeSelf)
                playerProp.avatar.SetActive(true);
        });

        public static event Action<AvatarHiderPlayer> OnEnable;
        public static event Action<AvatarHiderPlayer> OnDisable;

        public static void Init()
        {
            Config.DisableSpawnSound.OnValueChangedUntyped += OnStaticConfigChanged;
            OnStaticConfigChanged();
        }

        public static void OnStaticConfigChanged()
        {
            OnEnable = null;
            OnDisable = null;

            setActiveDelegate = setActiveCompletelyDelegate;
            setInactiveDelegate = setInactiveCompletelyDelegate;

            // If it's enabled then it was disabled before
            if (Config.DisableSpawnSound.Value)
            {
                foreach (AvatarHiderPlayer playerProp in PlayerManager.filteredPlayers.Values)
                {
                    playerProp.StopAudio();
                }

                OnEnable = new Action<AvatarHiderPlayer>((ahPlayer) =>
                {
                    ahPlayer.hasLetAudioPlay = false;
                    ahPlayer.StopAudio();
                });
            }
            else
            {
                foreach (AvatarHiderPlayer playerProp in PlayerManager.filteredPlayers.Values)
                {
                    playerProp.active = false; // Do this so avatar sounds run on the first time
                    playerProp.SetActive();
                    playerProp.hasLetAudioPlay = true;
                }

                OnEnable = new Action<AvatarHiderPlayer>((ahPlayer) =>
                {
                    if (ahPlayer.hasLetAudioPlay)
                        ahPlayer.StopAudio();
                });
                OnDisable = new Action<AvatarHiderPlayer>((ahPlayer) =>
                {
                    if (!ahPlayer.hasLetAudioPlay)
                        ahPlayer.hasLetAudioPlay = true;
                });
            }

            RefreshManager.Refresh();
        }
    }
}
