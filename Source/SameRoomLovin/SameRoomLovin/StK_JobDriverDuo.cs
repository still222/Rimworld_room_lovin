using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SameRoomLovin;

// --- Initiator job ---
public class JobDriver_SRL_Lovin : JobDriver
{
	private int ticksLeft;
	private readonly TargetIndex PartnerInd = TargetIndex.A;
	private const int TicksBetweenHeartMotes = 100;
	private Pawn Partner => (Pawn)job.GetTarget(PartnerInd);
	private int AgeTicks => Find.TickManager.TicksGame - startTick;
	private Vector3 drawOffset = Vector3.zero;
	public override Vector3 ForcedBodyOffset
	{
		get
		{
			// Animation
			if (SRL_Settings.srlAnimations && drawOffset != Vector3.zero)
			{
				float num = Mathf.Sin(AgeTicks / 60f * 8f);

				Vector3 forward = drawOffset.normalized;
				float forwardOffset = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
				return drawOffset + forward * forwardOffset;
			}
			return drawOffset;
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return pawn.Reserve(pawn, job) && pawn.Reserve(Partner, job);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(PartnerInd);
		this.FailOn(() => !Partner.health.capacities.CanBeAwake);

		// --- 1. Go to partner's bed ---
		Toil goToPartnerBed = ToilMaker.MakeToil("GoToPartnerBed");
		Building_Bed Bed = Partner.CurrentBed();
		goToPartnerBed.initAction = () =>
		{
			IntVec3 targetPos = Partner.Position + Bed.Rotation.FacingCell;
			pawn.pather.StartPath(targetPos, PathEndMode.OnCell);
		};
		goToPartnerBed.defaultCompleteMode = ToilCompleteMode.PatherArrival;
		yield return goToPartnerBed;

		// --- 2. Assign mirrored job (instant) ---
		Toil assignPartnerJob = ToilMaker.MakeToil("AssignPartnerJob");
		assignPartnerJob.initAction = () =>
		{
			// Duration for lovin
			ticksLeft = (int)(2500f * Mathf.Clamp(Rand.Range(0.1f, 1.1f), 0.1f, 2f));

			// Assign mirrored job to partner if not already doing it
			if (Partner.CurJob == null || Partner.CurJob.def != SRL_DefOf.SRL_LovinDuoPartner)
			{
				Job mirrorJob = JobMaker.MakeJob(SRL_DefOf.SRL_LovinDuoPartner, pawn, Bed);
				mirrorJob.count = ticksLeft; // pass duration
				Partner.jobs.StartJob(mirrorJob, JobCondition.InterruptForced);

				// Animation
				Vector3 facingVector = Bed.Rotation.FacingCell.ToVector3();
				drawOffset = -facingVector * 0.5f;

				LovinUtility.HandleStart(pawn, Partner);
			}
			else
			{
				Log.Warning($"[StKRoomLovin] Vanilla 9999 ticks triggered for {pawn.LabelShort}");
				ticksLeft = 9999999;	// straignt from vanilla, no idea what this is
			}
		};
		assignPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
		yield return assignPartnerJob;

		// --- 3. Do lovin ---
		Toil lovinToil = ToilMaker.MakeToil("LovinNoBed");
		lovinToil.AddPreTickIntervalAction(delta =>
		{
			ticksLeft -= delta;
			LovinUtility.HandleNeedsTick(pawn, delta);

			// Show hearts
			if (pawn.IsHashIntervalTick(TicksBetweenHeartMotes, delta))
				FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);

			// Stop if partner stops participating
			if (Partner.CurJob == null || Partner.CurJob.def != SRL_DefOf.SRL_LovinDuoPartner || ticksLeft <= 0)
			{
				if (ticksLeft <= 0)
					LovinUtility.HandleNeedsEnd(pawn);

				ReadyForNextToil();
				return;
			}
		});

		lovinToil.AddFinishAction(() =>
		{
			LovinUtility.HandleThoughts(pawn, Partner);
			LovinUtility.HandlePregnancy(pawn, Partner);
			pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtility.GenerateRandomMinTicksToNextLovin(pawn, 1.25f);
		});

		lovinToil.socialMode = RandomSocialMode.Off;
		lovinToil.defaultCompleteMode = ToilCompleteMode.Never;

		yield return lovinToil;
	}
}

// --- Partner job ---
public class JobDriver_SRL_Lovin_Partner : JobDriver
{
	private int ticksLeft;
	private readonly TargetIndex InitiatorInd = TargetIndex.A;
	private readonly TargetIndex BedInd = TargetIndex.B;
	private Pawn Initiator => (Pawn)job.GetTarget(InitiatorInd);
	//private Building_Bed Bed => (Building_Bed)(Thing)job.GetTarget(BedInd);
	//private int AgeTicks => Find.TickManager.TicksGame - this.startTick;
	private Vector3 drawOffset = Vector3.zero;
	public override Vector3 ForcedBodyOffset
	{
		get
		{
			// Animation
			//if (SRL_Settings.srlAnimations && drawOffset != Vector3.zero)
			//{
			//	float num = Mathf.Sin((float)this.AgeTicks / 60f * 8f);

			//	Vector3 forward = drawOffset.normalized;
			//	float forwardOffset = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
			//	return drawOffset + forward * forwardOffset;
			//}
			return drawOffset;
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		// Partner does not need to reserve initiator, just stay in place
		return true;
	}

	public override bool CanBeginNowWhileLyingDown()
	{
		return JobInBedUtility.InBedOrRestSpotNow(pawn, job.GetTarget(BedInd));
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(BedInd);
		this.FailOnDespawnedOrNull(InitiatorInd);
		this.FailOn(() => !Initiator.health.capacities.CanBeAwake);

		Toil mirroredToil = Toils_LayDown.LayDown(BedInd, true, false, false);
		mirroredToil.initAction = () =>
		{
			ticksLeft = job.count;

			if (pawn.gender == Gender.Male)
				pawn.jobs.posture = PawnPosture.LayingInBedFaceUp;
			else
				pawn.jobs.posture = PawnPosture.LayingInBed;
		};
		mirroredToil.AddPreTickIntervalAction(delta =>
		{
			ticksLeft -= delta;
			LovinUtility.HandleNeedsTick(pawn, delta);

			if (ticksLeft <= 0 || Initiator.CurJob == null || Initiator.CurJob.def != SRL_DefOf.SRL_LovinDuoInitiator)
			{
				if (ticksLeft <= 0)
					LovinUtility.HandleNeedsEnd(pawn);

				ReadyForNextToil();
				return;
			}
		});
		mirroredToil.AddFinishAction(() =>
		{
			LovinUtility.HandleThoughts(pawn, Initiator);
			pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtility.GenerateRandomMinTicksToNextLovin(pawn);
		});
		mirroredToil.defaultCompleteMode = ToilCompleteMode.Never;
		mirroredToil.socialMode = RandomSocialMode.Off;

		yield return mirroredToil;
	}
}
