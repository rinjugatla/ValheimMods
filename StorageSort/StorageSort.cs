using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;


namespace StorageSort
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BepInExPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rin_jugatla.StorageSort";
        public const string PluginName = "StorageSort";
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

        void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 36, "Nexus mod ID for updates");

            if (!IsEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(Container), "Interact")]
        public static class ModifyContainerInteract
        {
            private static bool Prefix(Container __instance, ref Humanoid character, ref bool hold)
            {
                // 他人が使用している場合は処理なし
                if (hold)
                    return true;

                // 開く直前に整頓
                Inventory inventory = __instance.GetInventory();
                if (inventory != null)
                {
                    List<ItemDrop.ItemData> items = inventory.GetAllItems();
                    StringBuilder sb = new StringBuilder("\n");
                    if (IsDebug)
                    {
                        foreach (var item in items)
                        {
                            Vector2i pos = item.m_gridPos;
                            sb.Append($"{item.m_shared.m_name}({item.m_stack}) = ({pos.x}, {pos.y})\n");
                        }
                        string localized = Localization.instance.Localize(sb.ToString());
                        UnityEngine.Debug.Log(localized);
                    }

                    // 名前順, スタック多い順で並べ替え
                    List<ItemDrop.ItemData> sorted = items.OrderBy(name => name.m_shared.m_name).ThenByDescending(stack => stack.m_stack).ToList();

                    int maxWidth = inventory.GetWidth();
                    int maxHeight = inventory.GetHeight();
                    // 左上から詰める
                    int width = 0;
                    int height = 0;
                    foreach (var item in sorted)
                    {
                        item.m_gridPos = new Vector2i(width++, height);
                        if (width == maxWidth)
                        {
                            width = 0;
                            height++;
                        }
                    }

                    Inventory privateInventory = Traverse.Create(__instance).Field("m_inventory").GetValue<Inventory>();
                    Traverse.Create(privateInventory).Field("m_inventory").SetValue(sorted);
                }
                return true;
            }
        }
    }
}
