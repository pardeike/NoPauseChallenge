using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace NoPauseChallenge
{
	public sealed class NoPauseChallengeBridgeTools
	{
		static readonly TimeSpeed[] displayedSpeeds =
		{
			TimeSpeed.Normal,
			TimeSpeed.Fast,
			TimeSpeed.Superfast,
			TimeSpeed.Ultrafast
		};

		[Tool(
			"nopausechallenge/get_speed_indicator_state",
			Description = "Read No Pause Challenge state and classify the exact textures selected for the four visible speed buttons.")]
		public static object GetSpeedIndicatorState()
		{
			return DescribeState();
		}

		[Tool(
			"nopausechallenge/prepare_mapless_caravan",
			Description = "Enable No Pause Challenge, form a real caravan from every free colonist on the current map, and abandon the final home map.",
			ResultDescription = "Returns the resulting map, caravan, speed, and texture-selection state.")]
		public static object PrepareMaplessCaravan(
			[ToolParameter(
				Description = "Whether the Half Speed feature should be enabled for the scenario.",
				DefaultValue = false)]
			bool halfSpeedEnabled = false)
		{
			if (Current.ProgramState != ProgramState.Playing)
			{
				return new
				{
					success = false,
					message = "A playable game must be loaded before preparing the scenario.",
					programState = Current.ProgramState.ToString()
				};
			}

			var map = Find.CurrentMap;
			if (map == null)
			{
				return new
				{
					success = false,
					message = "The current game is already mapless; start a fresh debug game before preparing the scenario.",
					state = DescribeState()
				};
			}

			var mapParent = map.Parent;
			if (mapParent == null || mapParent.Faction != Faction.OfPlayer)
			{
				return new
				{
					success = false,
					message = "The current map is not a player settlement.",
					mapParent = mapParent?.GetType().FullName,
					faction = mapParent?.Faction?.Name
				};
			}

			var pawns = map.mapPawns.FreeColonistsSpawned.ToList();
			if (pawns.Count == 0)
			{
				return new
				{
					success = false,
					message = "The current map has no free spawned colonists to form a caravan."
				};
			}

			var directionTile = CaravanExitMapUtility.RandomBestExitTileFrom(map);
			if (!directionTile.Valid)
			{
				return new
				{
					success = false,
					message = "RimWorld could not find a valid neighboring tile for the caravan."
				};
			}

			Main.noPauseEnabled = true;
			Main.halfSpeedEnabled = halfSpeedEnabled;
			Main.halfSpeedActive = false;
			Main.fullPauseActive = false;
			Main._cutSceneMap = null;

			var caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(
				pawns,
				Faction.OfPlayer,
				map.Tile,
				directionTile,
				PlanetTile.Invalid,
				sendMessage: false);
			if (caravan == null)
			{
				return new
				{
					success = false,
					message = "RimWorld failed to create the caravan."
				};
			}

			mapParent.Abandon(wasGravshipLaunch: false);
			Find.GameEnder.CheckOrUpdateGameOver();
			Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;

			return new
			{
				success = Find.CurrentMap == null && Find.Maps.Count == 0,
				message = Find.CurrentMap == null && Find.Maps.Count == 0
					? "Prepared a caravan-only game with no current map."
					: "The caravan formed, but RimWorld retained a map.",
				caravan = new
				{
					id = caravan.ID,
					name = caravan.Name,
					pawnCount = caravan.PawnsListForReading.Count,
					tile = caravan.Tile.ToString()
				},
				state = DescribeState()
			};
		}

		[Tool(
			"nopausechallenge/set_speed",
			Description = "Set one visible No Pause Challenge speed and optionally activate Half Speed, then return the exact indicator state.")]
		public static object SetSpeed(
			[ToolParameter(
				Description = "RimWorld TimeSpeed value: 1 Normal, 2 Fast, 3 Superfast, or 4 Ultrafast.",
				DefaultValue = 1)]
			int speed = 1,
			[ToolParameter(
				Description = "Whether the Half Speed visual and tick-rate modifier should be active.",
				DefaultValue = false)]
			bool halfSpeedActive = false)
		{
			if (speed < (int)TimeSpeed.Normal || speed > (int)TimeSpeed.Ultrafast)
			{
				return new
				{
					success = false,
					message = "Speed must be between 1 (Normal) and 4 (Ultrafast).",
					speed
				};
			}

			Main.halfSpeedActive = halfSpeedActive;
			Find.TickManager.CurTimeSpeed = (TimeSpeed)speed;
			return DescribeState();
		}

		static object DescribeState()
		{
			var tickManager = Find.TickManager;
			var currentSpeed = tickManager?.CurTimeSpeed ?? TimeSpeed.Paused;
			var indicators = DescribeIndicators(currentSpeed);
			var playerCaravans = Find.WorldObjects?.Caravans?
				.Count(caravan => caravan.Faction == Faction.OfPlayer) ?? 0;

			return new
			{
				success = true,
				modVersion = typeof(Main).Assembly.GetName().Version?.ToString(),
				programState = Current.ProgramState.ToString(),
				hasCurrentMap = Find.CurrentMap != null,
				mapCount = Find.Maps?.Count ?? 0,
				playerCaravanCount = playerCaravans,
				noPauseEnabled = Main.noPauseEnabled,
				halfSpeedEnabled = Main.halfSpeedEnabled,
				halfSpeedActive = Main.halfSpeedActive,
				fullPauseActive = Main.fullPauseActive,
				hasCutSceneMap = Main.CutSceneMap != null,
				currentSpeed = currentSpeed.ToString(),
				tickRateMultiplier = tickManager?.TickRateMultiplier ?? 0f,
				renderingCorrect = indicators.All(indicator => indicator.matchesExpected),
				indicators
			};
		}

		static IndicatorState[] DescribeIndicators(TimeSpeed currentSpeed)
		{
			var patchType = typeof(Main).Assembly.GetType(
				"NoPauseChallenge.TimeControls_DoTimeControlsGUI_Patch",
				throwOnError: true);
			var getButtonTexture = patchType.GetMethod(
				"GetButtonTexture",
				BindingFlags.Public | BindingFlags.Static);
			if (getButtonTexture == null)
				throw new MissingMethodException(patchType.FullName, "GetButtonTexture");

			return displayedSpeeds
				.Select(speed => DescribeIndicator(getButtonTexture, speed, currentSpeed))
				.ToArray();
		}

		static IndicatorState DescribeIndicator(
			MethodInfo getButtonTexture,
			TimeSpeed speed,
			TimeSpeed currentSpeed)
		{
			var texture = (Texture2D)getButtonTexture.Invoke(
				null,
				new object[] { speed, currentSpeed, speed });
			var index = (int)speed;
			var selected = speed == currentSpeed;
			var useOriginalTextures =
				(Main.CutSceneMap != null && Main.CutSceneMap == Find.CurrentMap)
				|| (Main.noPauseEnabled == false && Main.halfSpeedEnabled == false);
			var expected = useOriginalTextures
				? Main.originalSpeedButtonTextures[index]
				: selected
					? Main.halfSpeedActive
						? Main.SpeedButtonTexturesHalf[index]
						: Main.SpeedButtonTexturesActive[index]
					: Main.SpeedButtonTextures[index];

			return new IndicatorState
			{
				speed = speed.ToString(),
				selected = selected,
				textureName = texture?.name,
				matchesExpected = ReferenceEquals(texture, expected),
				matchesOriginal = ReferenceEquals(texture, Main.originalSpeedButtonTextures[index]),
				matchesBase = ReferenceEquals(texture, Main.SpeedButtonTextures[index]),
				matchesActive = ReferenceEquals(texture, Main.SpeedButtonTexturesActive[index]),
				matchesHalf = ReferenceEquals(texture, Main.SpeedButtonTexturesHalf[index])
			};
		}

		public sealed class IndicatorState
		{
			public string speed;
			public bool selected;
			public string textureName;
			public bool matchesExpected;
			public bool matchesOriginal;
			public bool matchesBase;
			public bool matchesActive;
			public bool matchesHalf;
		}
	}
}
