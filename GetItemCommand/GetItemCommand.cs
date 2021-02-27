using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
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
        public static ConfigEntry<bool> IsEnabled;
        /// <summary>
        /// 
        /// </summary>
        public static ConfigEntry<string> AgreementKey;
        /// <summary>
        /// 自動アップデート用のNexusID
        /// </summary>
        /// <remarks>
        /// https://www.nexusmods.com/valheim/mods/102
        /// </remarks>
        public static ConfigEntry<int> NexusID;

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
                if(text.ToLower() == "/getitem")
                {
                    Utility.PostText(__instance, "/getitem [name] - You get the maximum number of items.");
                    Utility.PostText(__instance, "/getitem [name] [int] - You get [int] number of items.");
                    return false;
                }
                if (!text.ToLower().StartsWith("/getitem "))
                    return true;

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

                // アイテム情報取得
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(name);
                if (!itemPrefab)
                {
                    Utility.PostText(__instance, $"An unknown item name({name}) was entered.");
                    return false;
                }

                // アイテムのポップ位置設定
                Vector3 position = Player.m_localPlayer.transform.position;
                Vector3 modify = new Vector3(50, 50, 0);
                GameObject itemObject = UnityEngine.Object.Instantiate<GameObject>(itemPrefab, position + modify, default);
                if (itemObject == null)
                {
                    Utility.PostText(__instance, $"Failed to create the ItemObject({name}).");
                    return false;
                }

                ItemDrop drop = itemObject.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    Utility.PostText(__instance, $"Failed to create the ItemDrop({name}).");
                    return false;
                }

                // スタックを最大数または指定数に変更
                int giveNumber = 0;
                if (number == 0)
                     giveNumber = drop.m_itemData.m_shared.m_maxStackSize;
                else
                    giveNumber = Math.Min(
                        drop.m_itemData.m_shared.m_maxStackSize,
                        (int)number);
                drop.m_itemData.m_stack = giveNumber;

                // アイテムを付与
                Player.m_localPlayer.GetInventory().AddItem(drop.m_itemData);
                PlayerProfile profile = Game.instance.GetPlayerProfile();
                string username = profile.GetName();
                long userid = profile.GetPlayerID();
                string message = $"created item({name}) * {giveNumber} with {PluginName}.";
                
                // 記録
                Utility.PostText(__instance, $"I {message}");
                ZLog.Log($"{username}({userid}) {message}");

                return false;
            }
        }

        internal static class Utility
        {
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
