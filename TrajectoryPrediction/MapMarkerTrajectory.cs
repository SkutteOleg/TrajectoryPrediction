using System;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace TrajectoryPrediction;

public class MapMarkerTrajectory : MonoBehaviour, ITrajectory
{
    public bool Busy { get; private set; }
    public Vector3[] Trajectory { get; private set; }
    public Vector3[] TrajectoryCache { get; private set; }
    private CanvasMapMarker _marker;
    private MapMarker.MarkerType _markerType;
    private OWRigidbody _body;
    private ForceDetector _forceDetector;
    private bool _updatedThisFrame;
    private Vector3 _framePosition;
    private Vector3 _frameVelocity;
    private TrajectoryVisualizer _visualizer;

    private void Start()
    {
        _marker = GetComponent<CanvasMapMarker>();
        _body = _marker._rigidbodyTarget;
        _forceDetector = _body.GetAttachedForceDetector();
        _visualizer = gameObject.AddComponent<TrajectoryVisualizer>();
        ApplyConfig();

        TrajectoryPrediction.OnConfigUpdate += ApplyConfig;
        TrajectoryPrediction.OnBeginFrame += BeginFrame;
        TrajectoryPrediction.OnEndFrame += EndFrame;
        _marker.OnMarkerChangeVisibility += SetVisibility;

        if (_markerType == MapMarker.MarkerType.Player)
        {
            GlobalMessenger.AddListener("EnterShip", OnEnterShip);
            GlobalMessenger.AddListener("ExitShip", OnExitShip);
            OnEnterShip();
        }
    }

    private void OnDestroy()
    {
        TrajectoryPrediction.OnConfigUpdate -= ApplyConfig;
        TrajectoryPrediction.OnBeginFrame -= BeginFrame;
        TrajectoryPrediction.OnEndFrame -= EndFrame;
        _marker.OnMarkerChangeVisibility -= SetVisibility;

        if (_markerType == MapMarker.MarkerType.Player)
        {
            GlobalMessenger.RemoveListener("EnterShip", OnEnterShip);
            GlobalMessenger.RemoveListener("ExitShip", OnExitShip);
        }
    }

    internal void SetMarkerType(MapMarker.MarkerType markerType)
    {
        _markerType = markerType;
    }

    private void SetVisibility(bool value)
    {
        _visualizer.SetVisibility(value);
    }

    private void ApplyConfig()
    {
        Trajectory = new Vector3[TrajectoryPrediction.StepsToSimulate];
        TrajectoryCache = new Vector3[TrajectoryPrediction.StepsToSimulate];
        Busy = false;
        switch (_markerType)
        {
            case MapMarker.MarkerType.Player:
                _visualizer.SetColor(TrajectoryPrediction.PlayerTrajectoryColor);
                break;
            case MapMarker.MarkerType.Ship:
                _visualizer.SetColor(TrajectoryPrediction.ShipTrajectoryColor);
                break;
            case MapMarker.MarkerType.Probe:
                _visualizer.SetColor(TrajectoryPrediction.ScoutTrajectoryColor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_markerType));
        }
    }

    private void BeginFrame()
    {
        _updatedThisFrame = false;
        _framePosition = _body.GetPosition();
        _frameVelocity = _body.GetVelocity();
    }

    private void EndFrame()
    {
        Trajectory.CopyTo(TrajectoryCache);
    }

    private void OnEnterShip()
    {
        _body = Locator.GetShipBody();
        _forceDetector = _body.GetAttachedForceDetector();
    }

    private void OnExitShip()
    {
        _body = Locator.GetPlayerBody();
        _forceDetector = _body.GetAttachedForceDetector();
    }

    public void UpdateTrajectory()
    {
        if (_updatedThisFrame)
            return;

        _updatedThisFrame = true;
        
        if (_forceDetector._activeVolumes.Count == 0 || _forceDetector._activeVolumes.All(volume => volume is not GravityVolume))
        {
            if (TrajectoryPrediction.Multithreading)
            {
                Busy = true;
                new Thread(() =>
                {
                    for (var i = 0; i < Trajectory.Length; i++)
                        Trajectory[i] = Vector3.zero;

                    Busy = false;
                }).Start();
            }
            else
                for (var i = 0; i < Trajectory.Length; i++)
                    Trajectory[i] = _framePosition;
        }
        else
        {
            if (TrajectoryPrediction.Multithreading)
            {
                Busy = true;
                TrajectoryPrediction.SimulateTrajectoryMultiThreaded(_body, _framePosition, _frameVelocity, Trajectory, Locator.GetReferenceFrame()?.GetAstroObject(), true, TrajectoryPrediction.PredictGravityVolumeIntersections, () => Busy = false);
            }
            else
                TrajectoryPrediction.SimulateTrajectory(_body, _framePosition, _frameVelocity, Trajectory, Locator.GetReferenceFrame()?.GetAstroObject(), true, TrajectoryPrediction.PredictGravityVolumeIntersections);
        }
    }

    public Vector3 GetCurrentPosition()
    {
        return _body.GetPosition();
    }

    public Vector3 GetFuturePosition(int step)
    {
        return Trajectory[Math.Max(step, 0)];
    }

    public Vector3 GetFuturePositionCached(int step)
    {
        return TrajectoryCache[Math.Max(step, 0)];
    }
}