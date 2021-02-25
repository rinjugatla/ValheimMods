using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SandBox
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BepInExPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rin_jugatla.SandBox";
        public const string PluginName = "SandBox";
        public const string PluginVersion = "1.0.0";
        /// <summary>
        /// デバッグが有効か
        /// </summary>
#if Debug
        private static readonly bool IsDebug = true;
#else
        private static readonly bool IsDebug = false;
#endif
        private void Awake()
        {
            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }
    }
}
