using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.Scripting;

namespace TrajectoryPrediction;

public class TrajectoryPrediction : ModBehaviour
{
    public static int SecondsToPredict { get; private set; }
    public static int StepsToSimulate { get; private set; }
    public static bool HighPrecisionMode { get; private set; }
    public static bool PredictGravityVolumeIntersections { get; private set; }
    public static bool Multithreading { get; private set; }
    
    public static Color PlayerTrajectoryColor { get; private set; }
    public static Color ShipTrajectoryColor { get; private set; }
    public static Color ScoutTrajectoryColor { get; private set; }
    public static bool DisplayPlanetTrajectories { get; private set; }
    public static Color PlanetTrajectoriesColor { get; private set; }

    public static bool Parallelization { get; private set; }
    internal static Thread MainThread { get; private set; }
    
    public static event Action OnConfigUpdate;
    public static event Action OnBeginFrame;
    public static event Action OnEndFrame;

    internal static readonly Dictionary<AstroObject, ITrajectory> AstroObjectToTrajectoryMap = new();
    internal static readonly Dictionary<GravityVolume, ITrajectory> GravityVolumeToTrajectoryMap = new();
    private static readonly List<AstroObjectTrajectory> AstroObjectTrajectories = new();
    private static readonly List<TrajectoryVisualizer> TrajectoryVisualizers = new();
    private static readonly List<OWRigidbody> BusyBodies = new();
    private const float MemoryFootprint = 0.3f;
    private static bool _active;

    private void Awake()
    {
        MainThread = Thread.CurrentThread;
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
    }

    private void OnDestroy()
    {
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
    }

    private static void OnEnterMapView()
    {
        _active = true;
    }

    private static void OnExitMapView()
    {
        _active = false;
    }

    public override void Configure(IModConfig config)
    {
        SecondsToPredict = Math.Max(config.GetSettingsValue<int>("Seconds To Predict"), 0);
        HighPrecisionMode = config.GetSettingsValue<bool>("High Precision Mode");
        StepsToSimulate = HighPrecisionMode ? (int)(SecondsToPredict / Time.fixedDeltaTime) : SecondsToPredict;
        PredictGravityVolumeIntersections = config.GetSettingsValue<bool>("Predict GravityVolume Intersections");
        PlayerTrajectoryColor = ColorUtility.TryParseHtmlString(config.GetSettingsValue<string>("Player Trajectory Color"), out var playerTrajectoryColor) ? playerTrajectoryColor : Color.cyan;
        ShipTrajectoryColor = ColorUtility.TryParseHtmlString(config.GetSettingsValue<string>("Ship Trajectory Color"), out var shipTrajectoryColor) ? shipTrajectoryColor : Color.yellow;
        ScoutTrajectoryColor = ColorUtility.TryParseHtmlString(config.GetSettingsValue<string>("Scout Trajectory Color"), out var scoutTrajectoryColor) ? scoutTrajectoryColor : Color.white;
        DisplayPlanetTrajectories = config.GetSettingsValue<bool>("Display Planet Trajectories");
        PlanetTrajectoriesColor = ColorUtility.TryParseHtmlString(config.GetSettingsValue<string>("Planet Trajectories Color"), out var planetTrajectoryColor) ? planetTrajectoryColor : Color.white;
        Multithreading = config.GetSettingsValue<bool>("Multithreading");
        Parallelization = config.GetSettingsValue<bool>("Parallelization");

        BusyBodies.Clear();
        OnConfigUpdate?.Invoke();
        BeginFrame();
    }

    private void FixedUpdate()
    {
        if (!_active)
            return;

        if (Multithreading)
        {
            if (TrajectoryVisualizers.Any(visualizer => visualizer.Busy))
                return;

            EndFrame();
            BeginFrame();
            foreach (var visualizer in TrajectoryVisualizers)
                visualizer.Visualize();
        }
        else
        {
            BeginFrame();
            foreach (var visualizer in TrajectoryVisualizers)
                visualizer.Visualize();
            EndFrame();
        }
    }
    
    private static void BeginFrame()
    {
        OnBeginFrame?.Invoke();
    }

    private static void EndFrame()
    {
        OnEndFrame?.Invoke();
    }

    public static void SimulateTrajectoryMultiThreaded(OWRigidbody body, Vector3 startingPosition, Vector3 startingVelocity, Vector3[] trajectory, AstroObject referenceAstroObject = null, bool stopOnCollision = false, bool predictVolumeIntersections = false, Action onExit = null)
    {
        if (BusyBodies.Contains(body))
        {
            onExit?.Invoke();
            return;
        }

        BusyBodies.Add(body);

        new Thread(() =>
        {
            SimulateTrajectory(body, startingPosition, startingVelocity, trajectory, referenceAstroObject, stopOnCollision, predictVolumeIntersections);

            lock (BusyBodies)
                BusyBodies.Remove(body);

            onExit?.Invoke();
        }).Start();
    }

    public static void SimulateTrajectory(OWRigidbody body, Vector3 startingPosition, Vector3 startingVelocity, Vector3[] trajectory, AstroObject referenceAstroObject = null, bool stopOnCollision = false, bool predictVolumeIntersections = false)
    {
        var position = startingPosition;
        var velocity = HighPrecisionMode ? startingVelocity * Time.fixedDeltaTime : startingVelocity;
        var forceDetector = body.GetAttachedForceDetector();
        var inheritedDetector = forceDetector._activeInheritedDetector;
        GravityVolume[] activeVolumes = forceDetector._activeVolumes.Select(volume => volume as GravityVolume).ToArray();

        if (referenceAstroObject)
            referenceAstroObject.GetTrajectory().UpdateTrajectory();

        if (predictVolumeIntersections)
        {
            foreach (var astroObjectTrajectory in AstroObjectTrajectories)
                astroObjectTrajectory.UpdateTrajectory();
        }
        else
        {
            foreach (var volume in activeVolumes)
                volume.GetTrajectory().UpdateTrajectory();

            if (inheritedDetector != null)
                inheritedDetector._attachedBody.GetReferenceFrame().GetAstroObject().GetTrajectory().UpdateTrajectory();
        }

        trajectory[0] = startingPosition;

        var collision = false;

        for (var step = 1; step < trajectory.Length; step++)
        {
            if (stopOnCollision && collision)
            {
                trajectory[step] = Vector3.zero;
                continue;
            }

            if (predictVolumeIntersections)
                activeVolumes = AstroObjectTrajectories.Where(astroObject => Vector3.Distance(position, astroObject.GetFuturePosition(step)) < astroObject.GetTriggerRadius()).Select(astroObject => astroObject.GetGravityVolume()).ToArray();

            var acceleration = GetAccelerationAtFutureWorldPoint(activeVolumes, position, step, forceDetector._fieldMultiplier, () => collision = true);

            if (inheritedDetector != null)
            {
                GravityVolume[] inheritedDetectorActiveVolumes = inheritedDetector._activeVolumes.Select(volume => volume as GravityVolume).ToArray();
                var inheritedDetectorFuturePosition = inheritedDetector._attachedBody.GetReferenceFrame().GetAstroObject().GetTrajectory().GetFuturePosition(step);
                acceleration += GetAccelerationAtFutureWorldPoint(inheritedDetectorActiveVolumes, inheritedDetectorFuturePosition, step, inheritedDetector._fieldMultiplier);
            }
            
            if (HighPrecisionMode)
                acceleration *= Time.fixedDeltaTime * Time.fixedDeltaTime;
            
            velocity += acceleration;
            position += velocity;
            trajectory[step] = position;
        }
    }

    private static Vector3 GetAccelerationAtFutureWorldPoint(IEnumerable<GravityVolume> gravityVolumes, Vector3 worldPoint, int step, float multiplier, Action collisionCallback = null)
    {
        var acceleration = Vector3.zero;
        foreach (var gravityVolume in gravityVolumes)
        {
            if (Vector3.Distance(worldPoint, gravityVolume.GetTrajectory().GetFuturePosition(step - 1)) < gravityVolume._upperSurfaceRadius)
                collisionCallback?.Invoke();

            acceleration += gravityVolume.CalculateForceAccelerationAtFuturePoint(worldPoint, step) * multiplier;
        }

        return acceleration;
    }

    public static void AddTrajectory(AstroObjectTrajectory astroObject)
    {
        AstroObjectTrajectories.Add(astroObject);
    }

    public static void RemoveTrajectory(AstroObjectTrajectory astroObject)
    {
        AstroObjectTrajectories.Remove(astroObject);
    }

    public static void AddVisualizer(TrajectoryVisualizer visualizer)
    {
        TrajectoryVisualizers.Add(visualizer);
    }

    public static void RemoveVisualizer(TrajectoryVisualizer visualizer)
    {
        TrajectoryVisualizers.Remove(visualizer);
    }
}