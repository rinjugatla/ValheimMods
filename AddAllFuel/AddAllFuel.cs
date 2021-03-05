using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private const string PluginVersion = "1.5.1";
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
                if ((__instance.name.StartsWith("piece_chest") || __instance.name.StartsWith("Container")) && __instance.GetInventory() != null)
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
        [HarmonyPriority(Priority.High)]
        private static class ModifySmelterOnAddOre
        {
            private static bool Prefix(Smelter __instance, ref Humanoid user, ZNetView ___m_nview, ref bool __result)
            {
                __result = false;

                // 現在の投入数
                int queueSizeNow = Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
                if (queueSizeNow >= __instance.m_maxOre)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_itsfull", 0, null);
                    return false;
                }

                // アイテムの追加方法
                bool isAddOne = Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                                !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value;

                // インベントリからアイテムを取得
                ItemDrop.ItemData item = FindCookableItem(__instance, user.GetInventory(), isAddOne);
                Container container = null;
                if (item == null)
                {
                    // コンテナから取得
                    if (IsUseFromContainer.Value)
                    {
                        List<Container> containers = Utility.GetNearByContainer(user.transform.position);
                        foreach (var c in containers ?? new List<Container>())
                        {
                            container = c;
                            item = FindCookableItem(__instance, c.GetInventory(), isAddOne);
                            if (item != null)
                                break;
                        }
                    }
                    // インベントリ、コンテナともに見つからない場合
                    if (item == null)
                    {
                        if (isAddOne)
                        {
                            // 一括投入しない場合は他のMODの処理に任せる
                            return true;
                        }
                        else
                        {
                            user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems", 0, null);
                            return false;
                        }
                    }
                }

                // アイテムの追加が許可されているか
                if (!Traverse.Create(__instance).Method("IsItemAllowed", item.m_dropPrefab.name).GetValue<bool>())
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_wontwork", 0, null);
                    return false;
                }

                user.Message(MessageHud.MessageType.Center, "$msg_added " + item.m_shared.m_name, 0, null);

                // 投入数
                int queueSizeLeft = __instance.m_maxOre - queueSizeNow;
                int queueSize = 1;
                if (!isAddOne)
                    queueSize = Math.Min(item.m_stack, queueSizeLeft);

                if (IsDebug)
                {
                    Debug.Log($"{item.m_shared.m_name}({item.m_stack})");
                    Debug.Log($"{queueSizeNow} / {__instance.m_maxOre}");
                    Debug.Log($"{queueSize}");
                }

                // 投入
                if (container == null)
                    user.GetInventory().RemoveItem(item, queueSize);
                else
                {
                    container.GetInventory().RemoveItem(item, queueSize);
                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(container, new object[] { });
                }

                RPC_AddOre(__instance, ___m_nview, item.m_dropPrefab.name, queueSize);

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
                    ItemDrop.ItemData item = inventory?.GetItem(name);
                    if (item != null)
                        return item;
                }
                return null;
            }

            /// <summary>
            /// 燃料/鉱石の投入を施設に反映
            /// </summary>
            /// <remarks>
            /// 一括投入するとエフェクトで処理が重くなるので独自実装
            /// </remarks>
            /// <param name="m_nview"></param>
            /// <param name="name">アイテム名</param>
            /// <param name="count">投入数</param>
            private static void RPC_AddOre(Smelter instance, ZNetView m_nview, string name, int count)
            {
                if (!m_nview.IsOwner())
                    return;
                if (!Traverse.Create(instance).Method("IsItemAllowed", name).GetValue<bool>())
                    return;

                int start = Traverse.Create(instance).Method("GetQueueSize").GetValue<int>();
                for (int i = 0; i < count; i++)
                {
                    // Key Valueの形で値を持っているみたい
                    m_nview.GetZDO().Set($"item{start + i}", name);
                }
                m_nview.GetZDO().Set("queued", start + count);

                instance.m_oreAddedEffects.Create(instance.transform.position, instance.transform.rotation, null, 1f);
                ZLog.Log($"Added ore {name} * {count}");
            }
        }

        // 溶解炉燃料追加
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        [HarmonyPriority(Priority.High)]
        private static class ModifySmelterOnAddFuel
        {
            private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview, ref bool __result)
            {
                __result = false;

                string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

                if (item != null && item.m_shared.m_name != fuelName)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_wrongitem", 0, null);
                    return false;
                }

                // 燃料が最大の場合
                float fuelNow = Traverse.Create(__instance).Method("GetFuel").GetValue<float>();
                if (fuelNow > __instance.m_maxFuel - 1)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_itsfull", 0, null);
                    return false;
                }

                bool isAddOne = Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                               !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value;

                // インベントリからアイテムを取得
                item = user.GetInventory().GetItem(fuelName);
                Container container = null;
                if (item == null)
                {
                    // コンテナから取得
                    if (IsUseFromContainer.Value)
                    {
                        List<Container> containers = Utility.GetNearByContainer(user.transform.position);
                        container = containers?.Where(n => n.GetInventory()?.GetItem(fuelName) != null).FirstOrDefault();
                        item = container?.GetInventory().GetItem(fuelName);
                    }
                    // インベントリ、コンテナともに見つからない場合
                    if (item == null)
                    {
                        if (isAddOne)
                        {
                            // 一括投入しない場合は他のMODの処理に任せる
                            return true;
                        }
                        else
                        {
                            user.Message(MessageHud.MessageType.Center, $"$msg_donthaveany {fuelName}", 0, null);
                            return false;
                        }
                    }
                }

                user.Message(MessageHud.MessageType.Center, $"$msg_added {fuelName}", 0, null);

                // 残り投入数
                int fuelLeft = (int)(__instance.m_maxFuel - fuelNow);
                int fuelSize = 1;
                if (!isAddOne)
                    fuelSize = Math.Min(item.m_stack, fuelLeft);

                if (IsDebug)
                {
                    Debug.Log($"{item.m_shared.m_name}({item.m_stack})");
                    Debug.Log($"{fuelNow} / {__instance.m_maxFuel}");
                    Debug.Log($"{fuelSize}");
                }

                // 投入
                if (container == null)
                    user.GetInventory().RemoveItem(item, fuelSize);
                else
                {
                    container.GetInventory().RemoveItem(item, fuelSize);
                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(container, new object[] { });
                }

                RPC_AddFuel(__instance, ___m_nview, fuelSize);

                __result = true;
                return false;
            }

            /// <summary>
            /// 燃料の投入を施設に反映
            /// </summary>
            /// <remarks>
            /// 一括投入するとエフェクトで処理が重くなるので独自実装
            /// </remarks>
            /// <param name="m_nview"></param>
            /// <param name="name">アイテム名</param>
            /// <param name="count">投入数</param>
            private static void RPC_AddFuel(Smelter instance, ZNetView m_nview, float count)
            {
                if (!m_nview.IsOwner())
                    return;

                float now = Traverse.Create(instance).Method("GetFuel").GetValue<float>();
                m_nview.GetZDO().Set("fuel", now + count);
                instance.m_fuelAddedEffects.Create(
                    instance.transform.position, instance.transform.rotation, instance.transform, 1f);
                ZLog.Log($"Added fuel * {count}");
            }
        }

        /// <summary>
        /// 焚火、トーチ
        /// </summary>
        [HarmonyPatch(typeof(Fireplace), "Interact")]
        [HarmonyPriority(Priority.High)]
        private static class ModifyFireplaceInteract
        {
            private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ZNetView ___m_nview, ref bool __result)
            {
                __result = false;

                if (hold)
                    return false;

                if (!___m_nview.HasOwner())
                    ___m_nview.ClaimOwnership();

                string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

                // 燃料が最大の場合
                float fuelNow = (float)Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel", 0f));
                if (fuelNow > __instance.m_maxFuel - 1)
                {
                    user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantaddmore", new string[]
                        {fuelName}), 0, null);
                    return false;
                }

                bool isAddOne = Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                               !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value;

                ItemDrop.ItemData item = user.GetInventory()?.GetItem(fuelName);
                Container container = null;
                if (item == null)
                {
                    if (IsUseFromContainer.Value)
                    {
                        List<Container> containers = Utility.GetNearByContainer(user.transform.position);
                        container = containers?.Where(n => n.GetInventory()?.GetItem(fuelName) != null)?.FirstOrDefault();
                        item = container?.GetInventory().GetItem(fuelName);
                    }

                    // インベントリ、コンテナに燃料がない場合
                    if (item == null)
                    {
                        if (isAddOne)
                        {
                            // 一括投入しない場合は他のMODの処理に任せる
                            return true;
                        }
                        else
                        {
                            user.Message(MessageHud.MessageType.Center, $"$msg_outof {fuelName}", 0, null);
                            return false;
                        }
                    }
                }

                user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", new string[]
                    {fuelName}), 0, null);

                // 残り投入数
                int fuelLeft = (int)(__instance.m_maxFuel - fuelNow);
                int fuelSize = 1;
                if (!isAddOne)
                    fuelSize = Math.Min(item.m_stack, fuelLeft);

                // 投入
                if (container == null)
                    user.GetInventory().RemoveItem(item, fuelSize);
                else
                {
                    container.GetInventory().RemoveItem(item, fuelSize);
                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(container, new object[] { });
                }

                RPC_AddFuel(__instance, ___m_nview, fuelSize);

                __result = true;
                return false;
            }

            /// <summary>
            /// 燃料の投入を施設に反映
            /// </summary>
            /// <remarks>
            /// 一括投入するとエフェクトで処理が重くなるので独自実装
            /// </remarks>
            /// <param name="m_nview"></param>
            /// <param name="name">アイテム名</param>
            /// <param name="count">投入数</param>
            private static void RPC_AddFuel(Fireplace instance, ZNetView m_nview, float count)
            {
                if (!m_nview.IsOwner())
                    return;

                // 端数切捨て
                float now = m_nview.GetZDO().GetFloat("fuel", 0f);
                float size = Mathf.Clamp(now + count, 0f, instance.m_maxFuel);
                m_nview.GetZDO().Set("fuel", size);
                instance.m_fuelAddedEffects.Create(
                    instance.transform.position, instance.transform.rotation, null, 1f);
                ZLog.Log($"Added fuel * {count}");

                Traverse.Create(instance).Method("UpdateState").GetValue();
            }
        }

        private static class Utility
        {
            /// <summary>
            /// 探索範囲内のコンテナを取得
            /// </summary>
            /// <param name="center">探索中心点</param>
            /// <returns></returns>
            public static List<Container> GetNearByContainer(Vector3 center)
            {
                float sqrRange = UseFromContainerRange.Value * UseFromContainerRange.Value;
                List<Container> containers = Containers.Where(n =>
                    n != null && n.transform != null && n.GetInventory() != null &&
                    (n.transform.position - center).sqrMagnitude < sqrRange &&
                    Traverse.Create(n).Method("CheckAccess", new object[] { Player.m_localPlayer.GetPlayerID() }).GetValue<bool>())?.ToList();

                if (containers == null || containers.Count() == 0)
                    return null;

                return containers;
            }
        }
    }
}