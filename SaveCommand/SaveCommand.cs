using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SaveCommand
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "rin_jugatla.SaveCommand";
		private const string PluginName = "SaveCommand";
		private const string PluginVersion = "1.1.0";
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
		/// セーブホットキー
		/// </summary>
		private static ConfigEntry<string> SaveHotKey;
		/// <summary>
		/// セーブコマンドの実行間隔
		/// </summary>
		private static ConfigEntry<int> SaveIntervalMinutes;
		/// <summary>
		/// 前回のセーブ時間
		/// </summary>
		private static DateTime PrevSaveTime = DateTime.Now;
		/// <summary>
		/// ヒューマノイド
		/// </summary>
		private static Humanoid HumanoidInstance;

		private void Awake()
		{
			IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod.");
			NexusID = Config.Bind<int>("General", "NexusID", 190, "Nexus mod ID for updates.");
			SaveHotKey = Config.Bind<string>("General", "SaveHotKey", "p", "Save hot key.");
			SaveIntervalMinutes = Config.Bind<int>("General", "SaveIntervalMinutes", 3, "Save interval.");

			if (SaveHotKey.Value == "")
				SaveHotKey.Value = "p";

#if !Debug
			if (SaveIntervalMinutes.Value < 3)
				SaveIntervalMinutes.Value = 3;
#endif

			if (!IsEnabled.Value)
				return;

			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}


		/// <summary>
		/// ホットキー処理
		/// </summary>
		private void Update()
		{
			if (Input.GetKeyUp(SaveHotKey.Value))
			{
				Utility.Save();
			}
		}

		[HarmonyPatch(typeof(Humanoid), "Awake")]
		private static class ModifyHumanoidAwake
		{
			private static void Postfix(Humanoid __instance)
			{
				HumanoidInstance = __instance;
			}
		}

		/// <summary>
		/// チャット処理
		/// </summary>
		[HarmonyPatch(typeof(Chat), "InputText")]
		private static class ModifyChatInputText
		{
			private static bool Prefix(Chat __instance)
			{
				if (IsDebug)
					Debug.Log("InputText");

				string text = __instance.m_input.text;
				string lower = text.ToLower();
				if (lower == "/save")
				{
					Utility.Save();
					return false;
				}

				return true;
			}
		}

		private static class Utility
		{
			/// <summary>
			/// ワールドデータ、プレイヤーデータをセーブ
			/// </summary>
			public static void Save()
			{
				// トップメニューの場合は処理しない
				if (Player.m_localPlayer == null)
					return;

				int elapsedTime = (int)(DateTime.Now - PrevSaveTime).TotalMinutes;
				int leftTime = SaveIntervalMinutes.Value - elapsedTime;
				if (leftTime > 0)
				{
					HumanoidInstance.Message(MessageHud.MessageType.Center,
						$"The save interval is too short.\nPlease run it after {leftTime} minutes.", 0, null);
					return;
				}

				if (ZNet.instance.IsSaving())
				{
					HumanoidInstance.Message(MessageHud.MessageType.Center,
						$"The save process is already in progress.", 0, null);
					return;
				}

				ZNet.instance.ConsoleSave();
				Game.instance.GetPlayerProfile().Save();
				PrevSaveTime = DateTime.Now;

				HumanoidInstance.Message(MessageHud.MessageType.Center, $"The save process is complete.", 0, null);
				return;
			}
		}
	}
}