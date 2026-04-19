using HarmonyLib;
using Verse;

namespace SameRoomLovin;

[HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Body), nameof(PawnRenderNodeWorker_Apparel_Body.CanDrawNow))]
public static class Patch_PawnRenderNodeWorker_Apparel_Body_CanDrawNow
{
	public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
	{
		if (!__result) return;

		Pawn pawn = parms.pawn;
		if (pawn?.CurJob != null)
		{
			JobDef def = pawn.CurJob.def;
			if (def == SRL_DefOf.SRL_LovinGroupInitiator ||
				def == SRL_DefOf.SRL_LovinGroupPartner ||
				def == SRL_DefOf.SRL_LovinGroupSupport ||
				def == SRL_DefOf.SRL_LovinDuoInitiator ||
				def == SRL_DefOf.SRL_LovinDuoPartner)
			{
				__result = false;
			}
		}
	}
}

[HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Head), nameof(PawnRenderNodeWorker_Apparel_Head.HeadgearVisible))]
public static class Patch_PawnRenderNodeWorker_Apparel_Head_HeadgearVisible
{
	public static void Postfix(PawnDrawParms parms, ref bool __result)
	{
		if (!__result) return;

		Pawn pawn = parms.pawn;
		if (pawn?.CurJob != null)
		{
			JobDef def = pawn.CurJob.def;
			if (def == SRL_DefOf.SRL_LovinGroupInitiator ||
				def == SRL_DefOf.SRL_LovinGroupPartner ||
				def == SRL_DefOf.SRL_LovinGroupSupport ||
				def == SRL_DefOf.SRL_LovinDuoInitiator ||
				def == SRL_DefOf.SRL_LovinDuoPartner)
			{
				__result = false;
			}
		}
	}
}

[HarmonyPatch(typeof(PawnRenderNodeWorker_Body), nameof(PawnRenderNodeWorker_Body.CanDrawNow))]
public static class Patch_PawnRenderNodeWorker_Body_CanDrawNow
{
	public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
	{
		if (__result || parms.bed == null || !parms.pawn.RaceProps.Humanlike)
			return;

		// Now I really hope that pawn in bed with a lovin job doesnt have any breakling mindstate,
		// as it could cause a graphical bug. Low priority though.
		Pawn pawn = parms.pawn;
		if (pawn?.CurJob != null)
		{
			JobDef def = pawn.CurJob.def;
			if (def == SRL_DefOf.SRL_LovinGroupPartner ||
				def == SRL_DefOf.SRL_LovinDuoPartner)
			{
				__result = true;
			}
		}
	}
}

