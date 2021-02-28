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
        public const string PluginGuid = "rin_jugatla.AddAllFuel";
        public const string PluginName = "AddAllFuel";
        public const string PluginVersion = "1.2.1";
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
        /// <summary>
        /// 一括投入時の修飾キー
        /// </summary>
        public static ConfigEntry<string> ModifierKey;
        /// <summary>
        /// 自動投入修飾キーを反転するか
        /// </summary>
        /// <remarks>
        /// def -> false: Eで1つずつ投入、 ModifierKey + Eで一括投入
        /// true: Eで一括投入、　ModifierKey + Eで1つずつ投入
        /// </remarks>
        public static ConfigEntry<bool> IsReverseModifierMode;
        /// <summary>
        /// 一括投入に使用しない木材、鉱石名
        /// </summary>
        /// <remarks>
        /// $item_wood, $item_finewood, $item_roundlog
        /// </remarks>
        public static IReadOnlyList<string> ExcludeNames;

        private void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 107, "Nexus mod ID for updates");
            ModifierKey = Config.Bind<string>("General", "ModifierKey", "left shift", "Modifier keys for using mods");
            IsReverseModifierMode = Config.Bind<bool>("General", "IsReverseModifierMode", false, "false: Batch submit with ModifierKey + UseKey. true: Batch submit with UseKey.");
            ExcludeNames = Config.Bind<string>("General", "ExcludeNames", "", 
                "Name of item not to be used as fuel/ore. Setting example: $item_finewood,$item_roundlog").Value.
                Replace(" ", "").Split(',').ToList();

            if (!IsEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        // 炭焼き窯、溶解炉
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        public static class ModifySmelterOnAddOre
        {
            private static void Postfix(Smelter __instance, ref Humanoid user)
            {
                if (IsDebug)
                    Debug.Log("OnAddOre");

                if (Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                    !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value)
                    return;

                // インベントリからアイテムを取得
                ItemDrop.ItemData item = FindCookableItem(__instance, user.GetInventory());
                if (item == null)
                    return;

                // 追加するアイテム名
                if (IsDebug)
                    Debug.Log(item.m_dropPrefab.name);

                // アイテムの追加が許可されているか
                bool isItemAllowed = Traverse.Create(__instance).Method("IsItemAllowed", item.m_dropPrefab.name).GetValue<bool>();
                if (!isItemAllowed)
                    return;

                // 現在の投入数
                int queueSizeNow = Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
                if (queueSizeNow >= __instance.m_maxOre)
                    return;

                // 残り投入数
                int queueSizeLeft = __instance.m_maxOre - queueSizeNow;
                int queueSize = Math.Min(item.m_stack, queueSizeLeft);
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
            /// <returns></returns>
            private static ItemDrop.ItemData FindCookableItem(Smelter __instance, Inventory inventory)
            {
                // 除外されている燃料以外のアイテム取得
                IEnumerable<string> names = __instance.m_conversion.
                    Where(n => !ExcludeNames.Contains(n.m_from.m_itemData.m_shared.m_name)).
                    Select(n => n.m_from.m_itemData.m_shared.m_name);
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
        public static class ModifySmelterOnAddFuel
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
                if(IsDebug)
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
    }
}
