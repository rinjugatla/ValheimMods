using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PlayerScale
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "rin_jugatla.PlayerScale";
		public const string PluginName = "PlayerScale";
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
		private static ConfigEntry<bool> IsEnabled;
		/// <summary>
		/// 自動アップデート用のNexusID
		/// </summary>
		/// <remarks>
		/// https://www.nexusmods.com/valheim/mods/102
		/// </remarks>
		private static ConfigEntry<int> NexusID;
		/// <summary>
		/// プレイヤースケール
		/// </summary>
		private static ConfigEntry<float> PlayerScaleFactor;

		private void Awake()
		{
			IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod.");
			NexusID = Config.Bind<int>("General", "NexusID", 327, "Nexus mod ID for updates.");
			PlayerScaleFactor = Config.Bind<float>("General", "PlayerScaleFactor", 1f, "Player scale factor.");

			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}

		[HarmonyPatch(typeof(Player), "OnSpawned")]
		private static class ModifyPlayerOnSpawned
		{
			private static void Postfix()
			{
				// トップメニューでは実行しない
				if (!Player.m_localPlayer)
					return;

				ZLog.Log($"Changed the player scale to {PlayerScaleFactor.Value} times.");
				float scale = PlayerScaleFactor.Value;
				Player.m_localPlayer.transform.localScale = new Vector3(scale, scale, scale);
			}
		}
	}
}
