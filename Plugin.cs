using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Debug = UnityEngine.Debug;

namespace GilbyMod;

[BepInPlugin("GilbyMod", "GilbyMod", "1.0.2")]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance;

    private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();
    private static readonly Dictionary<string, FieldInfo> FieldCache = new Dictionary<string, FieldInfo>();
    private static readonly object[] backwardsParam = new object[1] { false };
    private static readonly object[] forwardsParam = new object[1] { true };

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "GilbyMod";

        public const string PLUGIN_NAME = "GilbyMod";

        public const string PLUGIN_VERSION = "1.0.2";
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            BindableAction.Create(ActionID.Slot0, Key.Digit1, "Your first item slot");
            BindableAction.Create(ActionID.Slot1, Key.Digit2, "Your second item slot");
            BindableAction.Create(ActionID.Slot2, Key.Digit3, "Your third item slot");
            BindableAction.Create(ActionID.Slot3, Key.Digit4, "Your fourth item slot");
            BindableAction.Create(ActionID.Flashlight, Key.F, "Toggle your held flashlight on/off");
            BindableAction.Create(ActionID.Emote1, Key.F1, "Dance");
            BindableAction.Create(ActionID.Emote2, Key.F2, "Point");

            var harmony = new Harmony("GTweaking");
            harmony.PatchAll(typeof(Plugin));

            Debug.Log($"{PluginInfo.PLUGIN_GUID} loaded");
        }
        else
        {
            throw new System.Exception($"Multiple versions of {PluginInfo.PLUGIN_GUID}!!!");
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    [HarmonyPostfix]
    public static void ReadInput(PlayerControllerB __instance)
    {
        if ((!__instance.IsOwner || !__instance.isPlayerControlled || (__instance.IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }

        List<BindableAction> actionsToProcess = BindableAction.ActionDictionary.Values.Where(boundAction => ((ButtonControl)Keyboard.current[boundAction.ConfigEntry.Value]).wasPressedThisFrame).ToList();

        foreach (BindableAction boundAction in actionsToProcess)
        {
            switch (boundAction.Id)
            {
                case ActionID.Flashlight:
                    if (__instance.currentlyHeldObjectServer is FlashlightItem && __instance.currentlyHeldObjectServer != __instance.pocketedFlashlight)
                    {
                        __instance.pocketedFlashlight = __instance.currentlyHeldObjectServer;
                    }

                    if (!(__instance.pocketedFlashlight is FlashlightItem) || !__instance.pocketedFlashlight.isHeld)
                    {
                        return;
                    }

                    __instance.pocketedFlashlight.UseItemOnClient(true);
                    if (!(__instance.currentlyHeldObjectServer is FlashlightItem))
                    {
                        FlashlightItem flashlight = __instance.pocketedFlashlight as FlashlightItem;
                        flashlight.flashlightBulbGlow.enabled = false;
                        flashlight.flashlightBulb.enabled = false;

                        if (flashlight.isBeingUsed)
                        {
                            ((Behaviour)__instance.helmetLight).enabled = true;
                            flashlight.usingPlayerHelmetLight = true;
                            flashlight.PocketFlashlightServerRpc(true);
                        }
                        else
                        {
                            ((Behaviour)__instance.helmetLight).enabled = false;
                            flashlight.usingPlayerHelmetLight = false;
                            flashlight.PocketFlashlightServerRpc(false);
                        }
                    }
                    break;
                case ActionID.Slot0:
                    StopEmotes(__instance);
                    SwitchToSlot(__instance, 0);
                    break;
                case ActionID.Slot1:
                    StopEmotes(__instance);
                    SwitchToSlot(__instance, 1);
                    break;
                case ActionID.Slot2:
                    StopEmotes(__instance);
                    SwitchToSlot(__instance, 2);
                    break;
                case ActionID.Slot3:
                    StopEmotes(__instance);
                    SwitchToSlot(__instance, 3);
                    break;
                case ActionID.Emote1:
                    PerformEmote(__instance, 1);
                    break;
                case ActionID.Emote2:
                    PerformEmote(__instance, 2);
                    break;
            }
        }
    }

    private static void SwitchToSlot(PlayerControllerB __instance, int requestedSlot)
    {
        if (!CanSwap(__instance) || __instance.currentItemSlot == requestedSlot)
        {
            return;
        }

        ShipBuildModeManager.Instance.CancelBuildMode(true);
        __instance.playerBodyAnimator.SetBool("GrabValidated", false);
        int num = __instance.currentItemSlot - requestedSlot;
        if (num > 0)
        {
            if (num == 3)
            {
                GetPrivateMethod("SwitchItemSlotsServerRpc").Invoke(__instance, forwardsParam);
            }
            else
            {
                do
                {
                    GetPrivateMethod("SwitchItemSlotsServerRpc").Invoke(__instance, backwardsParam);
                    num--;
                }
                while (num != 0);
            }
        }
        else if (num == -3)
        {
            GetPrivateMethod("SwitchItemSlotsServerRpc").Invoke(__instance, backwardsParam);
        }
        else
        {
            do
            {
                GetPrivateMethod("SwitchItemSlotsServerRpc").Invoke(__instance, forwardsParam);
                num++;
            }
            while (num != 0);
        }
        object[] parameters = new object[2]
        {
                requestedSlot,
                __instance.ItemSlots[requestedSlot]
        };
        GetPrivateMethod("SwitchToItemSlot").Invoke(__instance, parameters);
        if (__instance.currentlyHeldObjectServer != null)
        {
            (__instance.currentlyHeldObjectServer).gameObject.GetComponent<AudioSource>().PlayOneShot(__instance.currentlyHeldObjectServer.itemProperties.grabSFX, 0.6f);
        }
        GetPrivateField("timeSinceSwitchingSlots").SetValue(__instance, 0f);
    }

    private static void PerformEmote(PlayerControllerB __instance, int emoteId)
    {
        __instance.timeSinceStartingEmote = 0f;
        __instance.performingEmote = true;
        __instance.playerBodyAnimator.SetInteger("emoteNumber", emoteId);
        __instance.StartPerformingEmoteServerRpc();
    }

    private static MethodInfo GetPrivateMethod(string name)
    {
        if (MethodCache.TryGetValue(name, out var value))
        {
            return value;
        }
        value = typeof(PlayerControllerB).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (value == null)
        {
            NullReferenceException ex = new NullReferenceException("Method " + name + " could not be found!");
            Debug.LogException((Exception)ex);
            throw ex;
        }
        MethodCache[name] = value;
        return value;
    }

    private static FieldInfo GetPrivateField(string name)
    {
        if (FieldCache.TryGetValue(name, out var value))
        {
            return value;
        }
        value = typeof(PlayerControllerB).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (value == null)
        {
            NullReferenceException ex = new NullReferenceException("Field " + name + " could not be found!");
            Debug.LogException((Exception)ex);
            throw ex;
        }
        FieldCache[name] = value;
        return value;
    }

    private static bool CanSwap(PlayerControllerB __instance)
    {
        float num = (float)GetPrivateField("timeSinceSwitchingSlots").GetValue(__instance);
        bool flag = (bool)GetPrivateField("throwingObject").GetValue(__instance);
        return !((double)num < 0.01 || __instance.inTerminalMenu || __instance.isGrabbingObjectAnimation || __instance.inSpecialInteractAnimation || flag) && !__instance.isTypingChat && !__instance.twoHanded && !__instance.activatingItem && !__instance.jetpackControls && !__instance.disablingJetpackControls;
    }

    private static void StopEmotes(PlayerControllerB __instance)
    {
        __instance.performingEmote = false;
        __instance.StopPerformingEmoteServerRpc();
        __instance.timeSinceStartingEmote = 0f;
    }
}