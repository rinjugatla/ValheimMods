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
        public const string PluginVersion = "1.2.0";
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

        private void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 107, "Nexus mod ID for updates");
            ModifierKey = Config.Bind<string>("General", "ModifierKey", "left shift", "Modifier keys for using mods");
            IsReverseModifierMode = Config.Bind<bool>("General", "IsReverseModifierMode", false, "false: Batch submit with ModifierKey + UseKey. true: Batch submit with UseKey.");

            if (!IsEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        // 炭焼き窯、溶解炉
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        public static class ModifySmelterOnAddOre
        {
            private static void Postfix(Smelter __instance, ref Switch sw, ref Humanoid user, ref ItemDrop.ItemData item)
            {
                if (IsDebug)
                    Debug.Log("OnAddOre");

                if (Input.GetKey(ModifierKey.Value) && IsReverseModifierMode.Value ||
                    !Input.GetKey(ModifierKey.Value) && !IsReverseModifierMode.Value)
                    return;

                if (item == null)
                {
                    // インベントリからアイテムを取得
                    item = Traverse.Create(__instance).Method("FindCookableItem", user.GetInventory()).GetValue<ItemDrop.ItemData>();
                    if (item == null)
                        return;
                }

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
