using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrisonerCaravanFix; 

[StaticConstructorOnStartup, UsedImplicitly]
class Main {
    static Main() {
        var harmony = new Harmony("com.github.harmony.rimworld.wsk.prisonercaravanfix");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

#if DEBUG 
[HarmonyDebug]
#endif
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
static class PawnExitMapFix {

    [Conditional("DEBUG")]
    static void DebugLog(string message) {
        Log.Message(message);
    }
    
    static bool IsFree(Pawn target) {
        var isInDropPod = ThingOwnerUtility.AnyParentIs<ActiveDropPodInfo>(target)
            || ThingOwnerUtility.AnyParentIs<TravelingTransportPods>(target);
        var result = (!target.IsCaravanMember() && !target.teleporting && !isInDropPod)
            || (!target.IsPrisoner && !target.IsSlaveOfColony && !isInDropPod)
            || target.guest is { Released: true };
        return result;
    }
    
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var instructionArray = instructions.ToArray();
        DebugLog(
            $"[WSK PrisonerCaravanFix] Transpiling Pawn.ExitMap method. Method has {instructionArray.Length} IL instructions."
        );

        // Instruction to call our IsFree method
        var loadThisInstruction = new CodeInstruction(OpCodes.Ldarg_0);
        var callIsFreeInstruction = new CodeInstruction(
            OpCodes.Call,
            AccessTools.DeclaredMethod(
                typeof(PawnExitMapFix),
                nameof(IsFree)
            )
        );
        var storeFreeInstruction = new CodeInstruction(OpCodes.Stloc_2);
        var myInstructions = new[] {
            loadThisInstruction, // `this`
            callIsFreeInstruction, // will call `IsFree(this)` from the Exitmap method
            storeFreeInstruction // will store the result of the call in Stloc_2, which is the `free` variable
        };
        
        // `bool flag` is stored at Stloc_1, 7 instructions earlier is the start of the conditional to assign flag
        // This is the start of the code to replace
        var startIndex = instructionArray.FirstIndexOf(ci => ci.opcode == OpCodes.Stloc_1) - 7;
        // Quite sensibly, the code to replace ends when the method calls Ldloc_2 (as we are storing in this location)
        var endIndex = instructionArray.FirstIndexOf(ci => ci.opcode == OpCodes.Ldloc_2);
        // We will replace the instructions from startIndex to endIndex with this call to our IsFree method instead and load the result

        DebugLog(
            $"[WSK PrisonerCaravanFix] startIndex is {startIndex}, endIndex is {endIndex}."
        );
        var instructionsBefore = instructionArray.Take(startIndex).ToArray();
        DebugLog(
            $"[WSK PrisonerCaravanFix] Last instruction of instructionsBefore is {instructionsBefore.Last().opcode} - {instructionsBefore.Last().operand}"
        );
        var instructionsAfter = instructionArray.Skip(endIndex).ToArray();
        DebugLog(
            $"[WSK PrisonerCaravanFix] First instruction of instructionsAfter is {instructionsAfter.First().opcode} - {instructionsAfter.First().operand}"
        );
        var skippedInstructions = instructionArray.Skip(startIndex).TakeWhile(ci => ci.opcode != OpCodes.Ldloc_2);
        var labelsToFix = skippedInstructions.SelectMany(ci => ci.labels);
        loadThisInstruction.labels.AddRange(labelsToFix);
        var result = instructionsBefore.Concat(myInstructions).Concat(instructionsAfter).ToArray();
        DebugLog(
            $"[WSK PrisonerCaravanFix] Finished preparing new instructions for Pawn.ExitMap. Method now has {result.Length} IL instructions."
        );
        
        return result;
    }
}