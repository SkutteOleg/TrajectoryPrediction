using System;
using UnityEngine;

namespace TrajectoryPrediction;

public class AstroObjectTrajectory : MonoBehaviour
{
    private AstroObject _astroObject;
    private OWRigidbody _body;
    private GravityVolume _gravityVolume;
    private float _triggerRadius;
    private bool _updatedThisFrame;
    private Vector3[] _trajectory;
    private Vector3[] _trajectoryCache;
    private Vector3 _framePosition;
    private Vector3 _frameVelocity;

    private void Start()
    {
        _astroObject = GetComponent<AstroObject>();
        _body = _astroObject.GetOWRigidbody();
        _gravityVolume = _astroObject.GetGravityVolume();
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
        _trajectory = new Vector3[TrajectoryPrediction.StepsToSimulate];
        _trajectoryCache = new Vector3[TrajectoryPrediction.StepsToSimulate];
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
        _trajectory.CopyTo(_trajectoryCache);
    }

    internal void UpdateTrajectory()
    {
        if (_updatedThisFrame)
            return;

        _updatedThisFrame = true;

        if (_body == null || _body.GetAttachedForceDetector() == null)
        {
            for (var i = 0; i < _trajectory.Length; i++)
                _trajectory[i] = _astroObject.transform.position;
        }
        else
        {
            if (TrajectoryPrediction.Parallelization)
                TrajectoryPrediction.SimulateTrajectoryMultiThreaded(_body, _framePosition, _frameVelocity, _trajectory);
            else
                TrajectoryPrediction.SimulateTrajectory(_body, _framePosition, _frameVelocity, _trajectory);
        }
    }

    public Vector3 GetFuturePosition(int step)
    {
        return _trajectory[Math.Max(step, 0)];
    }
    
    public Vector3 GetFuturePositionCached(int step)
    {
        return _trajectoryCache[Math.Max(step, 0)];
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