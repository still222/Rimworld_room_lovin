using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SameRoomLovin;
public static class SRL_LovePartnerRelationUtility
{
	public static bool IsEligibleForLovin(Pawn pawn)
	{
		if (pawn == null || pawn.health == null || pawn.mindState == null)
			return false;

		return pawn.health.capacities.CanBeAwake &&
			Find.TickManager.TicksGame >= pawn.mindState.canLovinTick;
	}

	public static List<Pawn> GetMyPartnersInBeds(Pawn pawn)
	{
		List<Pawn> partners = [];
		Room room = pawn.CurrentBed()?.GetRoom();
		if (room == null || room.PsychologicallyOutdoors)
			return partners;

		foreach (Building_Bed bedInRoom in room.ContainedBeds)
		{
			foreach (Pawn other in bedInRoom.CurOccupants)
			{
				if (other != pawn && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, other) && IsEligibleForLovin(other))
					partners.Add(other);
			}
		}

		// Ensure random but deterministic order for MP
		partners = [.. partners.OrderBy(_ => Rand.Value)];

		return partners;
	}

	public static List<Pawn> GetMyPartners(Pawn pawn)
	{
		List<Pawn> partners = [];
		Room room = pawn.CurrentBed()?.GetRoom();
		if (room == null || room.PsychologicallyOutdoors)
			return partners;

		foreach (Building_Bed bedInRoom in room.ContainedBeds)
		{
			foreach (Pawn other in bedInRoom.OwnersForReading)
			{
				if (other != pawn && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, other) && IsEligibleForLovin(other))
					partners.Add(other);
			}
		}

		// Ensure random but deterministic order for MP
		partners = [.. partners.OrderBy(_ => Rand.Value)];

		return partners;
	}

	public static List<Pawn> GetAllPartnersInRoom(Pawn pawn, bool ignoreReserveCheck = false)
	{
		if (pawn == null) return [];

		List<Pawn> directPartners = GetMyPartnersInBeds(pawn);
		if (directPartners.Count == 0)
			return [];

		Room room = pawn.CurrentBed()?.GetRoom();
		if (room == null || room.PsychologicallyOutdoors)
			return directPartners;

		List<Pawn> pawnsInRoom = [];
		foreach (Building_Bed bedInRoom in room.ContainedBeds)
		{
			pawnsInRoom.AddRange(bedInRoom.CurOccupants);
		}

		// BFS across partners to expand
		HashSet<Pawn> visited = new(directPartners) { pawn };
		Queue<Pawn> partnersToCheck = new(directPartners);

		while (partnersToCheck.Count > 0)
		{
			Pawn current = partnersToCheck.Dequeue();

			foreach (Pawn other in pawnsInRoom)
			{
				if (visited.Contains(other))
					continue;

				if (LovePartnerRelationUtility.LovePartnerRelationExists(current, other))
				{
					if (!IsEligibleForLovin(other))
						continue; // Skip ineligible pawns and do not traverse further

					visited.Add(other);
					partnersToCheck.Enqueue(other);
				}
			}
		}
		visited.Remove(pawn);
		List<Pawn> harem = visited
			.Where(partner => ignoreReserveCheck || (pawn.CanReserve(partner) && partner.CanReserve(pawn)))
			.ToList();

		return harem;
	}
}