using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SandBox
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class BepInExPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "rin_jugatla.SandBox";
		public const string PluginName = "SandBox";
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
		/// ヒューマノイド
		/// </summary>
		private static Humanoid HumanoidInstance;

		private void Awake()
		{
			Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
		}

		[HarmonyPatch(typeof(Humanoid), "Awake")]
		private static class ModifyHumanoidAwake
		{
			private static void Postfix(Humanoid __instance)
			{
				HumanoidInstance = __instance;
			}
		}

		[HarmonyPatch(typeof(CraftingStation), "Interact")]
		private static class ModifiyCraftingStationInteract
		{
			private static void Postfix(CraftingStation __instance)
            {
				Debug.Log("CraftingStation: " +__instance.name);
            }
		}

		[HarmonyPatch(typeof(CookingStation), "Interact")]
		private static class ModifiyCookingStationInteract
		{
			private static void Postfix(CraftingStation __instance)
			{
				Debug.Log("CookingStation: " + __instance.name);
			}
		}

		[HarmonyPatch(typeof(Fireplace), "Interact")]
		private static class ModifiyFireplaceInteract
		{
			private static void Postfix(Fireplace __instance)
			{
				Debug.Log("Fireplace: " + __instance.name);
			}
		}

		[HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
		private static class ModifiyTreeBaseRPC_Damage
		{
			private static void Postfix(TreeBase __instance)
			{
				Debug.Log("TreeBase: " + __instance.name);
			}
		}

		[HarmonyPatch(typeof(TreeLog), "RPC_Damage")]
		private static class ModifiyTreeLogRPC_Damage
		{
			private static void Postfix(TreeLog __instance)
			{
				Debug.Log("TreeLog: " + __instance.name);
			}
		}

		/// <summary>
		/// チャット処理 チャットにテキストを表示(他人には見えない)
		/// </summary>
		[HarmonyPatch(typeof(Chat), "InputText")]
		private static class ModifyChatInputText
		{
			private static bool Prefix(Chat __instance)
			{
				if (IsDebug)
					Debug.Log("InputText");

				string text = __instance.m_input.text;
				string lower = text.ToLower();
				if (lower == "/test")
				{
					Traverse.Create(__instance).Method("AddString", new object[]
						{
							"hogehoge"
						}).GetValue();

                    ZRpc serverRcp = ZNet.instance.GetServerRPC();
                    if (serverRcp != null)
                    {
						ZNet.instance.RemotePrint(serverRcp, "hoge");
						//ZNetPeer peer = ZNet.instance.GetPeer(serverRcp);
					}

                    


                    // だめ
                    //ZRpc serverRcp = ZNet.instance.GetServerRPC();
                    //if (serverRcp != null)
                    //    serverRcp.Invoke("Log", Array.Empty<object>());

                    // だめ
                    //ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ChatMessage", new object[]
                    //{
                    //	(int)MessageHud.MessageType.Center,
                    //	"hogehoge"
                    //});
                    return false;
				}

				return true;
			}
		}


		[HarmonyPatch(typeof(ItemStand), "DropItem")]
		private static class ModifiyItemStandDropItem
		{
			// Token: 0x06000005 RID: 5 RVA: 0x000021E8 File Offset: 0x000003E8
			private static void Prefix(ItemStand __instance)
			{
				ZNetView value = Traverse.Create(__instance).Field("m_nview").GetValue<ZNetView>();
				string @string = value.GetZDO().GetString("item", "");
				bool flag = @string != null;
				if (flag)
				{
					Debug.Log("DropItem: " + @string);
				}
				else
				{
					Debug.Log("DropItem: null");
				}
			}
		}
	}
}
