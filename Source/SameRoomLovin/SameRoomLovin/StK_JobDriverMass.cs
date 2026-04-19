using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SameRoomLovin;

// --- Initiator job ---
public class JobDriver_SRL_Lovin_Group : JobDriver
{
	private int ticksLeft;
	private int ActivePartnersCount;
	private readonly TargetIndex PartnerInd = TargetIndex.A;
	private const int TicksBetweenHeartMotes = 100;
	private const int TicksBetweenCulling = 60;
	private Pawn Partner => (Pawn)job.GetTarget(PartnerInd);
	private int AgeTicks => Find.TickManager.TicksGame - startTick;
	private int phaseOffset;
	private Vector3 drawOffset = Vector3.zero;
	public override Vector3 ForcedBodyOffset
	{
		get
		{
			// Animation
			if (SRL_Settings.srlAnimations && drawOffset != Vector3.zero)
			{
				float num = Mathf.Sin((AgeTicks + phaseOffset) / 60f * 8f);

				Vector3 forward = drawOffset.normalized;
				float forwardOffset = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
				return drawOffset + forward * forwardOffset;
			}
			return drawOffset;
		}
	}
	private List<Pawn> _harem;
	private List<Pawn> Harem => _harem ??= SRL_LovePartnerRelationUtility.GetAllPartnersInRoom(pawn, ignoreReserveCheck: true);
	private List<Pawn> _myPartners;
	private List<Pawn> MyPartners => _myPartners ??= SRL_LovePartnerRelationUtility.GetMyPartners(pawn);
	private readonly Dictionary<Pawn, bool> _activePartnerMap = [];
	private void UpdateActivePartners()
	{
		foreach (var partner in MyPartners)
		{
			_activePartnerMap[partner] = partner.CurJob != null &&
				(partner.CurJob.def == SRL_DefOf.SRL_LovinGroupPartner ||
				partner.CurJob.def == SRL_DefOf.SRL_LovinGroupSupport);
		}

		ActivePartnersCount = _activePartnerMap.Values.Count(v => v);
	}
	private void PopulateLists()
	{
		_ = MyPartners;
		_ = Harem;
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		PopulateLists();

		if (!pawn.Reserve(pawn, job)) return false;

		foreach (Pawn partner in Harem)
		{
			if (!pawn.Reserve(partner, job)) return false;
		}

		return true;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		Building_Bed bed = Partner.CurrentBed();
		this.FailOnDespawnedOrNull(PartnerInd);
		this.FailOn(() => !Partner.health.capacities.CanBeAwake);
		this.FailOn(() => Harem.Any(p => p.DestroyedOrNull() || !p.Spawned));
		this.FailOn(() => bed == null || bed.DestroyedOrNull() || !bed.Spawned);
		
		// Duration for lovin
		ticksLeft = (int)(2400f * Rand.Range(0.25f, 1.25f));

		// --- 1. Go to partner's bed and call all harem members---
		Toil goToPartnerBed = ToilMaker.MakeToil("GoToPartnerBed");
		goToPartnerBed.initAction = () =>
		{
			IntVec3 targetPos = Partner.Position + bed.Rotation.FacingCell;
			pawn.pather.StartPath(targetPos, PathEndMode.OnCell);

			// Assign "support" jobs to all other harem members
			foreach (Pawn member in Harem)
			{
				if (member == Partner || member.Downed)
				{
					continue; // skip main partner and any downed pawn
				}

				if (member.CurJob == null || member.CurJob.def != SRL_DefOf.SRL_LovinGroupSupport)
				{
					Job supportJob = JobMaker.MakeJob(SRL_DefOf.SRL_LovinGroupSupport, Partner, bed);
					supportJob.count = ticksLeft;
					member.jobs.StartJob(supportJob, JobCondition.InterruptForced);
				}
			}

			if (Partner.CurJob == null || Partner.CurJob.def != SRL_DefOf.SRL_LovinGroupPartner)
			{
				Job mirrorJob = JobMaker.MakeJob(SRL_DefOf.SRL_LovinGroupPartner, pawn, bed);
				mirrorJob.count = ticksLeft; // pass duration
				Partner.jobs.StartJob(mirrorJob, JobCondition.InterruptForced);
			}
			else
			{
				Log.Warning($"[StKRoomLovin] Vanilla 9999 ticks triggered for {pawn.LabelShort}");
				ticksLeft = 9999999;	// straignt from vanilla, no idea what this is
			}
		};
		goToPartnerBed.defaultCompleteMode = ToilCompleteMode.PatherArrival;
		yield return goToPartnerBed;

		// --- 2. Assign mirrored job (instant) ---
		Toil assignPartnerJob = ToilMaker.MakeToil("AssignPartnerJob");
		assignPartnerJob.initAction = () =>
		{
			Partner.jobs.curDriver.ReadyForNextToil();

			// Animation
			Vector3 facingVector = bed.Rotation.FacingCell.ToVector3();
			phaseOffset = Rand.Range(0, 60);
			drawOffset = -facingVector * 0.5f;


			// Remove inactive partners from MyPartners
			MyPartners.RemoveAll(p => p.CurJob == null ||
									(p.CurJob.def != SRL_DefOf.SRL_LovinGroupPartner &&
									p.CurJob.def != SRL_DefOf.SRL_LovinGroupSupport));
			ActivePartnersCount = MyPartners.Count;

			// Assign history events to every harem member
			foreach (Pawn member in MyPartners)
			{
				if (pawn.thingIDNumber < member.thingIDNumber)
					LovinUtility.HandleStart(pawn, member);
			}
		};
		assignPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
		yield return assignPartnerJob;

		// --- 3. Do lovin without bed ---
		Toil lovinToil = ToilMaker.MakeToil("LovinNoBed");

		// Pre-tick: decrement ticks and show hearts
		lovinToil.AddPreTickIntervalAction(delta =>
		{
			ticksLeft -= delta;

			if (pawn.IsHashIntervalTick(TicksBetweenCulling, delta))
			{
				if (ActivePartnersCount != 0 && ticksLeft >= 300)
					UpdateActivePartners();
			}

			// Show hearts
			if (pawn.IsHashIntervalTick(TicksBetweenHeartMotes, delta))
				FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);

			LovinUtility.HandleNeedsTick(pawn, delta, ActivePartnersCount);

			// If every partner left, end the job
			if (ticksLeft <= 0 || ActivePartnersCount == 0)
			{
				if (ActivePartnersCount != 0)
					LovinUtility.HandleNeedsEnd(pawn, ActivePartnersCount);

				ReadyForNextToil();
				return;
			}
		});

		lovinToil.AddFinishAction(() =>
		{
			// Log current active partners
			var activePartners = MyPartners.Where(p => _activePartnerMap.TryGetValue(p, out bool isActive) && isActive).ToList();
			//Log.Message($"[StKRoomLovin] Active partners for {pawn.LabelShort} in the end: " +
			//			string.Join(", ", activePartners.Select(p => p.LabelShort)));

			// Loop through all active partners
			foreach (Pawn partner in activePartners)
			{
				LovinUtility.HandleThoughts(pawn, partner);
				if (pawn.thingIDNumber < partner.thingIDNumber)
					LovinUtility.HandlePregnancy(pawn, partner);
			}

			pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtility.GenerateRandomMinTicksToNextLovin(pawn, 1.25f);
		});

		lovinToil.socialMode = RandomSocialMode.Off;
		lovinToil.defaultCompleteMode = ToilCompleteMode.Never;

		yield return lovinToil;
	}
}

// --- Partner job ---
public class JobDriver_SRL_Lovin_Group_Partner : JobDriver
{
	private int ticksLeft;
	private int ActivePartnersCount;
	private readonly TargetIndex InitiatorInd = TargetIndex.A;
	private readonly TargetIndex BedInd = TargetIndex.B;
	private const int TicksBetweenCulling = 60;
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
	private List<Pawn> _myPartners;
	private List<Pawn> MyPartners => _myPartners ??= SRL_LovePartnerRelationUtility.GetMyPartners(pawn);
	private readonly Dictionary<Pawn, bool> _activePartnerMap = [];
	private void UpdateActivePartners()
	{
		foreach (var partner in MyPartners)
		{
			_activePartnerMap[partner] = partner.CurJob != null &&
				(partner.CurJob.def == SRL_DefOf.SRL_LovinGroupInitiator ||
				partner.CurJob.def == SRL_DefOf.SRL_LovinGroupSupport);
		}

		ActivePartnersCount = _activePartnerMap.Values.Count(v => v);
	}
	private void PopulateLists()
	{
		_ = MyPartners;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		PopulateLists();
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

		Toil waitForInitiator = Toils_LayDown.LayDown(BedInd, true, false, false);
		waitForInitiator.initAction = () =>
		{
			if (pawn.gender == Gender.Male)
				pawn.jobs.posture = PawnPosture.LayingInBedFaceUp;
			else
				pawn.jobs.posture = PawnPosture.LayingInBed;
		};
		waitForInitiator.AddEndCondition(() =>
		{
			if (Initiator.CurJobDef != SRL_DefOf.SRL_LovinGroupInitiator)
				return JobCondition.Incompletable;

			return JobCondition.Ongoing;
		});
		waitForInitiator.defaultCompleteMode = ToilCompleteMode.Never;
		yield return waitForInitiator;

		Toil mirroredToil = Toils_LayDown.LayDown(BedInd, true, false, false);
		mirroredToil.initAction = () =>
		{
			//Log.Message($"[StKRoomLovin] Partner:{pawn.LabelShort} is advanced toils");
			ticksLeft = job.count;

			// Remove inactive partners from MyPartners
			MyPartners.RemoveAll(p => p.CurJob == null ||
									(p.CurJob.def != SRL_DefOf.SRL_LovinGroupInitiator &&
									p.CurJob.def != SRL_DefOf.SRL_LovinGroupSupport));
			ActivePartnersCount = MyPartners.Count;

			// Assign history events to every harem member
			foreach (Pawn member in MyPartners)
			{
				if (pawn.thingIDNumber < member.thingIDNumber)
					LovinUtility.HandleStart(pawn, member);
			}

			if (pawn.gender == Gender.Male)
				pawn.jobs.posture = PawnPosture.LayingInBedFaceUp;
			else
				pawn.jobs.posture = PawnPosture.LayingInBed;
		};
		
		mirroredToil.AddPreTickIntervalAction(delta =>
		{
			ticksLeft -= delta;

			if (pawn.IsHashIntervalTick(TicksBetweenCulling, delta) && ticksLeft >= 300)
				UpdateActivePartners();

			LovinUtility.HandleNeedsTick(pawn, delta, ActivePartnersCount);

			if (ticksLeft <= 0 || ActivePartnersCount == 0)
			{
				if (ActivePartnersCount != 0)
					LovinUtility.HandleNeedsEnd(pawn, ActivePartnersCount);

				ReadyForNextToil();
				return;
			}
		});
		mirroredToil.AddFinishAction(() =>
		{
			// Log current active partners
			var activePartners = MyPartners.Where(p => _activePartnerMap.TryGetValue(p, out bool isActive) && isActive).ToList();
			//Log.Message($"[StKRoomLovin] Active partners for {pawn.LabelShort} in the end: " +
			//			string.Join(", ", activePartners.Select(p => p.LabelShort)));

			foreach (Pawn partner in activePartners)
			{
				LovinUtility.HandleThoughts(pawn, partner);
				if (pawn.thingIDNumber < partner.thingIDNumber)
					LovinUtility.HandlePregnancy(pawn, partner);
			}

			pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtility.GenerateRandomMinTicksToNextLovin(pawn);
		});
		mirroredToil.defaultCompleteMode = ToilCompleteMode.Never;
		mirroredToil.socialMode = RandomSocialMode.Off;

		yield return mirroredToil;
	}
}

// --- Support job ---
public class JobDriver_SRL_Lovin_Group_Support : JobDriver
{
	private int ticksLeft;
	private int ActivePartnersCount;
	private const int TicksBetweenCulling = 60;
	private readonly TargetIndex PartnerInd = TargetIndex.A;
	private readonly TargetIndex BedInd = TargetIndex.B;
	private Pawn Partner => (Pawn)job.GetTarget(PartnerInd);
	private Building_Bed Bed => (Building_Bed)(Thing)job.GetTarget(BedInd);
	private int AgeTicks => Find.TickManager.TicksGame - startTick;
	private int phaseOffset;
	private Vector3 drawOffset = Vector3.zero;
	public override Vector3 ForcedBodyOffset
	{
		get
		{
			// Animation
			if (SRL_Settings.srlAnimations && drawOffset != Vector3.zero)
			{
				float num = Mathf.Sin((AgeTicks + phaseOffset) / 60f * 8f);

				Vector3 forward = drawOffset.normalized;
				float forwardOffset = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
				return drawOffset + forward * forwardOffset;
			}
			return drawOffset;
		}
	}
	private List<Pawn> _myPartners;
	private List<Pawn> MyPartners => _myPartners ??= SRL_LovePartnerRelationUtility.GetMyPartners(pawn);
	private readonly Dictionary<Pawn, bool> _activePartnerMap = [];
	private void UpdateActivePartners()
	{
		foreach (var partner in MyPartners)
		{
			_activePartnerMap[partner] = partner.CurJob != null &&
				(partner.CurJob.def == SRL_DefOf.SRL_LovinGroupInitiator ||
				partner.CurJob.def == SRL_DefOf.SRL_LovinGroupPartner ||
				partner.CurJob.def == SRL_DefOf.SRL_LovinGroupSupport);
		}

		ActivePartnersCount = _activePartnerMap.Values.Count(v => v);
	}
	private void PopulateLists()
	{
		_ = MyPartners;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		PopulateLists();
		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(BedInd);
		this.FailOnDespawnedOrNull(PartnerInd);
		this.FailOn(() => !Partner.health.capacities.CanBeAwake);
		

		// --- 1. Go to partner's bed ---
		Toil goToPartnerBed = ToilMaker.MakeToil("GoToPartnerBed");
		goToPartnerBed.initAction = () =>
		{
			IntVec3 targetPos = Partner.Position + Bed.Rotation.FacingCell;
			pawn.pather.StartPath(targetPos, PathEndMode.OnCell);
		};
		goToPartnerBed.defaultCompleteMode = ToilCompleteMode.PatherArrival;
		yield return goToPartnerBed;

		// --- 2. Initializing stuff ---
		Toil mirroredToil = ToilMaker.MakeToil("LovinSupport");
		mirroredToil.initAction = () =>
		{
			ticksLeft = job.count;

			// Remove inactive partners from MyPartners
			MyPartners.RemoveAll(p => p.CurJob == null ||
									(p.CurJob.def != SRL_DefOf.SRL_LovinGroupInitiator &&
									p.CurJob.def != SRL_DefOf.SRL_LovinGroupPartner &&
									p.CurJob.def != SRL_DefOf.SRL_LovinGroupSupport));
			ActivePartnersCount = MyPartners.Count;

			// Assign history events to every harem member
			foreach (Pawn member in MyPartners)
			{
				if (pawn.thingIDNumber < member.thingIDNumber)
					LovinUtility.HandleStart(pawn, member);
			}
			
			// Animation
			Vector3 facingVector = Bed.Rotation.FacingCell.ToVector3();
			phaseOffset = Rand.Range(0, 60);
			drawOffset = -facingVector * 0.25f;
		};
		mirroredToil.AddPreTickIntervalAction(delta =>
		{
			ticksLeft -= delta;

			if (pawn.IsHashIntervalTick(TicksBetweenCulling, delta) && ticksLeft >= 300)
				UpdateActivePartners();

			LovinUtility.HandleNeedsTick(pawn, delta, ActivePartnersCount);

			if (ticksLeft <= 0 || ActivePartnersCount == 0)
			{
				if (ActivePartnersCount != 0)
					LovinUtility.HandleNeedsEnd(pawn, ActivePartnersCount);

				ReadyForNextToil();
				return;
			}
		});
		mirroredToil.AddFinishAction(() =>
		{
			var activePartners = MyPartners.Where(p => _activePartnerMap.TryGetValue(p, out bool isActive) && isActive).ToList();

			foreach (Pawn partner in activePartners)
			{
				LovinUtility.HandleThoughts(pawn, partner);
				if (pawn.thingIDNumber < partner.thingIDNumber)
					LovinUtility.HandlePregnancy(pawn, partner);
			}

			pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtility.GenerateRandomMinTicksToNextLovin(pawn);
		});
		mirroredToil.defaultCompleteMode = ToilCompleteMode.Never;
		mirroredToil.socialMode = RandomSocialMode.Off;

		yield return mirroredToil;
	}
}
