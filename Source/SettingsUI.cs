using UnityEngine;
using Verse;

namespace NoPauseChallenge
{
	public class SettingsUI : Mod
	{
		public SettingsUI(ModContentPack content) : base(content)
		{
			_ = GetSettings<Settings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			Settings.DoSettingsWindowContents(inRect.LeftPart(0.75f));
		}

		public override string SettingsCategory()
		{
			return "No Pause Challenge";
		}
	}
}
