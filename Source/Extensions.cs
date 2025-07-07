using RimWorld;
using Verse;

namespace NoPauseChallenge
{
	public static class Extensions
	{
		public static void SetCurTimeSpeed(this TickManager tm, TimeSpeed value)
		{
			if (Main.isPlacingGravship)
				return;
			if (value == TimeSpeed.Paused || tm.PlayerCanControl)
				tm.curTimeSpeed = value;
		}
	}
}