﻿using Harmony;
using RimWorld;
using RimWorld.Planet;
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
		public static bool noPauseEnabled = false;
		public static bool closeTradeDialog = false;
		public static TimeSpeed lastTimeSpeed = TimeSpeed.Paused;
		public static Texture2D[] originalSpeedButtonTextures;

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
			// HarmonyInstance.DEBUG = true;
			var harmony = HarmonyInstance.Create("net.pardeike.harmony.NoPauseChallenge");
			harmony.PatchAll();
			AddUltraButton();
			CopyOriginalSpeedButtonTextures();
		}

		static void CopyOriginalSpeedButtonTextures()
		{
			var t_TexButton = AccessTools.TypeByName("Verse.TexButton");
			originalSpeedButtonTextures = Traverse.Create(t_TexButton).Field("SpeedButtonTextures").GetValue<Texture2D[]>();
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

	[HarmonyPatch(typeof(StorytellerUI))]
	[HarmonyPatch("DrawStorytellerSelectionInterface")]
	static class StorytellerUI_DrawStorytellerSelectionInterface_Patch
	{
		static void SetColorPlusOurUX(Color value, Listing_Standard infoListing)
		{
			GUI.color = value;
			infoListing.Gap(3f);
			infoListing.CheckboxLabeled("No Pause Challenge", ref Main.noPauseEnabled, null);
			infoListing.Gap(3f);
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			var m_get_white = AccessTools.Property(typeof(Color), "white").GetGetMethod();
			var m_set_color = AccessTools.Property(typeof(GUI), "color").GetSetMethod();
			var idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Call && instr.operand == m_get_white);
			if (idx < 0 || idx >= list.Count || list[idx + 1].opcode != OpCodes.Call || list[idx + 1].operand != m_set_color)
				Log.Error("Cannot find first 'GUI.color = Color.white' in TimeControls.DoTimeControlsGUI");
			else
			{
				list[idx + 1].operand = SymbolExtensions.GetMethodInfo(() => SetColorPlusOurUX(Color.clear, null));
				list.Insert(idx + 1, new CodeInstruction(OpCodes.Ldarg_3));
			}
			return list;
		}
	}

	[HarmonyPatch(typeof(CameraDriver))]
	[HarmonyPatch("Expose")]
	static class CameraDriver_Expose_Patch
	{
		static void Postfix()
		{
			try
			{
				Scribe_Values.Look(ref Main.noPauseEnabled, "noPause", false, false);
			}
			catch (System.Exception)
			{
				Main.noPauseEnabled = false;
			}
		}
	}

	[HarmonyPatch(typeof(Game))]
	[HarmonyPatch("FinalizeInit")]
	static class Game_FinalizeInit_Patch
	{
		static void Postfix()
		{
			if (Main.noPauseEnabled)
				ModCounter.Trigger();
		}
	}

	[HarmonyPatch(typeof(GameComponentUtility))]
	[HarmonyPatch("LoadedGame")]
	static class GameComponentUtility_LoadedGame_Patch
	{
		static void Postfix()
		{
			if (Main.noPauseEnabled == false)
				return;

			LongEventHandler.ExecuteWhenFinished(delegate
			{
				var tm = Find.TickManager;
				if (tm.CurTimeSpeed == TimeSpeed.Paused)
					tm.CurTimeSpeed = TimeSpeed.Normal;
			});
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("Paused", MethodType.Getter)]
	class TickManager_Paused_Patch
	{
		static bool Prefix(ref bool __result)
		{
			if (Main.noPauseEnabled == false)
				return true;

			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(WorldRoutePlanner))]
	[HarmonyPatch("ShouldStop", MethodType.Getter)]
	class WorldRoutePlanner_ShouldStop_Patch
	{
		static bool Prefix(bool ___active, ref bool __result)
		{
			if (Main.noPauseEnabled == false)
				return true;

			__result = !___active || !WorldRendererUtility.WorldRenderedNow;
			return false;
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("CurTimeSpeed", MethodType.Setter)]
	class TickManager_CurTimeSpeed_Patch
	{
		static bool Prefix(ref TimeSpeed value)
		{
			if (Main.noPauseEnabled == false)
				return true;
			return value != TimeSpeed.Paused;
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("TogglePaused")]
	class TickManager_TogglePaused_Patch
	{
		static bool Prefix()
		{
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(TimeSlower))]
	[HarmonyPatch("SignalForceNormalSpeed")]
	class TimeSlower_SignalForceNormalSpeed_Patch
	{
		static bool Prefix()
		{
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(TimeSlower))]
	[HarmonyPatch("SignalForceNormalSpeedShort")]
	class TimeSlower_SignalForceNormalSpeedShort_Patch
	{
		static bool Prefix()
		{
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(LordToil_ExitMapAndEscortCarriers))]
	[HarmonyPatch("UpdateTraderDuty")]
	class LordToil_ExitMapAndEscortCarriers_UpdateTraderDuty_Patch
	{
		static void Postfix()
		{
			if (Main.noPauseEnabled)
				Main.closeTradeDialog = true;
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade))]
	[HarmonyPatch("PostOpen")]
	class Dialog_Trade_PostOpen_Patch
	{
		static void Postfix()
		{
			if (Main.noPauseEnabled)
				Main.closeTradeDialog = false;
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade))]
	[HarmonyPatch("DoWindowContents")]
	class Dialog_Trade_DoWindowContents_Patch
	{
		static bool Prefix(Dialog_Trade __instance)
		{
			if (Main.noPauseEnabled == false)
				return true;

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
			if (Main.noPauseEnabled == false)
				return Main.originalSpeedButtonTextures[(int)timeSpeed];

			if (current == index)
				return Main.SpeedButtonTexturesActive[(int)timeSpeed];
			return Main.SpeedButtonTextures[(int)timeSpeed];
		}

		static int GetTimeSpeedVarValue(TimeSpeed timeSpeed)
		{
			return Main.noPauseEnabled ? -1 : (int)timeSpeed;
		}

		static int ConditionalLoopStart()
		{
			return Main.noPauseEnabled ? 1 : 0;
		}

		static int ConditionalUltaMultiplier()
		{
			return Main.noPauseEnabled ? 2 : 1;
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			int idx;

			idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Ldc_I4_0);
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldc_I4.0 in TimeControls.DoTimeControlsGUI");
			else
			{
				list[idx].opcode = OpCodes.Call;
				list[idx].operand = SymbolExtensions.GetMethodInfo(() => ConditionalLoopStart());
			}

			var f_HighlightTex = AccessTools.Field(typeof(TexUI), nameof(TexUI.HighlightTex));
			var speedCompareOperands = new List<CodeInstruction>();
			idx = list.FirstIndexOf(instr => instr.opcode == OpCodes.Ldsfld && instr.operand == f_HighlightTex) - 5;
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldsfld TexUI.HighlightTex in TimeControls.DoTimeControlsGUI");
			else
			{
				speedCompareOperands = list.GetRange(idx, 3).Select(instr => instr.Clone()).ToList();
				list.Insert(idx + 3, new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => GetTimeSpeedVarValue(TimeSpeed.Normal))));
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
				list.Insert(idx + 1, new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => ConditionalUltaMultiplier())));
				list.Insert(idx + 2, new CodeInstruction(OpCodes.Mul));
			}

			return list;
		}

		static void Prefix()
		{
			if (Main.noPauseEnabled == false)
				return;

			if (Event.current.type == EventType.KeyDown)
			{
				if (KeyBindingDefOf.TogglePause.KeyDownEvent)
				{
					var tm = Find.TickManager;
					if (tm.CurTimeSpeed == TimeSpeed.Paused || Main.lastTimeSpeed == TimeSpeed.Paused)
					{
						tm.CurTimeSpeed = TimeSpeed.Normal;
						Main.lastTimeSpeed = TimeSpeed.Normal;
					}
					else
					{
						if (tm.CurTimeSpeed == TimeSpeed.Normal)
							tm.CurTimeSpeed = Main.lastTimeSpeed;
						else
						{
							Main.lastTimeSpeed = tm.CurTimeSpeed;
							tm.CurTimeSpeed = TimeSpeed.Normal;
						}
					}

					Event.current.Use();
				}
			}
		}
	}
}