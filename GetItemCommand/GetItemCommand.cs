﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GetItemCommand
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BepInExPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "rin_jugatla.GetItemCommand";
        public const string PluginName = "GetItemCommand";
        public const string PluginVersion = "1.1.2";
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

        private void Awake()
        {
            IsEnabled = Config.Bind<bool>("General", "Enabled", false, "Enable this mod");
            NexusID = Config.Bind<int>("General", "NexusID", 148, "Nexus mod ID for updates");
            AgreementKey = Config.Bind<string>("General", "AgreementKey", "",
                "Please do not use this maliciously. The strings to be entered are described in the distribution page.");

            if (!IsEnabled.Value)
                return;

            if (AgreementKey.Value != "e198cb46-2e57-433f-921c-575fa044e421")
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
                if (IsDebug)
                    Debug.Log("InputText");

                string text = __instance.m_input.text;
                string lower = text.ToLower();
                if (lower == "/getitem" || lower == "/gi")
                {
                    Utility.PostText(__instance, "/getitem [name] - You get the maximum number of items.");
                    Utility.PostText(__instance, "/getitem [name] [int] - You get [int] number of items.");
                    return false;
                }
                else if(lower.StartsWith("/getitem ") || lower.StartsWith("/gi "))
                    return GetItem(__instance, text);

                if (lower == "/getitemlist" || lower == "/gil")
                {
                    return ExportItemList(__instance);
                }


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
                if (array.Length == 1)
                {
                    Utility.PostText(__instance, "Parameters are missing.");
                    return false;
                }

                // アイテム名と数を取得
                string name = array[1];
                uint number = 0;
                if (array.Length > 2)
                    if (!uint.TryParse(array[2], out number))
                    {
                        Utility.PostText(__instance, "Enter the number of items as a positive integer.");
                        return false;
                    }

                var item = ValidItemList.Where(n => n.name == name).FirstOrDefault();
                if(item == null)
                {
                    Utility.PostText(__instance, $"An unknown item name({name}) was entered.");
                    return false;
                }

                // スタックを最大数または指定数に変更
                int giveNumber = 0;
                if (number == 0)
                    giveNumber = item.m_itemData.m_shared.m_maxStackSize;
                else
                    giveNumber = Math.Min(
                        item.m_itemData.m_shared.m_maxStackSize,
                        (int)number);

                // アイテムを付与
                Player.m_localPlayer.GetInventory().AddItem(name, giveNumber, item.m_itemData.m_quality, item.m_itemData.m_variant, 
                    Player.m_localPlayer.GetPlayerID(), Player.m_localPlayer.GetPlayerName());
                
                // ログ
                PlayerProfile profile = Game.instance.GetPlayerProfile();
                string username = profile.GetName();
                long userid = profile.GetPlayerID();
                string message = $"created item({name}) * {giveNumber} with {PluginName}.";
                Utility.PostText(__instance, $"I {message}");
                ZLog.Log($"{username}({userid}) {message}");

                return false;
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
                    Utility.PostText(__instance, ex.Message);
                }

                return false;
            }
        }

        internal static class Utility
        {
            /// <summary>
            /// アイテムリスト取得
            /// </summary>
            /// <returns></returns>
            public static List<ItemDrop> GetValidItemData()
            {
                var result = new List<ItemDrop>();
                foreach (ItemDrop.ItemData.ItemType type in Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)))
                {
                    List<ItemDrop> drops = ObjectDB.instance.GetAllItems(type, "");;
                    result.AddRange(drops.Where(n => n.m_itemData.m_shared.m_icons.Length != 0));
                }

                return result;
            }

            /// <summary>
            /// チャットにテキストを送信
            /// </summary>
            /// <param name="__instance"></param>
            /// <param name="text">送信文字列</param>
            public static void PostText(Chat __instance, string text)
            {
                if (text == null)
                    return;
                if (IsDebug)
                    Debug.Log(text);

                __instance.SendText(Talker.Type.Whisper, text);
            }
        }
    }
}
