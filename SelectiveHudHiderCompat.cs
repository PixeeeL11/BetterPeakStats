using System;
using System.Reflection;

namespace PeakStats;

public sealed class SelectiveHudHiderCompat
{
    private bool _initialized;
    private Func<bool>? _visibilityDelegate;

    public void TryInitialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _visibilityDelegate = ResolveVisibilityDelegate();
    }

    public bool ShouldShowHud()
    {
        return _visibilityDelegate?.Invoke() ?? true;
    }

    private Func<bool>? ResolveVisibilityDelegate()
    {
        var type = Type.GetType("SelectiveHUDHider.API, SelectiveHUDHider")
                   ?? Type.GetType("SelectiveHudHider.API, SelectiveHudHider");
        if (type == null)
        {
            return null;
        }

        var method = type.GetMethod("IsHudVisible", BindingFlags.Public | BindingFlags.Static);
        if (method != null)
        {
            return () => InvokeBoolMethod(method, "PeakStats");
        }

        var property = type.GetProperty("HudVisible", BindingFlags.Public | BindingFlags.Static);
        if (property != null)
        {
            return () => (bool)property.GetValue(null, null)!;
        }

        return null;
    }

    private static bool InvokeBoolMethod(MethodInfo method, string hudId)
    {
        try
        {
            var result = method.GetParameters().Length switch
            {
                0 => method.Invoke(null, null),
                1 => method.Invoke(null, new object[] { hudId }),
                _ => null
            };

            return result is bool visible && visible;
        }
        catch (Exception)
        {
            return true;
        }
    }
}