using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaveCommand
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "rin_jugatla.SaveCommand";
		public const string PluginName = "SaveCommand";
		public const string PluginVersion = "1.0.0";
		/// <summary>
		/// デバッグが有効か
		/// </summary>
#if Debug
		private static readonly bool IsDebug = true;
#else
		private static readonly bool IsDebug = false;
#endif
		/// <summary>
		/// MODが有効か
		/// </summary>
		public static ConfigEntry<bool> IsEnabled;
		/// <summary>
		/// 自動アップデート用のNexusID
		/// </summary>
		/// <remarks>
		/// https://www.nexusmods.com/valheim/mods/102
		/// </remarks>
		public static ConfigEntry<int> NexusID;

		private void Awake()
		{
			IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
			NexusID = Config.Bind<int>("General", "NexusID", -1, "Nexus mod ID for updates");

			if (!IsEnabled.Value)
				return;

			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}
	}
}
