using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AddAllFuel
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BepInExPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "rin_jugatla.AddAllFuel";
        private const string PluginName = "AddAllFuel";
        private const string PluginVersion = "1.4.0";
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
        /// 一括投入時の修飾キー
        /// </summary>
        private static ConfigEntry<string> ModifierKey;
        /// <summary>
        /// 自動投入修飾キーを反転するか
        /// </summary>
        /// <remarks>
        /// def -> false: Eで1つずつ投入、 ModifierKey + Eで一括投入
        /// true: Eで一括投入、　ModifierKey + Eで1つずつ投入
        /// </remarks>
        private static ConfigEntry<bool> IsReverseModifierMode;
        /// <summary>
        /// 一括投入に使用しない木材、鉱石名
        /// </summary>
        /// <remarks>
        /// $item_wood, $item_finewood, $item_roundlog
        /// </remarks>
        private static IReadOnlyList<string> ExcludeNames;
        /// <summary>
        /// 一括投入しない場合に除外アイテムの使用を許可するか
        /// </summary>
        private static ConfigEntry<bool> IsAllowAddOneExcludeItem;
        /// <summary>
        /// コンテナからのアイテムを補充するか
        /// </summary>
        private static ConfigEntry<bool> IsUseFromContainer;
        /// <summary>
        /// コンテナから使用する場合の範囲
        /// </summary>
        private static ConfigEntry<float> UseFromContainerRange;
        /// <summary>
		/// コンテナ
		/// </summary>
		private static List<Container> Containers = new List<Container>();

        private void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 107, "Nexus mod ID for updates");
            ModifierKey = Config.Bind<string>("General", "ModifierKey", "left shift", "Modifier keys for using mods");
            IsReverseModifierMode = Config.Bind<bool>("General", "IsReverseModifierMode", false, "false: Batch submit with ModifierKey + UseKey. true: Batch submit with UseKey.");
            ExcludeNames = Config.Bind<string>("General", "ExcludeNames", "",
                "Name of item not to be used as fuel/ore. Setting example: $item_finewood,$item_roundlog").Value.Replace(" ", "").Split(',').ToList();
            IsAllowAddOneExcludeItem = Config.Bind<bool>("General", "AllowAddOneExcludeItem", true, "Allow the addition of excluded items if you don't want to do batch processing?");
            IsUseFromContainer = Config.Bind<bool>("General", "UseFromContainer", false, "If you don't have enough items on hand, you can replenish them from containers.");
            UseFromContainerRange = Config.Bind<float>("General", "UseFromContainerRange", 5f, "Search range when replenishing items from containers.");

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
            private static void Postfix(Container __instance)
            {
                if (__instance.name.StartsWith("piece_chest") || __instance.name.StartsWith("Container") && __instance.GetInventory() != null)
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

        // 炭焼き窯、溶解炉
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        private static class ModifySmelterOnAddOre
        {
            private static bool Prefix(Smelter __instance, ref Humanoid user, ref bool __result)
            {
                if (IsDebug)
                    Debug.Log("OnAddOre");

                __result = false;

                // アイテムの追加方法
                bool isAddOne = Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                                !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value;

                #region 既存メソッドの処理
                // インベントリからアイテムを取得
                ItemDrop.ItemData item = FindCookableItem(__instance, user.GetInventory(), isAddOne);
                if (item == null)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems", 0, null);
                    return false;
                }

                // アイテムの追加が許可されているか
                if (!Traverse.Create(__instance).Method("IsItemAllowed", item.m_dropPrefab.name).GetValue<bool>())
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_wontwork", 0, null);
                    return false;
                }

                // 追加するアイテム名
                ZLog.Log("trying to add " + item.m_shared.m_name);

                // 現在の投入数
                int queueSizeNow = Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
                if (queueSizeNow >= __instance.m_maxOre)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_itsfull", 0, null);
                    return false;
                }

                user.Message(MessageHud.MessageType.Center, "$msg_added " + item.m_shared.m_name, 0, null);
                #endregion

                // 投入数
                int queueSize = 1;
                if (!isAddOne)
                {
                    int queueSizeLeft = __instance.m_maxOre - queueSizeNow;
                    queueSize = Math.Min(item.m_stack, queueSizeLeft);
                }

                if (IsDebug)
                {
                    Debug.Log($"{item.m_shared.m_name}({item.m_stack})");
                    Debug.Log($"{queueSizeNow} / {__instance.m_maxOre}");
                    Debug.Log($"{queueSize}");
                }

                // 投入
                user.GetInventory().RemoveItem(item, queueSize);
                ZNetView m_nview = Traverse.Create(__instance).Field("m_nview").GetValue<ZNetView>();
                for (int i = 0; i < queueSize; i++)
                {
                    m_nview.InvokeRPC("AddOre", new object[] { item.m_dropPrefab.name });
                }

                // 後処理
                __result = true;
                return false;
            }

            /// <summary>
            /// インベントリから投入可能なアイテム(木材、鉱石)を取得
            /// </summary>
            /// <remarks>
            /// 「Craft Build and Smelt From Container」MODで既存の処理を書き換えているためDupeが発生
            /// 指定のインベントリからアイテムを探索するため独自実装
            /// 設定で除外されているアイテム以外を使用
            /// </remarks>
            /// <param name="__instance">インスタンス</param>
            /// <param name="inventory">インベントリ</param>
            /// <param name="isAddOne">一つだけ投入するか</param>
            /// <returns></returns>
            private static ItemDrop.ItemData FindCookableItem(Smelter __instance, Inventory inventory, bool isAddOne)
            {
                IEnumerable<string> names = null;
                if (IsAllowAddOneExcludeItem.Value && isAddOne)
                {
                    // 除外を考慮せず取得
                    names = __instance.m_conversion.Select(n => n.m_from.m_itemData.m_shared.m_name);
                }
                else
                {
                    // 除外されている燃料以外を取得
                    names = __instance.m_conversion.
                        Where(n => !ExcludeNames.Contains(n.m_from.m_itemData.m_shared.m_name)).
                        Select(n => n.m_from.m_itemData.m_shared.m_name);
                }

                if (names == null)
                    return null;

                foreach (string name in names)
                {
                    ItemDrop.ItemData item = inventory.GetItem(name);
                    if (item != null)
                        return item;
                }
                return null;
            }
        }

        // 溶解炉燃料追加
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        private static class ModifySmelterOnAddFuel
        {
            private static void Postfix(Smelter __instance, ref bool __result, ref Humanoid user)
            {
                if (IsDebug)
                {
                    Debug.Log("OnAddFuel");
                    Debug.Log(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                }

                if (Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                    !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value)
                    return;

                // 処理なし
                // 既存メソッドで燃料補充されていない場合
                if (!__result)
                    return;
                // 燃料が最大の場合
                float fuelNow = Traverse.Create(__instance).Method("GetFuel").GetValue<float>();
                if (fuelNow > __instance.m_maxFuel - 1)
                    return;

                Inventory inventory = user.GetInventory();
                // 燃料の所持なし
                bool isHaveItem = inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                if (!isHaveItem)
                    return;
                // アイテム取得
                ItemDrop.ItemData item = inventory.GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                if (item == null)
                    return;

                // 残り投入数
                int fuelLeft = (int)((float)(__instance.m_maxFuel) - fuelNow);
                int fuelSize = Math.Min(item.m_stack, fuelLeft);
                if (IsDebug)
                {
                    Debug.Log($"{item.m_shared.m_name}({item.m_stack})");
                    Debug.Log($"{fuelNow} / {__instance.m_maxFuel}");
                    Debug.Log($"{fuelSize}");
                }

                // 投入
                inventory.RemoveItem(item, fuelSize);
                ZNetView m_nview = Traverse.Create(__instance).Field("m_nview").GetValue<ZNetView>();
                for (int i = 0; i < fuelSize; i++)
                    m_nview.InvokeRPC("AddFuel", Array.Empty<object>());
            }
        }

        /// <summary>
        /// 焚火、トーチ
        /// </summary>
        [HarmonyPatch(typeof(Fireplace), "Interact")]
        private static class ModifyFireplaceInteract
        {
            private static void Postfix(Fireplace __instance, Humanoid user, bool __result, ZNetView ___m_nview)
            {
                if (!__result)
                    return;

                if (Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                    !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value)
                    return;

                // 燃料が最大の場合
                float fuelNow = (float)Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel", 0f));
                if (fuelNow > __instance.m_maxFuel - 1)
                    return;

                Inventory inventory = user.GetInventory();
                if (inventory == null)
                    return;

                // 燃料の所持なし
                bool isHaveItem = inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                if (!isHaveItem)
                    return;

                // アイテム取得
                ItemDrop.ItemData item = inventory.GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                if (item == null)
                    return;

                // 残り投入数
                int fuelLeft = (int)(__instance.m_maxFuel - fuelNow);
                int fuelSize = Math.Min(item.m_stack, fuelLeft);

                // 投入
                inventory.RemoveItem(item, fuelSize);
                for (int i = 0; i < fuelSize; i++)
                    ___m_nview.InvokeRPC("AddFuel", Array.Empty<object>());
            }
        }

        private static class Utility
        {
            /// <summary>
            /// コンテナリストから探索範囲内で最も近いコンテナを取得
            /// </summary>
            /// <param name="center">探索中心点</param>
            /// <returns></returns>
            public static Container GetNearestContainer(Vector3 center)
            {
                float sqrRange = UseFromContainerRange.Value * UseFromContainerRange.Value;
                Container container = Containers.
                    Where(n => (n.transform.position - center).sqrMagnitude < sqrRange).
                    OrderByDescending(m => m.transform.position - center).First();
                return container;
            }
        }
    }
}
