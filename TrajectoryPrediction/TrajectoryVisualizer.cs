using System;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace TrajectoryPrediction;

public class TrajectoryVisualizer : MonoBehaviour
{
    public bool Busy { get; private set; }
    private CanvasMapMarker _marker;
    private MapMarker.MarkerType _markerType;
    private LineRenderer _lineRenderer;
    private OWRigidbody _body;
    private ForceDetector _forceDetector;
    private Vector3[] _trajectory;
    private Vector3[] _trajectoryCached;
    private float _timeSinceUpdate;
    private Vector3 _framePosition;
    private Vector3 _frameVelocity;

    private void Start()
    {
        _marker = GetComponent<CanvasMapMarker>();
        _body = _marker._rigidbodyTarget;
        _forceDetector = _body.GetAttachedForceDetector();
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        var mapSatelliteLine = FindObjectOfType<MapSatelliteOrbitLine>().GetComponent<LineRenderer>();
        _lineRenderer.material = mapSatelliteLine.material;
        _lineRenderer.textureMode = mapSatelliteLine.textureMode;
        TrajectoryPrediction.AddVisualizer(this);
        ApplyConfig();

        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
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
        TrajectoryPrediction.RemoveVisualizer(this);
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
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
        _lineRenderer.enabled = value;
    }

    private void ApplyConfig()
    {
        _trajectory = new Vector3[TrajectoryPrediction.StepsToSimulate];
        _trajectoryCached = new Vector3[TrajectoryPrediction.StepsToSimulate];
        _lineRenderer.positionCount = TrajectoryPrediction.SecondsToPredict;
        Busy = false;
        switch (_markerType)
        {
            case MapMarker.MarkerType.Player:
                _lineRenderer.startColor = TrajectoryPrediction.PlayerTrajectoryColor;
                _lineRenderer.endColor = new Color(TrajectoryPrediction.PlayerTrajectoryColor.r, TrajectoryPrediction.PlayerTrajectoryColor.g, TrajectoryPrediction.PlayerTrajectoryColor.b, 0);
                break;
            case MapMarker.MarkerType.Ship:
                _lineRenderer.startColor = TrajectoryPrediction.ShipTrajectoryColor;
                _lineRenderer.endColor = new Color(TrajectoryPrediction.ShipTrajectoryColor.r, TrajectoryPrediction.ShipTrajectoryColor.g, TrajectoryPrediction.ShipTrajectoryColor.b, 0);
                break;
            case MapMarker.MarkerType.Probe:
                _lineRenderer.startColor = TrajectoryPrediction.ScoutTrajectoryColor;
                _lineRenderer.endColor = new Color(TrajectoryPrediction.ScoutTrajectoryColor.r, TrajectoryPrediction.ScoutTrajectoryColor.g, TrajectoryPrediction.ScoutTrajectoryColor.b, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_markerType));
        }
    }

    private void BeginFrame()
    {
        _framePosition = _body.GetPosition();
        _frameVelocity = _body.GetVelocity();
        _timeSinceUpdate = 0;
    }

    private void EndFrame()
    {
        _trajectory.CopyTo(_trajectoryCached);
    }

    private void OnEnterMapView()
    {
        _lineRenderer.enabled = _marker.IsVisible();
        Busy = false;
    }

    private void OnExitMapView()
    {
        _lineRenderer.enabled = false;
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

    public void Visualize()
    {
        if (!_lineRenderer.enabled)
            return;

        if (_forceDetector._activeVolumes.Count == 0 || _forceDetector._activeVolumes.All(volume => volume is not GravityVolume))
        {
            if (TrajectoryPrediction.Multithreading)
            {
                Busy = true;
                new Thread(() =>
                {
                    for (var i = 0; i < _trajectory.Length; i++)
                        _trajectory[i] = Vector3.zero;

                    Busy = false;
                }).Start();
            }
            else
                for (var i = 0; i < _trajectory.Length; i++)
                    _trajectory[i] = _framePosition;
        }
        else
        {
            if (TrajectoryPrediction.Multithreading)
            {
                Busy = true;
                TrajectoryPrediction.SimulateTrajectoryMultiThreaded(_body, _framePosition, _frameVelocity, _trajectory, Locator.GetReferenceFrame()?.GetAstroObject(), true, TrajectoryPrediction.PredictGravityVolumeIntersections, () => Busy = false);
            }
            else
                TrajectoryPrediction.SimulateTrajectory(_body, _framePosition, _frameVelocity, _trajectory, Locator.GetReferenceFrame()?.GetAstroObject(), true, TrajectoryPrediction.PredictGravityVolumeIntersections);
        }
    }

    private void Update()
    {
        if (!_lineRenderer.enabled)
            return;

        _timeSinceUpdate += Time.deltaTime;

        var referenceFrame = Locator.GetReferenceFrame()?.GetAstroObject() ?? Locator._sun.GetOWRigidbody().GetReferenceFrame().GetAstroObject();

        for (var i = 0; i < _lineRenderer.positionCount; i++)
        {
            int step = TrajectoryPrediction.HighPrecisionMode ? Mathf.Min((int)((i + _timeSinceUpdate) / Time.fixedDeltaTime), _trajectoryCached.Length - 1) : i;

            if (_trajectoryCached[step] == Vector3.zero)
            {
                _lineRenderer.SetPosition(i, _lineRenderer.GetPosition(Mathf.Max(0, i - 1)));
                continue;
            }

            var position = _trajectoryCached[step] - referenceFrame.GetTrajectory().GetFuturePositionCached(step) + referenceFrame.GetOWRigidbody().GetPosition();
            _lineRenderer.SetPosition(i, position);
        }

        _lineRenderer.widthMultiplier = Vector3.Distance(_body.GetPosition(), Locator.GetActiveCamera().transform.position) / 500f;
    }
}