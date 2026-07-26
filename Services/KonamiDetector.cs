using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

public class KonamiDetector
{
    private static readonly VirtualKey[] Sequence =
    [
        VirtualKey.UP, VirtualKey.UP, VirtualKey.DOWN, VirtualKey.DOWN,
        VirtualKey.LEFT, VirtualKey.RIGHT, VirtualKey.LEFT, VirtualKey.RIGHT,
    ];

    private static readonly VirtualKey[] MonitoredKeys =
        [VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT];

    private readonly IKeyState keyState;
    private readonly Dictionary<VirtualKey, bool> wasDown = new();
    private int step;
    private DateTime lastInputTime = DateTime.MinValue;

    public event Action? OnCompleted;

    public KonamiDetector(IKeyState keyState)
    {
        this.keyState = keyState;
    }

    public void Update(bool windowFocused)
    {
        if (step > 0 && (DateTime.Now - lastInputTime).TotalSeconds > 2.0)
            step = 0;

        if (!windowFocused)
        {
            foreach (var vk in MonitoredKeys)
                wasDown[vk] = keyState.IsVirtualKeyValid(vk) && keyState[vk];
            return;
        }

        var expected = Sequence[step];
        foreach (var vk in MonitoredKeys)
        {
            var down = keyState.IsVirtualKeyValid(vk) && keyState[vk];
            var prevDown = wasDown.TryGetValue(vk, out var p) && p;
            wasDown[vk] = down;

            if (!down || prevDown) continue;

            if (vk == expected)
            {
                step++;
                lastInputTime = DateTime.Now;
                if (step >= Sequence.Length)
                {
                    step = 0;
                    OnCompleted?.Invoke();
                }
            }
            else
            {
                step = vk == Sequence[0] ? 1 : 0;
                lastInputTime = DateTime.Now;
            }
        }
    }
}
