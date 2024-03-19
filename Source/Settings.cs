using RimWorld;
using UnityEngine;
using Verse;

namespace NoPauseChallenge
{
	public class Settings : ModSettings
	{
		public static bool slowOnRaid = true;
		public static bool slowOnCaravan = true;
		public static bool slowOnLetter = true;
		public static bool slowOnDamage = false;
		public static bool slowOnEnemyApproach = false;
		public static bool slowOnPrisonBreak = true;

		public static void DoSettingsWindowContents(Rect rect)
		{
			Listing_Standard modOptions = new Listing_Standard();

			modOptions.Begin(rect);
			modOptions.Gap(20f);

			_ = modOptions.Label("Events that trigger normal speed");

			modOptions.CheckboxLabeled("Raid", ref slowOnRaid, "Set the game to normal speed when a raid occurs.");
			modOptions.CheckboxLabeled("Caravan", ref slowOnCaravan, "Set the game to normal speed when a Caravan event occurs, such as an ambush.");
			modOptions.CheckboxLabeled("Notification", ref slowOnLetter, "Set the game to normal speed when a certain notifications are received, such as a mad animal.");
			modOptions.CheckboxLabeled("Damage", ref slowOnDamage, "Set the game to normal speed when a pawn takes damage.");
			modOptions.CheckboxLabeled("Enemy Approaching", ref slowOnEnemyApproach, "Set the game to normal speed when an enemy gets near.");
			modOptions.CheckboxLabeled("Prison Break", ref slowOnPrisonBreak, "Set the game to normal speed when a prison break occurs.");

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
		}
	}
}
