using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakStats;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class PeakStatsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.betterpeakstats.peakstats";
    public const string PluginName = "PeakStats";
    public const string PluginVersion = "0.1.0";

    internal static PeakStatsPlugin Instance { get; private set; } = null!;

    internal ConfigEntry<float> MaxDistance { get; private set; } = null!;
    internal ConfigEntry<float> RefreshInterval { get; private set; } = null!;
    internal ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;
    internal ConfigEntry<bool> ShowWhenHudHidden { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;

        MaxDistance = Config.Bind("General", "MaxDistance", 40f, "Максимальная дистанция для отображения статистики рядом.");
        RefreshInterval = Config.Bind("General", "RefreshInterval", 0.2f, "Интервал обновления данных (сек)." );
        ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.F9, "Клавиша для включения/выключения модального оверлея.");
        ShowWhenHudHidden = Config.Bind("SelectiveHudHider", "ShowWhenHudHidden", false, "Показывать ли статистику, если сторонний мод скрывает HUD.");

        var overlayRoot = new GameObject("PeakStatsOverlay");
        DontDestroyOnLoad(overlayRoot);
        overlayRoot.AddComponent<PlayerStatsOverlay>();

        Logger.LogInfo("PeakStats загружен.");
    }
}
