using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SameRoomLovin;
public class LovinUtility
{
	private static readonly float PregnancyChance = 0.05f;
	private static readonly SimpleCurve LovinIntervalHoursFromAgeCurve =
	[
		new CurvePoint(16f, 1.5f),
		new CurvePoint(22f, 1.5f),
		new CurvePoint(30f, 4f),
		new CurvePoint(50f, 12f),
		new CurvePoint(75f, 36f)
	];
	public static void HandlePregnancy(Pawn pawn, Pawn Partner)
	{
		if (ModsConfig.AnomalyActive && Rand.Chance(SRL_Settings.metalhorrorChance / 100f))
			HandleMetalhorrors(pawn, Partner);

		if (!ModsConfig.BiotechActive)
			return;

		Pawn male = pawn.gender == Gender.Male ? pawn : (Partner.gender == Gender.Male ? Partner : null);
		Pawn female = pawn.gender == Gender.Female ? pawn : (Partner.gender == Gender.Female ? Partner : null);

		if (male == null || female == null)
			return;

		float finalChance = PregnancyChance * PregnancyUtility.PregnancyChanceForPartners(female, male);
		if (!Rand.Chance(finalChance))
			return;

		GeneSet inheritedGenes = PregnancyUtility.GetInheritedGeneSet(male, female, out bool success);

		if (success)
		{
			Hediff_Pregnant hediffPregnant = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, female);
			hediffPregnant.SetParents(null, male, inheritedGenes);
			female.health.AddHediff(hediffPregnant);
		}
		else if (PawnUtility.ShouldSendNotificationAbout(male) || PawnUtility.ShouldSendNotificationAbout(female))
		{
			Messages.Message(
				"MessagePregnancyFailed".Translate(male.Named("FATHER"), female.Named("MOTHER"))
				+ ": " + "CombinedGenesExceedMetabolismLimits".Translate(),
				new LookTargets(male, female),
				MessageTypeDefOf.NegativeEvent
			);
		}
	}
	private static void HandleMetalhorrors(Pawn pawn, Pawn Partner)
	{
		bool isPawnHorror = MetalhorrorUtility.IsInfected(pawn);
		bool isPartnerHorror = MetalhorrorUtility.IsInfected(Partner);

		// Get pregnant! But with something else
		if (isPawnHorror ^ isPartnerHorror)
		{
			if (isPawnHorror)
				MetalhorrorUtility.Infect(Partner, pawn, "StkMetalhorrorLovin");

			else if (isPartnerHorror)
				MetalhorrorUtility.Infect(pawn, Partner, "StkMetalhorrorLovin");
		}
	}

	public static void HandleThoughts(Pawn pawn, Pawn Partner)
	{
		// Vanilla lovin thought, used for social relations only
		Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
		thought_Memory.moodPowerFactor = 0f;
		pawn.needs.mood?.thoughts.memories.TryGainMemory(thought_Memory, Partner);

		// Modded lovin though for mood, identical to vanilla but not personilized (meaning it doesnt spam moods)
		Thought_Memory thought_Mood = (Thought_Memory)ThoughtMaker.MakeThought(SRL_DefOf.Stk_Mass_Lovin);
		if ((pawn.health != null && pawn.health.hediffSet != null && pawn.health.hediffSet.hediffs.Any((Hediff h) => h.def == HediffDefOf.LoveEnhancer))
			|| (Partner.health != null && Partner.health.hediffSet != null && Partner.health.hediffSet.hediffs.Any((Hediff h) => h.def == HediffDefOf.LoveEnhancer)))
		{
			thought_Mood.moodPowerFactor = 1.5f;
		}
		pawn.needs.mood?.thoughts.memories.TryGainMemory(thought_Mood);

		Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.GotLovin, pawn.Named(HistoryEventArgsNames.Doer)));
		HistoryEventDef def = pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, Partner) ? HistoryEventDefOf.GotLovin_Spouse : HistoryEventDefOf.GotLovin_NonSpouse;
		Find.HistoryEventsManager.RecordEvent(new HistoryEvent(def, pawn.Named(HistoryEventArgsNames.Doer)));
	}
	public static void HandleStart(Pawn pawn, Pawn Partner)
	{
		Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InitiatedLovin, pawn.Named(HistoryEventArgsNames.Doer)));

		// Highmates
		if (InteractionWorker_RomanceAttempt.CanCreatePsychicBondBetween(pawn, Partner)
			&& InteractionWorker_RomanceAttempt.TryCreatePsychicBondBetween(pawn, Partner)
			&& (PawnUtility.ShouldSendNotificationAbout(pawn)
			|| PawnUtility.ShouldSendNotificationAbout(Partner)))
		{
			Find.LetterStack.ReceiveLetter("LetterPsychicBondCreatedLovinLabel".Translate(),
				"LetterPsychicBondCreatedLovinText".Translate(pawn.Named("BONDPAWN"), Partner.Named("OTHERPAWN")),
				LetterDefOf.PositiveEvent, new LookTargets(pawn, Partner));
		}
	}

	public static void HandleNeedsTick(Pawn pawn, int delta, int partnersCount = 1)
	{
		// partnersCount manually adds intitator pawn, which excluded from normal calculations, delta is the tick magic for optimizing.
		partnersCount++;
		if (SRL_Settings.srlRecreation)
		{
			if (pawn.needs?.joy != null)
				JoyUtility.JoyTickCheckEnd(pawn, partnersCount * delta, JoyTickFullJoyAction.None);
		}
	}
	public static void HandleNeedsEnd(Pawn pawn, int PartnersCount = 1)
	{
		// partnersCount manually adds intitator pawn, which excluded from normal calculations.
		PartnersCount++;
		if (ModLister.GetActiveModWithIdentifier("vanillaracesexpanded.highmate") != null)
		{
			Need lovinNeed = pawn.needs?.AllNeeds?.FirstOrDefault(n => n.def.defName == "VRE_Lovin");
			if (lovinNeed != null)
				lovinNeed.CurLevel = 1f;	// VRE Highmates have method that does this.
		}

		if (ModLister.GetActiveModWithIdentifier("LovelyDovey.Sex.WithEuterpe") != null)
		{
			Need intimNeed = pawn.needs?.AllNeeds?.FirstOrDefault(n => n.def.defName == "SEX_Intimacy");
			if (intimNeed != null)
				intimNeed.CurLevel = Mathf.Clamp01(intimNeed.CurLevel + PartnersCount * 0.1f);	// Intimacy have +0.2f per lovin
		}
	}
	public static int GenerateRandomMinTicksToNextLovin(Pawn pawn, float modifier = 1f)
	{
		if (DebugSettings.alwaysDoLovin)
			return 100;

		float num = LovinIntervalHoursFromAgeCurve.Evaluate(pawn.ageTracker.AgeBiologicalYearsFloat);
		if (ModsConfig.BiotechActive && pawn.genes != null)
		{
			foreach (Gene item in pawn.genes.GenesListForReading)
			{
				num *= item.def.lovinMTBFactor;
			}
		}
		foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
		{
			HediffComp_GiveLovinMTBFactor hediffComp_GiveLovinMTBFactor = hediff.TryGetComp<HediffComp_GiveLovinMTBFactor>();
			if (hediffComp_GiveLovinMTBFactor != null)
			{
				num *= hediffComp_GiveLovinMTBFactor.Props.lovinMTBFactor;
			}
		}
		num = Rand.Gaussian(num, 0.3f);
		if (num < 0.5f)	num = 0.5f;
		
		return (int)(num * modifier * 2500f);
	}
}

