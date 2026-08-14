using UnityEngine;
using Verse;

namespace NoPauseChallenge
{
	public class Settings : ModSettings
	{
		public const int StandardFourTimesSpeed = 2;
		public const int MaximumFourTimesSpeed = 7;
		public const float UnlimitedTickRate = 150f;

		public static bool slowOnRaid = true;
		public static bool slowOnCaravan = true;
		public static bool slowOnLetter = true;
		public static bool slowOnDamage = false;
		public static bool slowOnEnemyApproach = false;
		public static bool slowOnPrisonBreak = true;
		public static bool noFreeze = false;
		public static int fourTimesSpeed = StandardFourTimesSpeed;

		static void Headline(Listing_Standard modOptions, string title)
		{
			modOptions.Gap(20f);
			_ = modOptions.Label(title);
			modOptions.GapLine();
		}

		public static void DoSettingsWindowContents(Rect rect)
		{
			Listing_Standard modOptions = new Listing_Standard();

			modOptions.Begin(rect);

			Headline(modOptions, "Game speed");
			var sliderRect = modOptions.GetRect(32f);
			fourTimesSpeed = Mathf.RoundToInt(Widgets.HorizontalSlider(
				sliderRect,
				NormalizeFourTimesSpeed(fourTimesSpeed),
				0f,
				MaximumFourTimesSpeed,
				true,
				$"4× button: {FourTimesSpeedLabel}",
				"3×",
				"Unlimited",
				1f));
			TooltipHandler.TipRegion(sliderRect,
				"Controls the fourth speed button added by No Pause Challenge. Standard keeps RimWorld's normal extra-fast speed; Unlimited runs at RimWorld's maximum tick rate.");

			Headline(modOptions, "Events that trigger normal speed");
			modOptions.CheckboxLabeled("Raid", ref slowOnRaid, "Set the game to normal speed when a raid occurs.");
			modOptions.CheckboxLabeled("Caravan", ref slowOnCaravan, "Set the game to normal speed when a Caravan event occurs, such as an ambush.");
			modOptions.CheckboxLabeled("Notification", ref slowOnLetter, "Set the game to normal speed when a certain notifications are received, such as a mad animal.");
			modOptions.CheckboxLabeled("Damage", ref slowOnDamage, "Set the game to normal speed when a pawn takes damage.");
			modOptions.CheckboxLabeled("Enemy Approaching", ref slowOnEnemyApproach, "Set the game to normal speed when an enemy gets near.");
			modOptions.CheckboxLabeled("Prison Break", ref slowOnPrisonBreak, "Set the game to normal speed when a prison break occurs.");

			Headline(modOptions, "Non-challenge mode");
			modOptions.CheckboxLabeled("Pause instead of Freeze", ref noFreeze, "Allows you to access UI and map when using the freeze button.");

			Headline(modOptions, "Notes");
			_ = modOptions.Label("Don't forget to configure the new key bindings:");
			_ = modOptions.Label("- Half Speed");
			_ = modOptions.Label("- Freeze");

			modOptions.End();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref slowOnRaid, "NPC_SlowOnRaid", true);
			Scribe_Values.Look(ref slowOnCaravan, "NPC_SlowOnCaravan", true);
			Scribe_Values.Look(ref slowOnLetter, "NPC_SlowOnLetter", true);
			Scribe_Values.Look(ref slowOnDamage, "NPC_SlowOnDamage", false);
			Scribe_Values.Look(ref slowOnEnemyApproach, "NPC_SlowOnEnemyApproach", false);
			Scribe_Values.Look(ref slowOnPrisonBreak, "NPC_SlowOnPrisonBreak", true);
			Scribe_Values.Look(ref noFreeze, "noFreeze", true);
			Scribe_Values.Look(ref fourTimesSpeed, "fourTimesSpeed", StandardFourTimesSpeed);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
				fourTimesSpeed = NormalizeFourTimesSpeed(fourTimesSpeed);
		}

		public static string FourTimesSpeedLabel
		{
			get
			{
				switch (NormalizeFourTimesSpeed(fourTimesSpeed))
				{
					case 0: return "Same as 3×";
					case 1: return "Slightly slower";
					case 2: return "Standard";
					case 3: return "1.5× standard";
					case 4: return "2× standard";
					case 5: return "4× standard";
					case 6: return "8× standard";
					default: return "Unlimited";
				}
			}
		}

		public static float FourTimesTickRate(float standardTickRate, float threeTimesTickRate)
		{
			switch (NormalizeFourTimesSpeed(fourTimesSpeed))
			{
				case 0: return threeTimesTickRate;
				case 1: return Mathf.Lerp(standardTickRate, threeTimesTickRate, 0.25f);
				case 2: return standardTickRate;
				case 3: return Mathf.Min(UnlimitedTickRate, standardTickRate * 1.5f);
				case 4: return Mathf.Min(UnlimitedTickRate, standardTickRate * 2f);
				case 5: return Mathf.Min(UnlimitedTickRate, standardTickRate * 4f);
				case 6: return Mathf.Min(UnlimitedTickRate, standardTickRate * 8f);
				default: return UnlimitedTickRate;
			}
		}

		static int NormalizeFourTimesSpeed(int value)
		{
			return Mathf.Clamp(value, 0, MaximumFourTimesSpeed);
		}
	}
}
