using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static ItemDrop.ItemData;

namespace GetItemCommand
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BepInExPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rin_jugatla.GetItemCommand";
        public const string PluginName = "GetItemCommand";
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
        /// 使用確認鍵(MOD配布ページに記載)
        /// </summary>
        public static ConfigEntry<string> AgreementKey;
        /// <summary>
        /// 自動アップデート用のNexusID
        /// </summary>
        /// <remarks>
        /// https://www.nexusmods.com/valheim/mods/102
        /// </remarks>
        public static ConfigEntry<int> NexusID;
        /// <summary>
        /// アイテムリスト
        /// </summary>
        private static IReadOnlyList<ItemDrop> ValidItemList;
        /// <summary>
        /// チャットインスタンス
        /// </summary>
        private static Chat ChatInstance;

        private void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", false, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 148, "Nexus mod ID for updates");
            AgreementKey = Config.Bind<string>("General", "AgreementKey", "",
                "Please do not use this maliciously. The strings to be entered are described in the distribution page.");

            if (!IsEnabled.Value)
                return;

            string key = Utility.GetAgreementKey();
            if (key == null)
                return;

            if (AgreementKey.Value != key)
                return;

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static class ModifyObjectDBAwake
        {
            private static void Postfix(ObjectDB __instance)
            {
                ValidItemList = Utility.GetValidItemData();
            }
        }

        [HarmonyPatch(typeof(Chat), "Awake")]
        private static class ModifyChatAwake
        {
            private static void Postfix(Chat __instance)
            {
                ChatInstance = __instance;
            }
        }

        [HarmonyPatch(typeof(Chat), "InputText")]
        private static class ModifyChatInputText
        {
            /// <summary>
            /// コマンド検出
            /// </summary>
            /// <param name="__instance"></param>
            /// <returns></returns>
            private static bool Prefix(Chat __instance)
            {
                string text = __instance.m_input.text;
                string lower = text.ToLower();
                if (lower == "/getitem" || lower == "/gi")
                {
                    Utility.PostChatMyself("Non-equipment");
                    Utility.PostChatMyself(" /getitem [name] - You get the maximum number of items.");
                    Utility.PostChatMyself(" /getitem [name] [x] - You get [x] number of items.");

                    Utility.PostChatMyself("Equipment");
                    Utility.PostChatMyself(" /getitem [name] - Gain equipment of quality 1.");
                    Utility.PostChatMyself(" /getitem [name] [x] - Gain equipment of quality x.");
                    Utility.PostChatMyself(" /getitem [name] [x] [y] - Equipment of quality x and pattern y is acquired.");
                    Utility.PostChatMyself("Other");
                    Utility.PostChatMyself(" /getitemlist - Output the item list file.");
                    Utility.PostChatMyself(" /getitemsearch [str] - Search for items containing \"str\".");
                    return false;
                }
                else if(lower.StartsWith("/getitem ") || lower.StartsWith("/gi "))
                    return GetItem(__instance, text);

                if (lower == "/getitemlist" || lower == "/gil")
                {
                    return ExportItemList(__instance);
                }

                if (lower.StartsWith("/getitemsearch ") || lower.StartsWith("/gis "))
                    return SearchItem(text);

                return true;
            }

            /// <summary>
            /// アイテム獲得
            /// </summary>
            /// <param name="__instance"></param>
            /// <param name="text">入力文字列</param>
            /// <returns></returns>
            private static bool GetItem(Chat __instance, string text)
            {
                // パラメータ取得
                string[] array = text.Split(' ');

                // アイテム名取得
                string name = array[1];
                var item = ValidItemList.Where(n => n.name == name).FirstOrDefault();
                if (item == null)
                {
                    Utility.PostChatMyself($"An unknown item name({name}) was entered.");
                    return false;
                }

                // アイテムの種類によって引数の扱いを変える
                ItemDrop.ItemData.ItemType type = item.m_itemData.m_shared.m_itemType;
                bool isEquipment = IsEquipment(type);
                uint quality = 1;
                uint variant = 0;
                uint stack = 0;
                
                if (isEquipment)
                {
                    // 品質
                    if (array.Length >= 3 && !uint.TryParse(array[2], out quality))
                    {
                        Utility.PostChatMyself("The quality should be specified as an integer.");
                        return false;
                    }
                    // 模様
                    if (array.Length >= 4 && !uint.TryParse(array[3], out variant))
                    {
                        Utility.PostChatMyself("The pattern should be specified as an integer.");
                        return false;
                    }
                }
                else
                {
                    // 数量
                    if (array.Length >= 3 && !uint.TryParse(array[2], out stack))
                    {
                        Utility.PostChatMyself("Enter the number of items as a positive integer.");
                        return false;
                    }
                }

                // 品質補正
                int getQuality = Math.Min(
                        item.m_itemData.m_shared.m_maxQuality,
                        (int)quality);

                // 模様補正 存在しない場合は初期値を使用
                int getVariant = (int)variant;
                if (variant >= item.m_itemData.m_shared.m_icons.Length)
                    getVariant = 0;

                // スタックを最大数または指定数に変更
                int getStack = 0;
                if (stack == 0)
                    getStack = item.m_itemData.m_shared.m_maxStackSize;
                else
                    getStack = Math.Min(
                        item.m_itemData.m_shared.m_maxStackSize,
                        (int)stack);

                if(IsDebug)
                    Debug.Log($"{name}\n" +
                        $"max_stack: {item.m_itemData.m_shared.m_maxStackSize}\n" +
                        $"max_quality: {item.m_itemData.m_shared.m_maxQuality}\n" +
                        $"variants {item.m_itemData.m_shared.m_variants}\n" +
                        $"icon {item.m_itemData.m_shared.m_icons.Length}\n" +
                        $"getStask: {getStack}\n" +
                        $"getQuality: {getQuality}\n" +
                        $"getVariant: {getVariant}");

                // アイテムを付与
                Player.m_localPlayer.GetInventory().AddItem(name, getStack, getQuality, getVariant,
                    Player.m_localPlayer.GetPlayerID(), Player.m_localPlayer.GetPlayerName());

                // ログ
                PlayerProfile profile = Game.instance.GetPlayerProfile();
                string username = profile.GetName();
                long userid = profile.GetPlayerID();
                string message = $"created item({name}) * {getStack} with {PluginName}.";
                Utility.PostChatShout($"I {message}");
                ZLog.Log($"{username}({userid}) {message}");

                return false;
            }

            /// <summary>
            /// アイテムタイプが装備種か
            /// </summary>
            /// <param name="type">アイテムタイプ</param>
            /// <returns></returns>
            private static bool IsEquipment(ItemType type)
            {
                switch (type)
                {
                    case ItemType.Material:
                    case ItemType.Ammo:
                    case ItemType.Trophie:
                    case ItemType.Misc:
                        return false;
                    case ItemType.OneHandedWeapon:
                    case ItemType.TwoHandedWeapon:
                    case ItemType.Bow:
                    case ItemType.Shield:
                    case ItemType.Legs:
                    case ItemType.Hands:
                    case ItemType.Helmet:
                    case ItemType.Shoulder:
                    case ItemType.Chest:
                    case ItemType.Tool:
                    case ItemType.Utility:
                    case ItemType.Torch:
                        return true;
                    case ItemType.None:
                    case ItemType.Customization:
                    case ItemType.Consumable:
                    case ItemType.Attach_Atgeir:
                    default:
                        return false;
                }
            }

            /// <summary>
            /// アイテムリストファイルを出力
            /// </summary>
            /// <remarks>
            /// 出力先: <GameDirectory>/BepInEx/plugins/
            /// </remarks>
            /// <returns></returns>
            private static bool ExportItemList(Chat __instance)
            {
                string filepath = @".\BepInEx\plugins\GetItemCommand_ItemList.txt";


                StringBuilder sb = new StringBuilder();
                foreach (ItemDrop.ItemData.ItemType type in Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)))
                {
                    sb.Append($"-----{Enum.GetName(typeof(ItemDrop.ItemData.ItemType), type)}-----\n");
                    List<ItemDrop> drops = ObjectDB.instance.GetAllItems(type, "");
                    // iconが存在しない場合は無効なアイテム
                    sb.AppendLine(string.Join("\n", 
                        drops.Where(n => n.m_itemData.m_shared.m_icons.Length != 0).Select(n => n.name)));
                }

                try
                {
                    using (StreamWriter sw = new StreamWriter(filepath, false, Encoding.UTF8))
                        sw.Write(sb.ToString());
                }
                catch (Exception ex)
                {
                    Utility.PostChatMyself(ex.Message);
                }

                return false;
            }

            /// <summary>
            /// 指定の文字列が含まれるアイテム名を検索
            /// </summary>
            /// <returns></returns>
            private static bool SearchItem(string text)
            {
                // パラメータ取得
                string[] array = text.Split(' ');

                // アイテム名取得
                string name = array[1];
                var names = ValidItemList.Where(n => n.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) > -1).Select(n => n.name).Take(8);
                if (names == null)
                    Utility.PostChatMyself($"Item not found.");
                else
                    Utility.PostChatMyself($"The following items were found.\n {string.Join("\n ", names)}");
                return false;
            }
        }

        internal static class Utility
        {
            /// <summary>
            /// 認証キーを取得
            /// </summary>
            /// <returns></returns>
            public static string GetAgreementKey()
            {
                WebClient client = new WebClient();
                string source = null;
                try
                {
                    client.Encoding = Encoding.UTF8;
                    source = client.DownloadString(new Uri("https://www.nexusmods.com/valheim/mods/148"));
                }
                catch (Exception ex)
                {
                    Debug.Log($"{PluginName}: Error {ex.Message}");
                    return null;
                }
                
                string[] lines = source.Split(
                    new[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.None
                );

                foreach (var line in lines)
                {
                    Match match = Regex.Match(line, "Change \"AgreementKey\" to \"([\\d\\D]+)\"\\.");
                    if (!match.Success)
                        continue;

                    string key = match.Groups[1].Value;
                    if(IsDebug)
                        Debug.Log($"{PluginName}: Key {match.Value} -> {key}");
                    return key;
                }

                return null;
            }

            /// <summary>
            /// アイテムリスト取得
            /// </summary>
            /// <returns></returns>
            public static List<ItemDrop> GetValidItemData()
            {
                var result = new List<ItemDrop>();
                foreach (ItemDrop.ItemData.ItemType type in Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)))
                {
                    List<ItemDrop> drops = ObjectDB.instance.GetAllItems(type, "");
                    result.AddRange(drops.Where(n => n.m_itemData.m_shared.m_icons.Length != 0));
                }

                return result;
            }

            /// <summary>
            /// 自分にだけ見えるチャットを表示
            /// </summary>
            /// <param name="text"></param>
            public static void PostChatMyself(string text)
            {
                if (text == null)
                    return;

                Traverse.Create(ChatInstance).Method("AddString", new object[] { text }).GetValue();
            }

            /// <summary>
            /// チャットにテキストを送信
            /// </summary>
            /// <param name="__instance"></param>
            /// <param name="text">送信文字列</param>
            public static void PostChatShout(string text)
            {
                if (text == null)
                    return;

                ChatInstance.SendText(Talker.Type.Shout, text);
            }
        }
    }
}
