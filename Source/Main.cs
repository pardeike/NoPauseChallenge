using Harmony;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace NoPauseChallenge
{
	[StaticConstructorOnStartup]
	public class Main
	{
		public static bool closeTradeDialog = false;

		public static readonly Texture2D[] SpeedButtonTextures = new Texture2D[]
		{
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Pause", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Normal", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Fast", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Superfast", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Ultrafast", true)
		};
		public static readonly Texture2D[] SpeedButtonTexturesActive = new Texture2D[]
		{
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Pause_Active", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Normal_Active", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Fast_Active", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Superfast_Active", true),
			ContentFinder<Texture2D>.Get("TimeSpeedButton_Ultrafast_Active", true)
		};

		static Main()
		{
			//HarmonyInstance.DEBUG = true;
			var harmony = HarmonyInstance.Create("net.pardeike.harmony.NoPauseChallenge");
			harmony.PatchAll();
			AddUltraButton();
		}

		static void AddUltraButton()
		{
			var texButtonClass = AccessTools.TypeByName("Verse.TexButton");
			if (texButtonClass == null)
				Log.Error("Cannot get Verse.TexButton");
			var speedButtonTextures = Traverse.Create(texButtonClass).Field("SpeedButtonTextures");
			var textures = speedButtonTextures.GetValue<Texture2D[]>();
			var tex = ContentFinder<Texture2D>.Get("TimeSpeedButton_Ultrafast", true);
			textures[4] = tex;
			speedButtonTextures.SetValue(textures);
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("Paused", MethodType.Getter)]
	class TickManager_Paused_Patch
	{
		static bool Prefix(ref bool __result)
		{
			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("CurTimeSpeed", MethodType.Setter)]
	class TickManager_CurTimeSpeed_Patch
	{
		static bool Prefix(ref TimeSpeed value)
		{
			return value != TimeSpeed.Paused;
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("TogglePaused")]
	class TickManager_TogglePaused_Patch
	{
		static bool Prefix()
		{
			return false;
		}
	}

	[HarmonyPatch(typeof(TimeSlower))]
	[HarmonyPatch("SignalForceNormalSpeed")]
	class TimeSlower_SignalForceNormalSpeed_Patch
	{
		static bool Prefix()
		{
			return false;
		}
	}

	[HarmonyPatch(typeof(TimeSlower))]
	[HarmonyPatch("SignalForceNormalSpeedShort")]
	class TimeSlower_SignalForceNormalSpeedShort_Patch
	{
		static bool Prefix()
		{
			return false;
		}
	}

	[HarmonyPatch(typeof(LordToil_ExitMapAndEscortCarriers))]
	[HarmonyPatch("UpdateTraderDuty")]
	class LordToil_ExitMapAndEscortCarriers_UpdateTraderDuty_Patch
	{
		static void Postfix()
		{
			Main.closeTradeDialog = true;
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade))]
	[HarmonyPatch("PostOpen")]
	class Dialog_Trade_PostOpen_Patch
	{
		static void Postfix()
		{
			Main.closeTradeDialog = false;
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade))]
	[HarmonyPatch("DoWindowContents")]
	class Dialog_Trade_DoWindowContents_Patch
	{
		static bool Prefix(Dialog_Trade __instance)
		{
			/*var tradable = true;
			if (TradeSession.Active == false)
				tradable = false;
			else
			{
				var trader = TradeSession.trader;
				if (trader.CanTradeNow == false)
					tradable = false;
			}*/

			if (Main.closeTradeDialog)
			{
				__instance.Close(true);
				return false;
			}
			return true;
		}

		static bool TryExecute(TradeDeal instance, out bool actuallyTraded)
		{
			actuallyTraded = false;
			try
			{
				return instance.TryExecute(out actuallyTraded);
			}
			catch (System.Exception)
			{
			}
			return false;
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool _bool;
			var from = SymbolExtensions.GetMethodInfo(() => new TradeDeal().TryExecute(out _bool));
			var to = SymbolExtensions.GetMethodInfo(() => TryExecute(null, out _bool));
			return Transpilers.MethodReplacer(instructions, from, to);
		}
	}

	[HarmonyPatch(typeof(TimeControls))]
	[HarmonyPatch("DoTimeControlsGUI")]
	class TimeControls_DoTimeControlsGUI_Patch
	{
		static Texture2D GetButtonTexture(TimeSpeed timeSpeed, TimeSpeed current, TimeSpeed index)
		{
			if (current == index)
				return Main.SpeedButtonTexturesActive[(int)timeSpeed];
			return Main.SpeedButtonTextures[(int)timeSpeed];
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			int idx;

			idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Ldc_I4_0);
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldc_I4.0 in TimeControls.DoTimeControlsGUI");
			else
				list[idx].opcode = OpCodes.Ldc_I4_1;

			var f_HighlightTex = AccessTools.Field(typeof(TexUI), nameof(TexUI.HighlightTex));
			var speedCompareOperands = new List<CodeInstruction>();
			idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Ldsfld && instr.operand == f_HighlightTex) - 5;
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldsfld TexUI.HighlightTex in TimeControls.DoTimeControlsGUI");
			else
			{
				speedCompareOperands = list.GetRange(idx, 3).Select(instr => instr.Clone()).ToList();
				list[idx + 2].opcode = OpCodes.Ldc_I4;
				list[idx + 2].operand = -1;
			}

			var t_TexButton = AccessTools.TypeByName("Verse.TexButton");
			if (t_TexButton == null)
				Log.Error("Cannot get Verse.TexButton");
			var f_SpeedButtonTextures = AccessTools.Field(t_TexButton, "SpeedButtonTextures");
			if (f_SpeedButtonTextures == null)
				Log.Error("Cannot get TexButton.SpeedButtonTextures");
			idx = list.FirstIndexOf(instr => instr.operand == f_SpeedButtonTextures);
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find operand TexButton.SpeedButtonTextures in TimeControls.DoTimeControlsGUI");
			else
			{
				list.RemoveAt(idx);
				list[idx + 1] = new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => GetButtonTexture(0, 0, 0)));
				list.InsertRange(idx + 1, speedCompareOperands);
			}

			idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Ldc_I4_4);
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldc_I4.4 in TimeControls.DoTimeControlsGUI");
			else
			{
				list[idx].opcode = OpCodes.Ldc_I4;
				list[idx].operand = -1;
			}

			return list;
		}
	}
}