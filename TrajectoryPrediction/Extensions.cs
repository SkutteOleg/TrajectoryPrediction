using UnityEngine;

namespace TrajectoryPrediction;

public static class Extensions
{
    public static Vector3 CalculateForceAccelerationAtFuturePoint(this GravityVolume gravityVolume, Vector3 worldPoint, int step)
    {
        var delta = gravityVolume.GetTrajectory().GetFuturePosition(step) - worldPoint;
        float distance = delta.magnitude;
        float gravityMagnitude = gravityVolume.CalculateGravityMagnitude(distance);
        return delta / distance * gravityMagnitude;
    }

    public static ITrajectory GetTrajectory(this GravityVolume gravityVolume)
    {
        return TrajectoryPrediction.GravityVolumeToTrajectoryMap.ContainsKey(gravityVolume) ? TrajectoryPrediction.GravityVolumeToTrajectoryMap[gravityVolume] : null;
    }

    public static ITrajectory GetTrajectory(this AstroObject astroObject)
    {
        return TrajectoryPrediction.AstroObjectToTrajectoryMap.ContainsKey(astroObject) ? TrajectoryPrediction.AstroObjectToTrajectoryMap[astroObject] : null;
    }
}