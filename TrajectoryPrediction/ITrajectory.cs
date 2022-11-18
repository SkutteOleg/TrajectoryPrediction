using UnityEngine;

namespace TrajectoryPrediction;

public interface ITrajectory
{
    bool Busy { get; }
    Vector3[] Trajectory { get; }
    Vector3[] TrajectoryCache { get; }
    void UpdateTrajectory();
    Vector3 GetCurrentPosition();
    Vector3 GetFuturePosition(int step);
    Vector3 GetFuturePositionCached(int step);
}