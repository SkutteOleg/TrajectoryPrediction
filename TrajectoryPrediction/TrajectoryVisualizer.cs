using System.Linq;
using UnityEngine;

namespace TrajectoryPrediction;

public class TrajectoryVisualizer : MonoBehaviour
{
    public bool Busy => _trajectory.Busy;
    private ITrajectory _trajectory;
    private LineRenderer _lineRenderer;
    private float _timeSinceUpdate;
    private bool _visible = true;
    private Color _color = Color.white;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    private void Start()
    {
        _trajectory = GetComponent<ITrajectory>();
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        var material = new Material(Shader.Find("Outer Wilds/Effects/Orbit Line"));
        material.SetTexture(MainTex, Resources.FindObjectsOfTypeAll<Texture2D>().First(texture => texture.name == "Effects_SPA_OrbitLine_Dotted_d"));
        _lineRenderer.material = material;
        _lineRenderer.textureMode = LineTextureMode.RepeatPerSegment;
        
        Enable();
    }

    private void Enable()
    {
        if (_trajectory == null)
            return;

        TrajectoryPrediction.AddVisualizer(this);
        UpdateVisibility();
        UpdateColor();
        ApplyConfig();

        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
        TrajectoryPrediction.OnConfigUpdate += ApplyConfig;
        TrajectoryPrediction.OnBeginFrame += BeginFrame;
    }

    private void OnEnable()
    {
        Enable();
    }

    private void OnDisable()
    {
        TrajectoryPrediction.RemoveVisualizer(this);
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
        TrajectoryPrediction.OnConfigUpdate -= ApplyConfig;
        TrajectoryPrediction.OnBeginFrame -= BeginFrame;
    }

    public void SetVisibility(bool value)
    {
        _visible = value;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (!_lineRenderer)
            return;

        _lineRenderer.enabled = _visible;
    }

    public void SetColor(Color color)
    {
        _color = color;
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (!_lineRenderer)
            return;

        _lineRenderer.startColor = _color;
        _lineRenderer.endColor = new Color(_color.r, _color.g, _color.b, 0);
    }

    private void ApplyConfig()
    {
        _lineRenderer.positionCount = TrajectoryPrediction.SecondsToPredict;
    }

    private void BeginFrame()
    {
        _timeSinceUpdate = 0;
    }

    private void OnEnterMapView()
    {
        _lineRenderer.enabled = _visible;
    }

    private void OnExitMapView()
    {
        _lineRenderer.enabled = false;
    }

    public void Visualize()
    {
        if (!_lineRenderer.enabled)
            return;

        _trajectory.UpdateTrajectory();
    }

    private void Update()
    {
        if (!_lineRenderer.enabled)
            return;

        _timeSinceUpdate += Time.deltaTime;

        var referenceFrame = Locator.GetReferenceFrame()?.GetAstroObject() ?? Locator._sun.GetOWRigidbody().GetReferenceFrame().GetAstroObject();

        for (var i = 0; i < _lineRenderer.positionCount; i++)
        {
            var step = TrajectoryPrediction.HighPrecisionMode ? Mathf.Min((int) ((i + _timeSinceUpdate) / Time.fixedDeltaTime), _trajectory.TrajectoryCache.Length - 1) : i;

            if (_trajectory.TrajectoryCache[step] == Vector3.zero)
            {
                _lineRenderer.SetPosition(i, _lineRenderer.GetPosition(Mathf.Max(0, i - 1)));
                continue;
            }

            var position = _trajectory.TrajectoryCache[step] - referenceFrame.GetTrajectory().GetFuturePositionCached(step) + referenceFrame.GetOWRigidbody().GetPosition();
            _lineRenderer.SetPosition(i, position);
        }

        _lineRenderer.widthMultiplier = Vector3.Distance(_trajectory.GetCurrentPosition(), Locator.GetActiveCamera().transform.position) / 500f;
    }
}