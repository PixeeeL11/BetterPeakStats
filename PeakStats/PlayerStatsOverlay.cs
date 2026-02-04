using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        return ReflectionGameApi.TryGetLocalPlayer();
    }

    public static List<PlayerSnapshot> GetNearbyPlayers(PlayerSnapshot localPlayer, float maxDistance)
    {
        return ReflectionGameApi.GetPlayersAround(localPlayer, maxDistance);
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

internal static class ReflectionGameApi
{
    private static readonly Lazy<ReflectionCache> Cache = new(() => new ReflectionCache());

    public static PlayerSnapshot? TryGetLocalPlayer()
    {
        return Cache.Value.TryGetLocalPlayer();
    }

    public static List<PlayerSnapshot> GetPlayersAround(PlayerSnapshot localPlayer, float maxDistance)
    {
        return Cache.Value.GetPlayersAround(localPlayer, maxDistance);
    }
}

internal sealed class ReflectionCache
{
    private readonly Type? _characterType;
    private readonly PropertyInfo? _localCharacterProperty;
    private readonly PropertyInfo? _allCharactersProperty;
    private readonly PropertyInfo? _centerProperty;
    private readonly PropertyInfo? _characterNameProperty;
    private readonly PropertyInfo? _isLocalProperty;
    private readonly PropertyInfo? _refsProperty;
    private readonly PropertyInfo? _statsProperty;
    private readonly PropertyInfo? _heightProperty;
    private readonly PropertyInfo? _healthProperty;
    private readonly PropertyInfo? _dataProperty;
    private readonly PropertyInfo? _currentStaminaProperty;
    private readonly PropertyInfo? _pingProperty;

    public ReflectionCache()
    {
        _characterType = Type.GetType("Character, Assembly-CSharp");
        if (_characterType == null)
        {
            return;
        }

        _localCharacterProperty = _characterType.GetProperty("localCharacter", BindingFlags.Public | BindingFlags.Static);
        _allCharactersProperty = _characterType.GetProperty("AllCharacters", BindingFlags.Public | BindingFlags.Static);
        _centerProperty = _characterType.GetProperty("Center", BindingFlags.Public | BindingFlags.Instance);
        _characterNameProperty = _characterType.GetProperty("characterName", BindingFlags.Public | BindingFlags.Instance);
        _isLocalProperty = _characterType.GetProperty("IsLocal", BindingFlags.Public | BindingFlags.Instance);
        _refsProperty = _characterType.GetProperty("refs", BindingFlags.Public | BindingFlags.Instance);
        _dataProperty = _characterType.GetProperty("data", BindingFlags.Public | BindingFlags.Instance);
        _pingProperty = _characterType.GetProperty("Ping", BindingFlags.Public | BindingFlags.Instance);

        if (_refsProperty?.PropertyType != null)
        {
            _statsProperty = _refsProperty.PropertyType.GetProperty("stats", BindingFlags.Public | BindingFlags.Instance);
            _healthProperty = _refsProperty.PropertyType.GetProperty("health", BindingFlags.Public | BindingFlags.Instance);
        }

        if (_dataProperty?.PropertyType != null)
        {
            _currentStaminaProperty = _dataProperty.PropertyType.GetProperty("currentStamina", BindingFlags.Public | BindingFlags.Instance);
        }

        if (_statsProperty?.PropertyType != null)
        {
            _heightProperty = _statsProperty.PropertyType.GetProperty("heightInMeters", BindingFlags.Public | BindingFlags.Instance);
        }
    }

    public PlayerSnapshot? TryGetLocalPlayer()
    {
        var localCharacter = _localCharacterProperty?.GetValue(null, null);
        if (localCharacter == null)
        {
            return null;
        }

        return BuildSnapshot(localCharacter);
    }

    public List<PlayerSnapshot> GetPlayersAround(PlayerSnapshot localPlayer, float maxDistance)
    {
        var result = new List<PlayerSnapshot>();
        var allCharacters = _allCharactersProperty?.GetValue(null, null);
        if (allCharacters is not IEnumerable enumerable)
        {
            return result;
        }

        foreach (var character in enumerable)
        {
            if (character == null)
            {
                continue;
            }

            if (_isLocalProperty?.GetValue(character, null) is bool isLocal && isLocal)
            {
                continue;
            }

            var snapshot = BuildSnapshot(character);
            if (snapshot == null)
            {
                continue;
            }

            if (Vector3.Distance(localPlayer.WorldPosition, snapshot.WorldPosition) <= maxDistance)
            {
                result.Add(snapshot);
            }
        }

        return result;
    }

    private PlayerSnapshot? BuildSnapshot(object character)
    {
        if (_centerProperty?.GetValue(character, null) is not Vector3 center)
        {
            var transform = (character as Component)?.transform;
            if (transform == null)
            {
                return null;
            }

            center = transform.position;
        }

        var displayName = _characterNameProperty?.GetValue(character, null) as string ?? ((character as Component)?.name ?? "Unknown");
        var stamina = GetFloat(_currentStaminaProperty?.GetValue(_dataProperty?.GetValue(character, null), null));
        var health = GetFloat(_healthProperty?.GetValue(_refsProperty?.GetValue(character, null), null));
        var ping = GetInt(_pingProperty?.GetValue(character, null));

        return new PlayerSnapshot
        {
            PlayerId = character.GetHashCode(),
            DisplayName = displayName,
            Health = health,
            Stamina = stamina,
            Ping = ping,
            WorldPosition = center
        };
    }

    private static float GetFloat(object? value)
    {
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => 0f
        };
    }

    private static int GetInt(object? value)
    {
        return value switch
        {
            int i => i,
            short s => s,
            byte b => b,
            _ => 0
        };
    }
}
