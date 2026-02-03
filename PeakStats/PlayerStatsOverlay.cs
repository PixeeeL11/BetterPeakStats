using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PeakStats;

public sealed class PlayerStatsOverlay : MonoBehaviour
{
    private readonly Dictionary<int, PlayerStatsView> _views = new();
    private float _nextRefreshTime;
    private bool _visible = true;
    private SelectiveHudHiderCompat? _hudHider;

    private void Awake()
    {
        _hudHider = new SelectiveHudHiderCompat();
        _hudHider.TryInitialize();
    }

    private void Update()
    {
        if (PeakStatsPlugin.Instance == null)
        {
            return;
        }

        if (Input.GetKeyDown(PeakStatsPlugin.Instance.ToggleKey.Value))
        {
            _visible = !_visible;
        }

        if (!_visible)
        {
            SetAllVisible(false);
            return;
        }

        var allowHud = _hudHider?.ShouldShowHud() ?? true;
        if (!allowHud && !PeakStatsPlugin.Instance.ShowWhenHudHidden.Value)
        {
            SetAllVisible(false);
            return;
        }

        if (Time.time < _nextRefreshTime)
        {
            UpdateViewPositions();
            return;
        }

        _nextRefreshTime = Time.time + PeakStatsPlugin.Instance.RefreshInterval.Value;
        RefreshViews();
    }

    private void RefreshViews()
    {
        var localPlayer = PlayerLocator.TryGetLocalPlayer();
        if (localPlayer == null)
        {
            SetAllVisible(false);
            return;
        }

        var nearbyPlayers = PlayerLocator.GetNearbyPlayers(localPlayer, PeakStatsPlugin.Instance.MaxDistance.Value);

        var activeIds = new HashSet<int>();
        foreach (var player in nearbyPlayers)
        {
            activeIds.Add(player.PlayerId);
            if (!_views.TryGetValue(player.PlayerId, out var view))
            {
                view = CreateView(player.PlayerId);
                _views[player.PlayerId] = view;
            }

            view.UpdateData(player);
            view.UpdatePosition(player.WorldPosition);
            view.SetVisible(true);
        }

        foreach (var kvp in _views)
        {
            if (!activeIds.Contains(kvp.Key))
            {
                kvp.Value.SetVisible(false);
            }
        }
    }

    private void UpdateViewPositions()
    {
        foreach (var kvp in _views)
        {
            kvp.Value.UpdatePositionFromCache();
        }
    }

    private void SetAllVisible(bool visible)
    {
        foreach (var kvp in _views)
        {
            kvp.Value.SetVisible(visible);
        }
    }

    private PlayerStatsView CreateView(int playerId)
    {
        var viewRoot = new GameObject($"PeakStatsView_{playerId}");
        viewRoot.transform.SetParent(transform, false);
        return viewRoot.AddComponent<PlayerStatsView>();
    }
}

public sealed class PlayerStatsView : MonoBehaviour
{
    private TextMeshPro _text = null!;
    private Canvas _canvas = null!;
    private Vector3 _cachedWorldPosition;

    private void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 500;

        var canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 30f;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(transform, false);
        _text = textObject.AddComponent<TextMeshPro>();
        _text.fontSize = 2.2f;
        _text.color = Color.white;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = false;
    }

    public void UpdateData(PlayerSnapshot snapshot)
    {
        _text.text = $"{snapshot.DisplayName}\nHP: {snapshot.Health:0}\nStamina: {snapshot.Stamina:0}\nPing: {snapshot.Ping} ms";
    }

    public void UpdatePosition(Vector3 worldPosition)
    {
        _cachedWorldPosition = worldPosition;
        UpdatePositionFromCache();
    }

    public void UpdatePositionFromCache()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        var offsetPosition = _cachedWorldPosition + Vector3.up * 2.1f;
        transform.position = offsetPosition;
        transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
    }

    public void SetVisible(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.enabled = visible;
        }
    }
}

public static class PlayerLocator
{
    public static PlayerSnapshot? TryGetLocalPlayer()
    {
        return FakeGameApi.TryGetLocalPlayer();
    }

    public static List<PlayerSnapshot> GetNearbyPlayers(PlayerSnapshot localPlayer, float maxDistance)
    {
        return FakeGameApi.GetPlayersAround(localPlayer, maxDistance);
    }
}

public sealed class PlayerSnapshot
{
    public int PlayerId { get; init; }
    public string DisplayName { get; init; } = "Unknown";
    public float Health { get; init; }
    public float Stamina { get; init; }
    public int Ping { get; init; }
    public Vector3 WorldPosition { get; init; }
}

internal static class FakeGameApi
{
    public static PlayerSnapshot? TryGetLocalPlayer()
    {
        return null;
    }

    public static List<PlayerSnapshot> GetPlayersAround(PlayerSnapshot localPlayer, float maxDistance)
    {
        return new List<PlayerSnapshot>();
    }
}
