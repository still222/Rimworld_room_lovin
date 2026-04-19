using HarmonyLib;
using Verse;

namespace SameRoomLovin;

[StaticConstructorOnStartup]
public static class Startup
{
	static Startup()
	{
		var harmony = new Harmony("stk.SameRoomLovin");

		harmony.PatchAll();
	}
}