using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ChangeEquipmentByWheel
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "rin_jugatla.ChangeEquipmentByWheel";
		public const string PluginName = "ChangeEquipmentByWheel";
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
		/// 修飾キー
		/// </summary>
		private static ConfigEntry<string> ModifierKey;
		/// <summary>
		/// ホイール操作を逆転させるか
		/// </summary>
		/// <remarks>
		/// true: ホイール下で8, ホイール上で1方向
		/// false(def): ホイール下で1, ホイール上で8方向
		/// </remarks>
		private static ConfigEntry<bool> IsReverseWheel;
		/// <summary>
		/// 現在有効なホットバー
		/// </summary>
		private static int NowHotbarIndex;

		private void Awake()
		{
			IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod.");
			NexusID = Config.Bind<int>("General", "NexusID", 377, "Nexus mod ID for updates.");
			ModifierKey = Config.Bind<string>("General", "ModifierKey", "left shift", "Modifier keys for using mods");
			IsReverseWheel = Config.Bind<bool>("General", "ReverseWheel", false, "Do you want to reverse the order in which items are sent?");

			if (!IsEnabled.Value)
				return;

			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}

		private void Update()
		{
			if (!Player.m_localPlayer)
				return;

			if (Input.GetKey(ModifierKey.Value))
			{
				var scroll = Input.mouseScrollDelta.y;
				if (scroll == 0)
					return;

				// Todo: 右手装備で装備中の場合は飛ばす処理を追加する？
				int reverse = IsReverseWheel.Value ? -1 : 1;
				if (scroll > 0)
					Player.m_localPlayer.UseHotbarItem(NowHotbarIndex + (1 * reverse));
				else
					Player.m_localPlayer.UseHotbarItem(NowHotbarIndex - (1 * reverse));
			}
		}

		/// <summary>
		/// 装備持ち替え
		/// </summary>
		[HarmonyPatch(typeof(Player), "UseHotbarItem")]
		private static class ModifyPlayerUseHotbarItem
		{
			private static void Postfix(int index, Inventory ___m_inventory)
			{
				ItemDrop.ItemData itemAt = ___m_inventory.GetItemAt(index - 1, 0);
				if (itemAt == null)
					return;

				NowHotbarIndex = index;
			}
		}

		/// <summary>
		/// カメラ操作
		/// </summary>
		/// <remarks>
		/// 修飾キーを押している場合はカメラをズームさせない
		/// </remarks>
		[HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
		private static class ModifyGameCameraUpdateCamera
		{
			private static bool isFirst = true;
			private static float defaultZoomSens;

			private static void Prefix(GameCamera __instance, float dt, ref float ___m_zoomSens)
			{
				if (isFirst)
				{
					Debug.Log($"GetZoomSens: {___m_zoomSens}");
					defaultZoomSens = ___m_zoomSens;
					isFirst = false;
				}

				if (!Input.GetKey(ModifierKey.Value))
					___m_zoomSens = defaultZoomSens;
				else
					___m_zoomSens = 0f;
			}
		}
	}
}
