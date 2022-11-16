using HarmonyLib;

namespace TrajectoryPrediction;

[HarmonyPatch(typeof(AstroObject))]
public class AstroObjectPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    // ReSharper disable once InconsistentNaming
    private static void AstroObject_Awake(AstroObject __instance)
    {
        __instance.gameObject.AddComponent<AstroObjectTrajectory>();;
    }
}