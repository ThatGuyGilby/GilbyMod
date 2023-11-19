using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using System.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine;
using Debug = UnityEngine.Debug;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine.InputSystem.Controls;
using BepInEx.Configuration;
using System.Collections.Generic;
using System;
using System.Reflection;
using Object = UnityEngine.Object;
using System.Globalization;

namespace GTweaking;

[BepInPlugin("GTweaking", "GTweaking", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance;
    
    private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();
    private static readonly Dictionary<string, FieldInfo> FieldCache = new Dictionary<string, FieldInfo>();
    private static readonly object[] backwardsParam = new object[1] { false };
    private static readonly object[] forwardsParam = new object[1] { true };

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "GTweaking";

        public const string PLUGIN_NAME = "GTweaking";

        public const string PLUGIN_VERSION = "1.0.0";
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
        if ((!((NetworkBehaviour)__instance).IsOwner || !__instance.isPlayerControlled || (((NetworkBehaviour)__instance).IsServer && !__instance.isHostPlayerObject)) && !__instance.isTestingPlayer)
        {
            return;
        }

        List<BindableAction> actionsToProcess = new List<BindableAction>();
        foreach (var boundAction in BindableAction.ActionDictionary.Values)
        {
            if (((ButtonControl)Keyboard.current[boundAction.ConfigEntry.Value]).wasPressedThisFrame)
            {
                actionsToProcess.Add(boundAction);
            }
        }

        foreach (var boundAction in actionsToProcess)
        {
            switch(boundAction.Id)
            {
                case ActionID.Flashlight:
                    if (__instance.currentlyHeldObjectServer is FlashlightItem && (Object)(object)__instance.currentlyHeldObjectServer != (Object)(object)__instance.pocketedFlashlight)
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
                        GrabbableObject pocketedFlashlight = __instance.pocketedFlashlight;
                        GrabbableObject pocketedFlashlight2 = __instance.pocketedFlashlight;
                        ((Behaviour)((FlashlightItem)((pocketedFlashlight2 is FlashlightItem) ? pocketedFlashlight2 : null)).flashlightBulbGlow).enabled = false;
                        GrabbableObject pocketedFlashlight3 = __instance.pocketedFlashlight;
                        ((Behaviour)((FlashlightItem)((pocketedFlashlight3 is FlashlightItem) ? pocketedFlashlight3 : null)).flashlightBulb).enabled = false;
                        GrabbableObject pocketedFlashlight4 = __instance.pocketedFlashlight;
                        if (((pocketedFlashlight4 is FlashlightItem) ? pocketedFlashlight4 : null).isBeingUsed)
                        {
                            ((Behaviour)__instance.helmetLight).enabled = true;
                            GrabbableObject pocketedFlashlight5 = __instance.pocketedFlashlight;
                            ((FlashlightItem)((pocketedFlashlight5 is FlashlightItem) ? pocketedFlashlight5 : null)).usingPlayerHelmetLight = true;
                            GrabbableObject pocketedFlashlight6 = __instance.pocketedFlashlight;
                            ((FlashlightItem)((pocketedFlashlight6 is FlashlightItem) ? pocketedFlashlight6 : null)).PocketFlashlightServerRpc(true);
                        }
                        else
                        {
                            ((Behaviour)__instance.helmetLight).enabled = false;
                            GrabbableObject pocketedFlashlight7 = __instance.pocketedFlashlight;
                            ((FlashlightItem)((pocketedFlashlight7 is FlashlightItem) ? pocketedFlashlight7 : null)).usingPlayerHelmetLight = false;
                            GrabbableObject pocketedFlashlight8 = __instance.pocketedFlashlight;
                            ((FlashlightItem)((pocketedFlashlight8 is FlashlightItem) ? pocketedFlashlight8 : null)).PocketFlashlightServerRpc(false);
                        }
                    }
                    break;
                case ActionID.Slot0:
                    stopEmotes(__instance);
                    switchItemSlots(__instance, 0);
                    break;
                case ActionID.Slot1:
                    stopEmotes(__instance);
                    switchItemSlots(__instance, 1);
                    break;
                case ActionID.Slot2:
                    stopEmotes(__instance);
                    switchItemSlots(__instance, 2);
                    break;
                case ActionID.Slot3:
                    stopEmotes(__instance);
                    switchItemSlots(__instance, 3);
                    break;
            }
        }
    }

    private static void switchItemSlots(PlayerControllerB __instance, int requestedSlot)
    {
        if (!isItemSwitchPossible(__instance) || __instance.currentItemSlot == requestedSlot)
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
        if ((Object)(object)__instance.currentlyHeldObjectServer != (Object)null)
        {
            ((Component)__instance.currentlyHeldObjectServer).gameObject.GetComponent<AudioSource>().PlayOneShot(__instance.currentlyHeldObjectServer.itemProperties.grabSFX, 0.6f);
        }
        GetPrivateField("timeSinceSwitchingSlots").SetValue(__instance, 0f);
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

    private static bool isItemSwitchPossible(PlayerControllerB __instance)
    {
        float num = (float)GetPrivateField("timeSinceSwitchingSlots").GetValue(__instance);
        bool flag = (bool)GetPrivateField("throwingObject").GetValue(__instance);
        return !((double)num < 0.01 || __instance.inTerminalMenu || __instance.isGrabbingObjectAnimation || __instance.inSpecialInteractAnimation || flag) && !__instance.isTypingChat && !__instance.twoHanded && !__instance.activatingItem && !__instance.jetpackControls && !__instance.disablingJetpackControls;
    }

    private static void stopEmotes(PlayerControllerB __instance)
    {
        __instance.performingEmote = false;
        __instance.StopPerformingEmoteServerRpc();
        __instance.timeSinceStartingEmote = 0f;
    }
}