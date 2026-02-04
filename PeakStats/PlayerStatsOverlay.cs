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
        _text.textWrappingMode = TextWrappingModes.NoWrap;
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
    public int PlayerId { get; set; }
    public string DisplayName { get; set; } = "Unknown";
    public float Health { get; set; }
    public float Stamina { get; set; }
    public int Ping { get; set; }
    public Vector3 WorldPosition { get; set; }
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
    private readonly PropertyInfo? _centerProperty;
    private readonly PropertyInfo? _characterNameProperty;
    private readonly PropertyInfo? _isLocalProperty;
    private readonly FieldInfo? _localCharacterField;
    private readonly FieldInfo? _allCharactersField;
    private readonly FieldInfo? _dataField;
    private readonly FieldInfo? _refsField;
    private readonly FieldInfo? _afflictionsField;
    private readonly PropertyInfo? _currentStaminaProperty;
    private readonly PropertyInfo? _pingProperty;
    private readonly MethodInfo? _getCurrentStatusMethod;
    private readonly object? _injuryStatusValue;

    public ReflectionCache()
    {
        _characterType = Type.GetType("Character, Assembly-CSharp");
        if (_characterType == null)
        {
            return;
        }

        _localCharacterField = _characterType.GetField("localCharacter", BindingFlags.Public | BindingFlags.Static);
        _allCharactersField = _characterType.GetField("AllCharacters", BindingFlags.Public | BindingFlags.Static);
        _centerProperty = _characterType.GetProperty("Center", BindingFlags.Public | BindingFlags.Instance);
        _characterNameProperty = _characterType.GetProperty("characterName", BindingFlags.Public | BindingFlags.Instance);
        _isLocalProperty = _characterType.GetProperty("IsLocal", BindingFlags.Public | BindingFlags.Instance);
        _dataField = _characterType.GetField("data", BindingFlags.Public | BindingFlags.Instance);
        _refsField = _characterType.GetField("refs", BindingFlags.Public | BindingFlags.Instance);
        _pingProperty = _characterType.GetProperty("Ping", BindingFlags.Public | BindingFlags.Instance);

        var dataType = _dataField?.FieldType;
        if (dataType != null)
        {
            _currentStaminaProperty = dataType.GetProperty("currentStamina", BindingFlags.Public | BindingFlags.Instance);
        }

        var refsType = _refsField?.FieldType;
        if (refsType != null)
        {
            _afflictionsField = refsType.GetField("afflictions", BindingFlags.Public | BindingFlags.Instance);
        }

        var afflictionsType = _afflictionsField?.FieldType;
        if (afflictionsType != null)
        {
            _getCurrentStatusMethod = afflictionsType.GetMethod("GetCurrentStatus", BindingFlags.Public | BindingFlags.Instance);
            var statusEnumType = afflictionsType.GetNestedType("STATUSTYPE", BindingFlags.Public);
            if (statusEnumType != null)
            {
                _injuryStatusValue = Enum.Parse(statusEnumType, "Injury");
            }
        }
    }

    public PlayerSnapshot? TryGetLocalPlayer()
    {
        var localCharacter = _localCharacterField?.GetValue(null);
        if (localCharacter == null)
        {
            return null;
        }

        return BuildSnapshot(localCharacter);
    }

    public List<PlayerSnapshot> GetPlayersAround(PlayerSnapshot localPlayer, float maxDistance)
    {
        var result = new List<PlayerSnapshot>();
        var allCharacters = _allCharactersField?.GetValue(null);
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
        var data = _dataField?.GetValue(character);
        var stamina = GetFloat(_currentStaminaProperty?.GetValue(data, null));
        var health = GetHealth(character);
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

    private float GetHealth(object character)
    {
        if (_getCurrentStatusMethod == null || _injuryStatusValue == null)
        {
            return 0f;
        }

        var refs = _refsField?.GetValue(character);
        var afflictions = _afflictionsField?.GetValue(refs);
        if (afflictions == null)
        {
            return 0f;
        }

        var injury = GetFloat(_getCurrentStatusMethod.Invoke(afflictions, new[] { _injuryStatusValue }));
        var healthPercent = Mathf.Clamp01(1f - injury) * 100f;
        return healthPercent;
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
