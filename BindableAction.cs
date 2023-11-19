using BepInEx.Configuration;
using BepInEx;
using GTweaking;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;

namespace GTweaking;

public sealed class BindableAction
{
    public static Dictionary<string, BindableAction> ActionDictionary = new Dictionary<string, BindableAction>();
    public static List<BindableAction> ActionList = new List<BindableAction>();

    public ActionID Id { get; }

    public Key Hotkey { get; }

    public string Description { get; }

    public ConfigEntry<Key> ConfigEntry { get; set; }

    private BindableAction(ActionID id, Key hotkey, string description)
    {
        Id = id;
        Hotkey = hotkey;
        Description = description;
    }

    public static void Create(ActionID id, Key hotkey, string description)
    {
        BindableAction action = new(id, hotkey, description);

        ConfigEntry<Key> configEntry = ((BaseUnityPlugin)Plugin.Instance).Config.Bind<Key>("Bindings", action.Id.ToString(), action.Hotkey, action.Description);
        action.ConfigEntry = configEntry;

        ActionDictionary.Add(id.ToString(), action);
        ActionList.Add(action);

        Debug.Log($"{id} bound to {hotkey}");
    }
}