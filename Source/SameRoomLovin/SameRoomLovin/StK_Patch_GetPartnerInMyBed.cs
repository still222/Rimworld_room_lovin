using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SameRoomLovin;

// Patch to check multiple partners across the room, not just in active bed.
[HarmonyPatch(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.GetPartnerInMyBed))]
public static class Patch_GetPartnerInMyBed
{
	public static bool Prefix(Pawn pawn, ref Pawn __result)
	{
		__result = null;

		if (LovePartnerRelationUtility.HasAnyLovePartner(pawn))
			__result = SRL_LovePartnerRelationUtility.GetMyPartnersInBeds(pawn).FirstOrDefault();

		return false;
	}
}
