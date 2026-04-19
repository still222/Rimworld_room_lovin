using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SameRoomLovin;

[HarmonyPatch(typeof(JobGiver_DoLovin), "TryGiveJob")]
public static class Patch_JobGiver_DoLovin_TryGiveJob
{
	private static List<Pawn> MassLovinEligibleList(Pawn pawn)
	{
		int groupChance = SRL_Settings.groupChance;
		if (!Rand.Chance(groupChance / 100f))
			return null;

		// TODO: Ideology/traits?

		List<Pawn> harem = [.. SRL_LovePartnerRelationUtility.GetAllPartnersInRoom(pawn)];

		return harem.Count >= 2 ? harem : null;
	}

	public static bool Prefix(Pawn pawn, ref Job __result)
	{
		__result = null;
		if (pawn.Downed)
			return false;

		Building_Bed bed = pawn.CurrentBed();
		if (Find.TickManager.TicksGame < pawn.mindState.canLovinTick
			|| bed == null || bed.Medical || !pawn.health.capacities.CanBeAwake)
			return false;

		var partners = SRL_LovePartnerRelationUtility.GetMyPartnersInBeds(pawn);
		Pawn validPartner = partners.FirstOrDefault(partner => pawn.CanReserve(partner) && partner.CanReserve(pawn));
		if (validPartner == null)
			return false;

		var massEligibleList = MassLovinEligibleList(pawn);
		string mode = massEligibleList != null ? "mass" : "duo";
		Log.Message($"[StKRoomLovin] {pawn.LabelShort} started {mode} lovin with {validPartner}.");

		__result = JobMaker.MakeJob(
			massEligibleList != null
				? SRL_DefOf.SRL_LovinGroupInitiator
				: SRL_DefOf.SRL_LovinDuoInitiator,
			validPartner);

		return false;
	}
}
