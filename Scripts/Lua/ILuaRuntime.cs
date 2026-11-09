using System.Collections.Generic;

// The seam the design doc requires: "Lua is embedded behind an ILuaRuntime
// interface... a native binding can replace it later if profiling demands."
//
// The object parameter is the runtime's opaque function handle (a boxed
// DynValue under MoonSharp) - the interface itself stays implementation
// agnostic so the swap the doc describes is a real possibility.
public interface ILuaRuntime {

    string ModId { get; }

    // Executes a file relative to the mod's root folder.
    void RunFile(string relativePath);

    // Invokes a Lua event handler with an engine-built context table.
    // ctx.Cancel() inside Lua propagates to the C# EventContext directly.
    void RaiseEvent(object luaHandler, EventContext ctx);
}
