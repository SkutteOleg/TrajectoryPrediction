using HarmonyLib;

namespace TrajectoryPrediction;

[HarmonyPatch(typeof(MapMarker))]
public class CanvasMapMarkerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("InitMarker")]
    // ReSharper disable once InconsistentNaming
    private static void MapMarker_InitMarker(MapMarker __instance)
    {
        if (__instance._markerType is MapMarker.MarkerType.Ship or MapMarker.MarkerType.Probe or MapMarker.MarkerType.Player) 
            __instance._canvasMarker.gameObject.AddComponent<TrajectoryVisualizer>().SetMarkerType(__instance._markerType);
    }
}