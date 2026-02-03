# PeakStats

Этот репозиторий содержит пример мода **PeakStats** для игры **Peak**. Мод показывает статистику ближайших игроков рядом с ними в виде world-space UI. Также реализована совместимость с модом [Selective-HUD-Hider](https://github.com/Toderonss/Selective-HUD-Hider) — если он скрывает HUD, PeakStats тоже скрывается (опционально).

## Что внутри

- `PeakStatsPlugin.cs` — точка входа BepInEx и конфиги.
- `PlayerStatsOverlay.cs` — логика поиска игроков, обновления и отрисовки виджетов.
- `SelectiveHudHiderCompat.cs` — soft-интеграция с Selective-HUD-Hider через reflection.
- `PeakStats.csproj` — пример проекта (пути к DLL нужно поправить под вашу установку).

## Как это работает

1. Плагин создаёт объект `PeakStatsOverlay` и добавляет компонент `PlayerStatsOverlay`.
2. Каждые `RefreshInterval` секунд опрашиваются игроки в радиусе `MaxDistance`.
3. Для каждого игрока создаётся `PlayerStatsView` (Canvas + TextMeshPro), позиция обновляется в world-space рядом с игроком.
4. Совместимость с Selective-HUD-Hider:
   - если найдён API типа `SelectiveHUDHider.API` или `SelectiveHudHider.API` — используется метод `IsHudVisible` или свойство `HudVisible`.
   - HUD PeakStats скрывается, когда внешний мод просит скрыть HUD (можно переопределить через конфиг `ShowWhenHudHidden`).

## Куда подключить реальные данные

Файл `PlayerStatsOverlay.cs` содержит секцию `FakeGameApi`. Сейчас она возвращает `null` и пустой список — это заглушки. Ваша задача — заменить эти методы на обращения к реальному API игры:

```csharp
internal static class FakeGameApi
{
    public static PlayerSnapshot? TryGetLocalPlayer()
    {
        // TODO: вернуть локального игрока
    }

    public static List<PlayerSnapshot> GetPlayersAround(PlayerSnapshot localPlayer, float maxDistance)
    {
        // TODO: вернуть список игроков рядом
    }
}
```

Заполните `PlayerSnapshot` реальными данными (HP, stamina, ping, позиция, имя).

## Сборка

1. Скопируйте DLL из игры/модов в папку `lib` рядом с `.csproj`.
2. Проверьте, что пути к DLL в `PeakStats.csproj` корректны.
3. Соберите проект (например, `dotnet build`).

## Конфиг

В BepInEx конфиге создаются параметры:

- `MaxDistance` — радиус поиска игроков.
- `RefreshInterval` — частота обновления.
- `ToggleKey` — горячая клавиша включения/выключения оверлея.
- `ShowWhenHudHidden` — показывать ли PeakStats, если внешний HUD скрыт.

## Совместимость с Selective-HUD-Hider

PeakStats не требует прямой зависимости. Если мод установлен, он автоматически будет учитываться. Если его нет — PeakStats работает сам по себе.
