using Godot;
using System;
using System.Collections.Generic;

// The event bus (design doc: "Events with priority and cancel:
// events.on('item.use', handler, { priority = 0 })").
//
// This is now the C# core under the Lua-facing api.events; Lua handlers wrap
// these same delegates through MoonSharpRuntime. Semantics:
//   - HIGHER priority runs EARLIER (first crack at Cancel()).
//   - Ties keep registration order.
//   - A handler that throws is logged with its owner (the mod id) and
//     disabled after MaxStrikes failures - the game keeps running (doc's
//     resilience rule; it is also the containment until instruction budgets
//     exist, see MoonSharpRuntime).
//   - Cancel() on the context vetoes the action; the emitter checks it.
//
// Current engine-fired events (data keys in parentheses):
//   item.equipping  (cancellable) - before a slot swap (item, slot)
//   item.equipped   (after)       - after a slot swap (item, slot)
//   item.use        (cancellable) - attack pressed (item)
//   block.mine      (cancellable) - before breaking (position, normal)
//   block.place     (cancellable) - before placing (position, normal, block)
//   entity.hurt     (cancellable) - before damage lands (entity, damage, health, item)
//   entity.died     (after)       - a killing blow landed (entity, item)
public sealed class EventContext {

    public string Name { get; }
    public Dictionary<string, object> Data { get; }
    public bool Cancelled { get; private set; }

    public void Cancel() => Cancelled = true;

    internal EventContext(string name, Dictionary<string, object> data) {
        Name = name;
        Data = data;
    }
}

public static class Events {

    public delegate void Handler(EventContext ctx);

    private sealed class Subscriber {
        public Handler Fn;
        public int Priority;
        public string Owner;
        public int Strikes;
    }

    private const int MaxStrikes = 3;

    private static readonly Dictionary<string, List<Subscriber>> subscribers = new();

    public static void On(string name, Handler handler, int priority = 0, string owner = "csharp") {
        if (handler == null) return;

        if (!subscribers.TryGetValue(name, out List<Subscriber> list)) {
            list = new List<Subscriber>();
            subscribers[name] = list;
        }

        // Insert before the first lower-priority subscriber: stable order,
        // highest priority first.
        int insert = list.FindIndex(s => s.Priority < priority);
        if (insert < 0) list.Add(new Subscriber { Fn = handler, Priority = priority, Owner = owner });
        else list.Insert(insert, new Subscriber { Fn = handler, Priority = priority, Owner = owner });
    }

    // Note: lambdas can't be removed (no reference to keep) - store the
    // delegate in a field if you need Off().
    public static void Off(string name, Handler handler) {
        if (!subscribers.TryGetValue(name, out List<Subscriber> list)) return;
        list.RemoveAll(s => s.Fn == handler);
    }

    public static EventContext Emit(string name, Dictionary<string, object> data = null) {
        EventContext ctx = new(name, data ?? new Dictionary<string, object>());
        if (!subscribers.TryGetValue(name, out List<Subscriber> list)) return ctx;

        // Snapshot: handlers may subscribe/unsubscribe during dispatch.
        Subscriber[] snapshot = list.ToArray();
        bool disabledAny = false;

        foreach (Subscriber s in snapshot) {
            try {
                s.Fn(ctx);
            } catch (Exception e) {
                s.Strikes++;
                GD.PushError($"[events] {s.Owner} handler for '{name}' failed " +
                             $"({s.Strikes}/{MaxStrikes}): {e.Message}");
                if (s.Strikes >= MaxStrikes) disabledAny = true;
            }
        }

        if (disabledAny) {
            int removed = list.RemoveAll(s => s.Strikes >= MaxStrikes);
            if (removed > 0) {
                GD.PushWarning($"[events] disabled {removed} failing handler(s) for '{name}'");
            }
        }

        return ctx;
    }
}
