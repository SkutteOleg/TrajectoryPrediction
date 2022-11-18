using System;
using System.Threading;
using UnityEngine;

namespace TrajectoryPrediction;

public class AstroObjectTrajectory : MonoBehaviour, ITrajectory
{
    public bool Busy { get; private set; }
    public Vector3[] Trajectory { get; private set; }
    public Vector3[] TrajectoryCache { get; private set; }
    private AstroObject _astroObject;
    private OWRigidbody _body;
    private GravityVolume _gravityVolume;
    private float _triggerRadius;
    private bool _updatedThisFrame;
    private Vector3 _framePosition;
    private Vector3 _frameVelocity;
    private TrajectoryVisualizer _visualizer;

    private void Start()
    {
        _astroObject = GetComponent<AstroObject>();
        _body = _astroObject.GetOWRigidbody();
        _gravityVolume = _astroObject.GetGravityVolume();
        
        if (_gravityVolume)
            _visualizer = gameObject.AddComponent<TrajectoryVisualizer>();
        
        TrajectoryPrediction.AstroObjectToTrajectoryMap[_astroObject] = this;
        if (_gravityVolume)
        {
            if (_gravityVolume.GetOWTriggerVolume().GetShape())
                _triggerRadius = _gravityVolume.GetOWTriggerVolume().GetShape().localBounds.radius;
            if (_gravityVolume.GetOWTriggerVolume().GetOWCollider())
                _triggerRadius = ((SphereCollider)_gravityVolume.GetOWTriggerVolume().GetOWCollider().GetCollider()).radius;

            TrajectoryPrediction.GravityVolumeToTrajectoryMap[_gravityVolume] = this;
            TrajectoryPrediction.AddTrajectory(this);
        }

        ApplyConfig();

        TrajectoryPrediction.OnConfigUpdate += ApplyConfig;
        TrajectoryPrediction.OnBeginFrame += BeginFrame;
        TrajectoryPrediction.OnEndFrame += EndFrame;
    }

    private void OnDestroy()
    {
        TrajectoryPrediction.AstroObjectToTrajectoryMap.Remove(_astroObject);
        if (_gravityVolume)
        {
            TrajectoryPrediction.GravityVolumeToTrajectoryMap.Remove(_gravityVolume);
            TrajectoryPrediction.RemoveTrajectory(this);
        }

        TrajectoryPrediction.OnConfigUpdate -= ApplyConfig;
        TrajectoryPrediction.OnBeginFrame -= BeginFrame;
        TrajectoryPrediction.OnEndFrame -= EndFrame;
    }

    private void ApplyConfig()
    {
        Trajectory = new Vector3[TrajectoryPrediction.StepsToSimulate];
        TrajectoryCache = new Vector3[TrajectoryPrediction.StepsToSimulate];
        Busy = false;

        if (_visualizer)
        {
            _visualizer.SetVisibility(TrajectoryPrediction.DisplayPlanetTrajectories);
            _visualizer.SetColor(TrajectoryPrediction.PlanetTrajectoriesColor);
        }
    }

    private void BeginFrame()
    {
        _updatedThisFrame = false;
        if (_body)
        {
            _framePosition = _body.GetPosition();
            _frameVelocity = _body.GetVelocity();
        }
        else
            _framePosition = _astroObject.transform.position;
    }

    private void EndFrame()
    {
        Trajectory.CopyTo(TrajectoryCache);
    }

    public void UpdateTrajectory()
    {
        if (_updatedThisFrame)
            return;

        _updatedThisFrame = true;

        if (_body == null || _body.GetAttachedForceDetector() == null)
        {
            for (var i = 0; i < Trajectory.Length; i++)
                Trajectory[i] = _astroObject.transform.position;
        }
        else
        {
            if (TrajectoryPrediction.Parallelization || TrajectoryPrediction.Multithreading && Thread.CurrentThread == TrajectoryPrediction.MainThread)
            {
                Busy = true;
                TrajectoryPrediction.SimulateTrajectoryMultiThreaded(_body, _framePosition, _frameVelocity, Trajectory, null, false, false, () => Busy = false);
            }
            else
                TrajectoryPrediction.SimulateTrajectory(_body, _framePosition, _frameVelocity, Trajectory);
        }
    }

    public Vector3 GetCurrentPosition()
    {
        return _body ? _body.GetPosition() : _astroObject.transform.position;
    }

    public Vector3 GetFuturePosition(int step)
    {
        return Trajectory[Math.Max(step, 0)];
    }

    public Vector3 GetFuturePositionCached(int step)
    {
        return TrajectoryCache[Math.Max(step, 0)];
    }

    public GravityVolume GetGravityVolume()
    {
        return _gravityVolume;
    }

    public float GetTriggerRadius()
    {
        return _triggerRadius;
    }

    public Vector3 GetFramePosition()
    {
        return _framePosition;
    }
}