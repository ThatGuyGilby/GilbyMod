using BepInEx;

namespace LethalCompanyPlugin
{
    [BepInPlugin("org.bepinex.plugins.lethalcompanyexample", "Example Plug-In", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
