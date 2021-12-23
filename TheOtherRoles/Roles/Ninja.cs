using System.Net;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using TheOtherRoles.Objects;
using static TheOtherRoles.GameHistory;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;
using TheOtherRoles.Patches;

namespace TheOtherRoles
{
    [HarmonyPatch]
    public class Ninja : Role<Ninja> { 

        public static Color color = Palette.ImpostorRed;

        public static float stealthCooldown = 10f;
        public static float stealthDuration = 10f;
        public static float killPenalty = 10f;
        public static float speedBonus = 1.25f;
        public static float fadeTime = 0.5f;
        public static bool canUseVents = false;
        public static bool canBeTargeted = true;

        public bool penalized = false;
        public bool stealthed = false;
        public DateTime stealthedAt = DateTime.UtcNow;

        public Ninja()
        {
            roleType = RoleId.Ninja;
            penalized = false;
            stealthed = false;
            stealthedAt = DateTime.UtcNow;
        }

        public static void OnMeetingStart()
        {
            foreach (var ninja in players)
            {
                ninja.stealthed = false;
            }
        }

        public static void OnMeetingEnd()
        {
            foreach (var ninja in players)
            {
                if (ninja.player == PlayerControl.LocalPlayer && ninja.penalized)
                {
                    ninja.player.SetKillTimer(PlayerControl.GameOptions.KillCooldown + killPenalty);
                }
                else
                {
                    ninja.player.SetKillTimer(PlayerControl.GameOptions.KillCooldown);
                }
            }
        }

        public static void FixedUpdate()
        {
        }

        public static bool isStealthed(PlayerControl player)
        {
            if (isRole(player))
            {
                Ninja n = players.First(x => x.player == player);
                return n.stealthed;
            }
            return false;
        }

        public static float stealthFade(PlayerControl player)
        {
            if (isRole(player) && fadeTime > 0f)
            {
                Ninja n = players.First(x => x.player == player);
                return Math.Min(1.0f, (float)(DateTime.UtcNow - n.stealthedAt).TotalSeconds / fadeTime);
            }
            return 1.0f;
        }

        public static bool isPenalized(PlayerControl player)
        {
            if (isRole(player))
            {
                Ninja n = players.First(x => x.player == player);
                return n.penalized;
            }
            return false;
        }

        public static void setStealthed(PlayerControl player, bool stealthed = true)
        {
            if (isRole(player))
            {
                Ninja n = players.First(x => x.player == player);
                n.stealthed = stealthed;
                n.stealthedAt = DateTime.UtcNow;
            }
        }

        public static void onKill(PlayerControl player)
        {
            if (isRole(player))
            {
                Ninja n = players.First(x => x.player == player);
                n.penalized = n.stealthed;

                float penalty = n.penalized ? killPenalty : 0f;
                player.SetKillTimer(PlayerControl.GameOptions.KillCooldown + penalty);
            }
        }

        private static Sprite buttonSprite;
        public static Sprite getButtonSprite()
        {
            if (buttonSprite) return buttonSprite;
            buttonSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.NinjaButton.png", 115f);
            return buttonSprite;
        }

        public static void clearAndReload()
        {
            players = new List<Ninja>();
            stealthCooldown = CustomOptionHolder.ninjaStealthCooldown.getFloat();
            stealthDuration = CustomOptionHolder.ninjaStealthDuration.getFloat();
            killPenalty = CustomOptionHolder.ninjaKillPenalty.getFloat();
            speedBonus = CustomOptionHolder.ninjaSpeedBonus.getFloat() / 100f;
            fadeTime = CustomOptionHolder.ninjaFadeTime.getFloat();
            canUseVents = CustomOptionHolder.ninjaCanVent.getBool();
            canBeTargeted = CustomOptionHolder.ninjaCanBeTargeted.getBool();
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        public static class PlayerPhysicsNinjaPatch
        {
            public static void Postfix(PlayerPhysics __instance)
            {
                if (__instance.AmOwner && __instance.myPlayer.CanMove && GameData.Instance && isStealthed(__instance.myPlayer))
                {
                    __instance.body.velocity *= speedBonus;
                }

                if (isRole(__instance.myPlayer))
                {
                    var ninja = __instance.myPlayer;
                    if (ninja == null) return;

                    var opacity = (PlayerControl.LocalPlayer.Data.Role.IsImpostor || PlayerControl.LocalPlayer.Data.IsDead) ? 0.1f : 0.0f;

                    if (isStealthed(ninja))
                    {
                        opacity = Math.Max(opacity, 1.0f - stealthFade(ninja));
                        ninja.myRend.material.SetFloat("_Outline", 0f);
                    }
                    else
                    {
                        opacity = Math.Max(opacity, stealthFade(ninja));
                    }

                    // Sometimes it just doesn't work?
                    try
                    {
                        var color = Color.Lerp(Palette.ClearWhite, Palette.White, opacity);
                        if (ninja.MyPhysics?.rend != null)
                        {
                            ninja.MyPhysics.rend.color = color;
                        }
                        ninja.MyPhysics?.Skin?.layer?.material?.SetColor("_Color", color);
                        ninja.HatRenderer?.BackLayer?.material?.SetColor("_Color", color);
                        ninja.HatRenderer?.FrontLayer?.material?.SetColor("_Color", color);
                        ninja.CurrentPet?.rend?.material?.SetColor("_Color", color);
                        ninja.CurrentPet?.shadowRend?.material?.SetColor("_Color", color);
                        if (ninja.VisorSlot != null)
                        {
                            ninja.VisorSlot.color = color;
                        }
                    }
                    catch { }
                }
            }
        }
    }
}