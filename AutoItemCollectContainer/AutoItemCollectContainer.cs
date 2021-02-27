using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoItemCollectContainer
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "rin_jugatla.AutoItemCollectContainer";
		private const string PluginName = "AutoItemCollectContainer";
		private const string PluginVersion = "1.0.0";
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
		/// 自動回収範囲
		/// </summary>
		private static ConfigEntry<float> CollectRnage;
		/// <summary>
		/// 自動アップデート用のNexusID
		/// </summary>
		/// <remarks>
		/// https://www.nexusmods.com/valheim/mods/102
		/// </remarks>
		public static ConfigEntry<int> NexusID;
		/// <summary>
		/// コンテナ
		/// </summary>
		private static List<Container> Containers = new List<Container>();

		private void Awake()
		{
			IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
			NexusID = Config.Bind<int>("General", "NexusID", -1, "Nexus mod ID for updates");
			CollectRnage = Config.Bind<float>("General", "CollectRange", 10f, "Auto item collect range");

			if (!IsEnabled.Value)
				return;

			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}

		/// <summary>
		/// コンテナリスト取得
		/// </summary>
		[HarmonyPatch(typeof(Container), "Awake")]
		public static class ModifyContainerAwake
		{
			private static void Postfix(Container __instance, ZNetView ___m_nview)
			{
				if (__instance.name.StartsWith("piece_chest") && __instance.GetInventory() != null)
					Containers.Add(__instance);
			}
		}

		/// <summary>
		/// コンテナリストから破棄コンテナを削除
		/// </summary>
		[HarmonyPatch(typeof(Container), "OnDestroyed")]
		public static class ModifyContainerOnDestroyed
		{
			private static void Prefix(Container __instance)
			{
				Containers.Remove(__instance);
			}
		}

		/// <summary>
		/// アイテムをコンテナに移動
		/// </summary>
		[HarmonyPatch(typeof(ItemDrop), "SlowUpdate")]
		public static class ModifyItemDropSlowUpdate
		{
			private static void Postfix(ItemDrop __instance)
			{
				if (__instance == null || __instance.m_itemData == null)
					return;

				// 回収範囲
				float range = CollectRnage.Value * CollectRnage.Value;
				
				// 回収範囲内のコンテナ取得
				foreach (var container in Containers)
				{
					Inventory inventory = container.GetInventory();
					if (inventory == null)
						continue;

					Vector3 itemPosition = __instance.transform.position;
					Vector3 containerPosition = container.transform.position;

					// 指定距離以上離れている場合は処理を飛ばす
					float distance = (itemPosition - containerPosition).sqrMagnitude;
					if (distance > range)
						continue;

					// アイテムをコンテナに移動
					if (inventory.CanAddItem(__instance.m_itemData, __instance.m_itemData.m_stack))
					{
						if (IsDebug)
							Debug.Log($"{__instance.name} has been stored in the container.");

						inventory.AddItem(__instance.m_itemData);
						Traverse.Create(__instance).Method("Save").GetValue();
						Destroy(__instance.gameObject);
						return;
					}
				}
			}
		}
	}
}
