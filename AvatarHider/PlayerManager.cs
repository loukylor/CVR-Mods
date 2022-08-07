using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using AvatarHider.DataTypes;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AvatarHider
{
    public static class PlayerManager
    {
        public static Dictionary<string, AvatarHiderPlayer> players = new Dictionary<string, AvatarHiderPlayer>();
        public static Dictionary<string, AvatarHiderPlayer> filteredPlayers = new Dictionary<string, AvatarHiderPlayer>();

        private static readonly FieldInfo playerDescriptorField = typeof(PuppetMaster).GetField("_playerDescriptor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo moderationIndexField = typeof(CVRSelfModerationManager).GetField("_moderationIndex", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Init()
        {
            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(PuppetMaster).GetMethod(nameof(PuppetMaster.AvatarInstantiated)),
                postfix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnAvatarChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(PlayerDescriptor).GetConstructors()[0],
                prefix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnPlayerJoin), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(CVRPlayerEntity).GetMethod(nameof(CVRPlayerEntity.Recycle)),
                prefix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnPlayerLeave), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(Friends).GetMethod(nameof(Friends.ReloadFriends)),
                prefix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnFriendsChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(CVRSelfModerationManager).GetMethod(nameof(CVRSelfModerationManager.SetPlayerAvatarVisibility)),
                postfix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnPlayerModerationChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(CVRSelfModerationManager).GetMethod(nameof(CVRSelfModerationManager.ResetPlayerAvatarVisibility)),
                postfix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnPlayerModerationChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(CVRSelfModerationManager).GetMethod(nameof(CVRSelfModerationManager.SetAvatarVisibility)),
                postfix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnAvatarModerationChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );

            AvatarHiderMod.Instance.HarmonyInstance.Patch(
                typeof(CVRSelfModerationManager).GetMethod(nameof(CVRSelfModerationManager.ResetAvatarVisibility)),
                postfix: typeof(PlayerManager).GetMethod(nameof(PlayerManager.OnAvatarModerationChanged), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );
        }

        public static void OnSceneWasLoaded()
        {
            players.Clear();
            filteredPlayers.Clear();
        }

        private static void OnAvatarChanged(PuppetMaster __instance)
        {
            string uuid = ((PlayerDescriptor)playerDescriptorField.GetValue(__instance)).ownerId;
            if (!players.ContainsKey(uuid)) return;

            players[uuid].SetAvatar(__instance.avatarObject);
            if (filteredPlayers.ContainsKey(uuid))
                RefreshManager.RefreshPlayer(players[uuid], PlayerSetup.Instance.transform.position);
            else
                if (Config.IncludeHiddenAvatars.Value && players[uuid].shownStatus == false)
                    players[uuid].SetInActive();
        }

        private static void OnPlayerJoin(PlayerDescriptor __0)
        {
            MelonCoroutines.Start(OnPlayerJoinDelay(__0));
        }
        private static IEnumerator OnPlayerJoinDelay(PlayerDescriptor player)
        {
            yield return new WaitForEndOfFrame();

            if (player == null)
                yield break;

            if (players.ContainsKey(player.ownerId))
                yield break;

            AvatarHiderPlayer playerProp = new AvatarHiderPlayer()
            {
                active = true,
                Uuid = player.ownerId,
                avatarId = player.avtrId,
                player = player,
                avatar = player.transform.GetChild(player.transform.childCount - 1).gameObject,
                isFriend = Friends.FriendsWith(player.ownerId),
                shownStatus = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(player.ownerId, player.avtrId)
            };

            players.Add(playerProp.Uuid, playerProp);
            HideOrShowAvatar(playerProp);
            RefreshFilteredList();
        }

        private static void OnPlayerLeave(CVRPlayerEntity __0)
        {
            players.Remove(__0.Uuid);
            filteredPlayers.Remove(__0.Uuid);
        }

        private static void OnFriendsChanged()
        {
            foreach (Friend_t changedFriend in Friends.DeltaList)
            {
                if (players.ContainsKey(changedFriend.UserId))
                {
                    players[changedFriend.UserId].isFriend = true;
                }
            }
            RefreshFilteredList();
            RefreshManager.Refresh();
        }

        private static void OnPlayerModerationChanged(string __0)
        {
            if (!players.ContainsKey(__0))
                return;

            players[__0].shownStatus = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(players[__0].Uuid, players[__0].avatarId);

            RefreshFilteredList();
            RefreshManager.Refresh();
        }

        private static void OnAvatarModerationChanged(string __0)
        {
            bool changed = false;

            foreach (AvatarHiderPlayer player in players.Values)
            {
                if (player.avatarId != __0)
                    continue;

                players[__0].shownStatus = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(players[__0].Uuid, players[__0].avatarId);
                changed = true;
            }

            if (changed)
            {
                RefreshFilteredList();
                RefreshManager.Refresh();
            }
        }

        public static List<AvatarHiderPlayer> RefreshFilteredList()
        {
            ExcludeFlags excludeFlags = ExcludeFlags.None;
            if (Config.IgnoreFriends.Value)
                excludeFlags |= ExcludeFlags.Friends;
            if (Config.ExcludeShownAvatars.Value)
                excludeFlags |= ExcludeFlags.Shown;
            if (Config.IncludeHiddenAvatars.Value)
                excludeFlags |= ExcludeFlags.Hidden;

            List<AvatarHiderPlayer> removedPlayers = new List<AvatarHiderPlayer>();
            filteredPlayers.Clear();

            foreach (KeyValuePair<string, AvatarHiderPlayer> item in players)
            {
                if (((excludeFlags.HasFlag(ExcludeFlags.Friends) && item.Value.isFriend) ||
                     (excludeFlags.HasFlag(ExcludeFlags.Shown) && item.Value.shownStatus == true)) &&
                     !(excludeFlags.HasFlag(ExcludeFlags.Hidden) && item.Value.shownStatus == false))
                { 
                    removedPlayers.Add(item.Value);
                }
                else
                {
                    filteredPlayers.Add(item.Key, item.Value);
                }
            }
            return removedPlayers;
        }

        public static void HideOrShowAvatar(AvatarHiderPlayer avatarHiderPlayer)
        {
            if (Config.IncludeHiddenAvatars.Value && avatarHiderPlayer.shownStatus == false)
                avatarHiderPlayer.SetInActive();
            else if ((Config.IgnoreFriends.Value && avatarHiderPlayer.isFriend) ||
                (Config.ExcludeShownAvatars.Value && avatarHiderPlayer.shownStatus == false))
                avatarHiderPlayer.SetActive();
        }
    }
}
