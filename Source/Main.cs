using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace PrisonerCaravanFix; 

[StaticConstructorOnStartup, UsedImplicitly]
class Main {
    static Main() {
        var harmony = new Harmony("com.github.harmony.rimworld.wsk.prisonercaravanfix");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
static class PawnExitMapFix {

    [UsedImplicitly]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var myInstructions = instructions.ToArray();
        var isPrisonerMethod = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsPrisoner));
        var callsPrisonerIndex = myInstructions.FirstIndexOf(i => i.Calls(isPrisonerMethod));
        var replaceIndex = callsPrisonerIndex + 1;
        myInstructions[replaceIndex].opcode = OpCodes.Brtrue_S;
        return myInstructions.AsEnumerable();
    }
}