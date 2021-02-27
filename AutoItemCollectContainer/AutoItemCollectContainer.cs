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
		private const string PluginVersion = "0.0.1";
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
        /// アイテム収納チェックのタイミング
        /// </summary>
        /// <remarks>
        /// true: Start アイテム作成時のみ
        ///  1度のみの実行なので効率的
        /// false: SlowUpdate アイテムアップデート時
        ///  アイテムが移動する場合などに有効？同じアイテムに対して複数回処理を行うため処理が重い
        /// </remarks>
        private static ConfigEntry<bool> IsHookStart;
		/// <summary>
		/// 最初のコンテナに収納するか
		/// </summary>
		private static ConfigEntry<bool> IsFirstContainer;
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
			CollectRnage = Config.Bind<float>("General", "CollectRange", 10f, "Range for automatic collection of items.");
            IsHookStart = Config.Bind<bool>("General", "HookStart", true,
                "You can set the timing for storing items in the chest. " +
                "true: Process only when the item is created. " +
                "This is more efficient because it is executed only once. " +
                "false: Processing is performed when the item position is updated. " +
                "This is useful when items are being moved.However, since the same item will be processed multiple times, the process will be slow.");
            IsFirstContainer = Config.Bind<bool>("General", "FirstContainer", false,
                "true: Store items in the first container detected. false: Use the container closest to the item in the collection range.");

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
        /// アイテムオブジェクト作成時
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), "Start")]
        private static class ModifyItemDropStart
        {
            private static void Postfix(ItemDrop __instance)
            {
                if (IsHookStart.Value)
                    ContainerController.StoreItemNearbyContainer(__instance);
            }
        }

        /// <summary>
        /// アイテムオブジェクトアップデート時
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), "SlowUpdate")]
        private static class ModifyItemDropSlowUpdate
        {
            private static void Postfix(ItemDrop __instance)
            {
                if (!IsHookStart.Value)
                    ContainerController.StoreItemNearbyContainer(__instance);
            }
        }

        /// <summary>
        /// チェスト操作
        /// </summary>
		private static class ContainerController
        {
            /// <summary>
            /// アイテム近くのチェストを探索、発見時は収納
            /// </summary>
            /// <param name="__instance"></param>
            public static void StoreItemNearbyContainer(ItemDrop __instance)
            {
                if (__instance == null || __instance.m_itemData == null)
                    return;

                if (Containers.Count == 0)
                    return;

                // 回収範囲
                float range = CollectRnage.Value * CollectRnage.Value;

                // 回収範囲内のコンテナ取得
                Container target = null;
                float prevDistance = range * 2;
                foreach (var container in Containers)
                {
                    if (container == null || container.GetInventory() == null)
                        continue;

                    Vector3 itemPosition = __instance.transform.position;
                    Vector3 containerPosition = container.transform.position;

                    // 指定距離以上離れている場合は処理を飛ばす
                    float distance = (itemPosition - containerPosition).sqrMagnitude;
                    if (distance > range)
                        continue;

                    if (IsFirstContainer.Value)
                    {
                        if (!container.GetInventory().CanAddItem(__instance.m_itemData, __instance.m_itemData.m_stack))
                            continue;
                        if (IsDebug)
                            Debug.Log($"{__instance.name}({__instance.m_itemData.m_stack}) -> {distance} CanPickup: {__instance.CanPickup()}");
                        target = container;
                        break;
                    }
                    else
                    {
                        // 前回探索済みコンテナとアイテムの距離が近い場合はターゲットを更新
                        if (distance < prevDistance)
                        {
                            if (IsDebug)
                                Debug.Log($"The target container has been updated.");
                            target = container;
                            prevDistance = distance;
                        }
                    }
                }

                if (target == null)
                    return;

                StoreItemToContainer(target, __instance);
            }

            /// <summary>
            /// アイテムをコンテナに収納
            /// </summary>
            /// <param name="container">コンテナ</param>
            /// <param name="drop">アイテム</param>
            private static void StoreItemToContainer(Container container, ItemDrop drop)
            {
                // アイテムをコンテナに移動
                Inventory inventory = container.GetInventory();
                if (inventory.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack))
                {
                    if (IsDebug)
                        Debug.Log($"{drop.name} has been stored in the container.");

                    if (inventory.AddItem(drop.m_itemData))
                    {
                        Traverse.Create(drop).Field("m_nview").GetValue<ZNetView>().Destroy();
                    }
                        
                }
            }
        }
	}
}
