using UnityEngine;
using Verse;

namespace SameRoomLovin;
public class SRL_Settings : ModSettings
{
	public static int groupChance = 23;
	public static int metalhorrorChance = 50;
	public static bool srlRecreation = true;
	public static bool srlAnimations = false;
	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref groupChance, "SRL_group_chance", 23);
		Scribe_Values.Look(ref metalhorrorChance, "SRL_metalhorror_chance", 50);
		Scribe_Values.Look(ref srlRecreation, "SRL_Recreation", true);
		Scribe_Values.Look(ref srlAnimations, "SRL_Animations", false);
	}
}

public class SRL_Mod : Mod
{
	public SRL_Mod(ModContentPack content) : base(content)
	{
		GetSettings<SRL_Settings>();
	}
	public override string SettingsCategory() => "StK SameRoomLovin";

	public override void DoSettingsWindowContents(Rect canvas)
	{
		Listing_Standard listing = new();
		listing.Begin(canvas);

		listing.Gap();

		listing.Label("StkSrlGroupChance".Translate(SRL_Settings.groupChance));
			SRL_Settings.groupChance = (int)listing.Slider(SRL_Settings.groupChance, 1f, 100f);

		listing.Gap();

		listing.Label("StkSrlMetalhorrorChance".Translate(SRL_Settings.metalhorrorChance));
			SRL_Settings.metalhorrorChance = (int)listing.Slider(SRL_Settings.metalhorrorChance, 0f, 100f);

		listing.Gap();

		listing.CheckboxLabeled("StkSrlRecreationLabel".Translate(),
			ref SRL_Settings.srlRecreation,
			"StkSrlRecreationDesc".Translate());

		listing.Gap(6);

		listing.CheckboxLabeled("StkSrlAnimationsLabel".Translate(),
			ref SRL_Settings.srlAnimations,
			"StkSrlAnimationsDesc".Translate());

		listing.End();
	}
}
