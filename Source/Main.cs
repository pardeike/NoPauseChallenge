using HarmonyLib;
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
	public static class Main
	{
		public static bool noPauseEnabled = false;
		public static bool fullPauseActive = false;
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
			var harmony = new Harmony("net.pardeike.harmony.NoPauseChallenge");
			harmony.PatchAll();
			AddUltraButton();
			CopyOriginalSpeedButtonTextures();
		}

		static void CopyOriginalSpeedButtonTextures()
		{
			originalSpeedButtonTextures = TexButton.SpeedButtonTextures;
		}

		static void AddUltraButton()
		{
			TexButton.SpeedButtonTextures[4] = ContentFinder<Texture2D>.Get("TimeSpeedButton_Ultrafast", true);
		}
	}

	[HarmonyPatch(typeof(StorytellerUI), nameof(StorytellerUI.DrawStorytellerSelectionInterface))]
	static class StorytellerUI_DrawStorytellerSelectionInterface_Patch
	{
		static void AddCheckbox(Listing_Standard infoListing, float gap)
		{
			infoListing.Gap(gap);
			infoListing.CheckboxLabeled("No Pause Challenge", ref Main.noPauseEnabled, null);
			infoListing.Gap(3f);
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			var m_get_ProgramState = AccessTools.PropertyGetter(typeof(Current), nameof(Current.ProgramState));
			var m_Listing_Gap = AccessTools.Method(typeof(Listing), nameof(Listing.Gap));
			var idx = list.FirstIndexOf(instr => instr.Calls(m_get_ProgramState));
			if (idx < 0 || idx >= list.Count)
				Log.Error($"Cannot find Current.get_ProgramState in DrawStorytellerSelectionInterface");
			else if (list[idx + 1].Branches(out var label) == false)
				Log.Error($"Cannot find branch in DrawStorytellerSelectionInterface");
			else if (list[idx + 3].opcode != OpCodes.Ldc_R4)
				Log.Error($"Cannot find ldc.r4 in DrawStorytellerSelectionInterface");
			else if (list[idx + 4].Calls(m_Listing_Gap) == false)
				Log.Error($"Cannot find CALL Listing.Gap in DrawStorytellerSelectionInterface");
			else
			{
				list[idx + 4].opcode = OpCodes.Call;
				list[idx + 4].operand = SymbolExtensions.GetMethodInfo(() => AddCheckbox(default, 0));
			}
			return list;
		}
	}

	[HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Expose))]
	static class CameraDriver_Expose_Patch
	{
		public static void Postfix()
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

	[HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.LoadedGame))]
	static class GameComponentUtility_LoadedGame_Patch
	{
		public static void Postfix()
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

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.Paused), MethodType.Getter)]
	class TickManager_Paused_Patch
	{
		public static bool Prefix(ref bool __result)
		{
			if (Main.fullPauseActive)
			{
				__result = true;
				return false;
			}

			if (Main.noPauseEnabled == false)
				return true;

			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.ShouldStop), MethodType.Getter)]
	class WorldRoutePlanner_ShouldStop_Patch
	{
		public static bool Prefix(bool ___active, ref bool __result)
		{
			if (Main.noPauseEnabled == false)
				return true;

			__result = !___active || !WorldRendererUtility.WorldRenderedNow;
			return false;
		}
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Getter)]
	class TickManager_CurTimeSpeed_Getter_Patch
	{
		public static bool Prefix(ref TimeSpeed __result)
		{
			if (Main.fullPauseActive)
			{
				__result = TimeSpeed.Paused;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
	class TickManager_CurTimeSpeed_Setter_Patch
	{
		public static bool Prefix(ref TimeSpeed value)
		{
			if (Main.noPauseEnabled == false)
				return true;
			return value != TimeSpeed.Paused;
		}
	}

	[HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
	class TickManager_TogglePaused_Patch
	{
		public static bool Prefix()
		{
			if (Main.fullPauseActive) return false;
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeed))]
	class TimeSlower_SignalForceNormalSpeed_Patch
	{
		public static bool Prefix()
		{
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeedShort))]
	class TimeSlower_SignalForceNormalSpeedShort_Patch
	{
		public static bool Prefix()
		{
			return (Main.noPauseEnabled == false);
		}
	}

	[HarmonyPatch(typeof(LordToil_ExitMapAndEscortCarriers), nameof(LordToil_ExitMapAndEscortCarriers.UpdateTraderDuty))]
	class LordToil_ExitMapAndEscortCarriers_UpdateTraderDuty_Patch
	{
		public static void Postfix()
		{
			if (Main.noPauseEnabled)
				Main.closeTradeDialog = true;
		}
	}

	[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
	class WindowStack_Add_Patch
	{
		public static void Postfix(Window window)
		{
			if (window.GetType().Name.StartsWith("Dialog_") == false)
				return;

			if (Main.noPauseEnabled && Find.Maps != null)
			{
				var tm = Find.TickManager;
				if (tm != null)
					tm.CurTimeSpeed = TimeSpeed.Normal;
			}
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen))]
	class Dialog_Trade_PostOpen_Patch
	{
		public static void Postfix()
		{
			if (Main.noPauseEnabled)
				Main.closeTradeDialog = false;
		}
	}

	[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents))]
	class Dialog_Trade_DoWindowContents_Patch
	{
		public static bool Prefix(Dialog_Trade __instance)
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

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool _bool;
			var from = SymbolExtensions.GetMethodInfo(() => new TradeDeal().TryExecute(out _bool));
			var to = SymbolExtensions.GetMethodInfo(() => TryExecute(null, out _bool));
			return Transpilers.MethodReplacer(instructions, from, to);
		}
	}

	[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
	class UIRoot_Play_UIRootOnGUI_Patch
	{
		static bool EscapeKeyHandling()
		{
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
			{
				Event.current.Use();
				Main.fullPauseActive = !Main.fullPauseActive;
			}
			return Main.fullPauseActive;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var label = generator.DefineLabel();
			var list = instructions.ToList();
			list[0].labels.Add(label);
			list.InsertRange(0, new[]
			{
				new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => EscapeKeyHandling())),
				new CodeInstruction(OpCodes.Brfalse, label),
				new CodeInstruction(OpCodes.Ret)
			});
			return list.AsEnumerable();
		}
	}

	[HarmonyPatch(typeof(TimeControls), nameof(TimeControls.DoTimeControlsGUI))]
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

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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
			idx = list.FirstIndexOf(instr => instr.LoadsField(f_HighlightTex)) - 5;
			if (idx < 0 || idx >= list.Count)
				Log.Error("Cannot find Ldsfld TexUI.HighlightTex in TimeControls.DoTimeControlsGUI");
			else
			{
				speedCompareOperands = list.GetRange(idx, 3).Select(instr => instr.Clone()).ToList();
				list.Insert(idx + 3, new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => GetTimeSpeedVarValue(TimeSpeed.Normal))));
			}

			var f_SpeedButtonTextures = AccessTools.Field(typeof(TexButton), nameof(TexButton.SpeedButtonTextures));
			idx = list.FirstIndexOf(instr => instr.OperandIs(f_SpeedButtonTextures));
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

		public static bool Prefix()
		{
			if (Main.fullPauseActive)
				return false;

			if (Main.noPauseEnabled == false)
				return true;

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
			return true;
		}
	}
}
