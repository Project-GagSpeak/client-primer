using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using GagSpeak.ResourceManager.Loaders;
using GagSpeak.ResourceManager.VfxStructs;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using OtterGui;

namespace GagSpeak.ResourceManager.ResourceSpawn;

// Grabbed from VFXEditor
public enum SpawnType { None, Self, Target }

public class VfxSpawnItem
{
    public readonly string Path;
    public readonly SpawnType Type;
    public readonly bool CanLoop;

    public VfxSpawnItem(string path, SpawnType type, bool canLoop)
    {
        Path = path;
        Type = type;
        CanLoop = canLoop;
    }
}

public class VfxLoopItem
{
    public VfxSpawnItem Item;
    public DateTime RemovedTime;

    public VfxLoopItem(VfxSpawnItem item, DateTime removedTime)
    {
        Item = item;
        RemovedTime = removedTime;
    }
}

public unsafe class VfxSpawns : DisposableMediatorSubscriberBase
{
    private readonly ResourceLoader _resourceLoader;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targets;

    public VfxSpawns(ILogger<VfxSpawns> logger,
        GagspeakMediator mediator, ResourceLoader resourceLoader,
        IClientState clientState, ITargetManager targets) : base(logger, mediator)
    {
        _resourceLoader = resourceLoader;

        _clientState = clientState;
        _targets = targets;
        _resourceLoader = resourceLoader;

        // Subscribe to the VfxActorRemoved event
        Mediator.Subscribe<VfxActorRemoved>(this, (msg) => InteropRemoved(msg.data));
    }

    // Currently loaded VfxSpawnItems, and if they should be looping.
    public readonly Dictionary<ActorVfx, VfxSpawnItem> Vfxs = [];
    public readonly List<VfxLoopItem> ToLoop = [];

    public bool IsActive => Vfxs.Count > 0;

    public void DrawVfxRemove()
    {
        if (ImGui.Button("Remove VFX")) Clear();
    }

    public void DrawVfxSpawnOptions(string path, bool loop, int labelID = 1)
    {
        if (ImGui.Button($"Spawn on Self##{labelID}SpawnSelf{path}")) OnSelf(path, loop);
        if (ImGui.Button($"Spawn on Target##{labelID}SpawnTarget{path}")) OnTarget(path, loop);
    }

    public void OnSelf(string path, bool canLoop)
    {
        IGameObject? playerObject = _clientState?.LocalPlayer;
        if (playerObject == null) return;

        try
        {
            // attmept to fetch the replacement file from the input path, if we cannot, throw an exception.
            if(path.EndsWith(".avfx"))
            {
                if(!AvfxManager.PathMappings.TryGetValue(path, out var newVfxPath))
                {
                    throw new Exception("Failed to find replacement path for VFX");
                }
            }
            else if(path.EndsWith(".scd"))
            {
                if(!ScdManager.PathMappings.TryGetValue(path, out var newScdPath))
                {
                    throw new Exception("Failed to find replacement path for VFX");
                }
            }
            else if (!path.EndsWith(".avfx") && !path.EndsWith(".scd"))
            {
                throw new Exception("Invalid Path");
            }

            // construct the actorVfx from the given objects and paths.
            VfxStruct* createdVfx = (VfxStruct*)_resourceLoader.ActorVfxCreate
                (path, playerObject.Address, playerObject.Address, -1, (char)0, 0, (char)0);

            // create the actor for it & add it.
            Vfxs.Add(new ActorVfx(createdVfx, path), new(path, SpawnType.Self, canLoop));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to spawn VFX");
        }
    }

    public void OnTarget(string path, bool canLoop)
    {
        IGameObject? targetObject = _targets?.Target;
        if (targetObject == null) return;

        try
        {
            // attmept to fetch the replacement file from the input path, if we cannot, throw an exception.
            if (path.EndsWith(".avfx"))
            {
                if (!AvfxManager.PathMappings.TryGetValue(path, out var newVfxPath))
                {
                    throw new Exception("Failed to find replacement path for VFX");
                }
            }
            else if (path.EndsWith(".scd"))
            {
                if (!ScdManager.PathMappings.TryGetValue(path, out var newScdPath))
                {
                    throw new Exception("Failed to find replacement path for VFX");
                }
            }
            else if(!path.EndsWith(".avfx") && !path.EndsWith(".scd"))
            {
                throw new Exception("Invalid Path");
            }

            // construct the actorVfx from the given objects and paths.
            VfxStruct* createdVfx = (VfxStruct*)_resourceLoader.ActorVfxCreate
                (path, targetObject.Address, targetObject.Address, -1, (char)0, 0, (char)0);

            // create the actor for it & add it.
            Vfxs.Add(new ActorVfx(createdVfx, path), new(path, SpawnType.Target, canLoop));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to spawn VFX");
        }
    }

    // Action to perform on each tick
    public void Tick()
    {
        // Check to see if any stored Vfx's Looped
        var justLooped = new List<VfxLoopItem>();
        foreach (var loop in ToLoop)
        {
            // If the time since the Vfx was removed is less than 0.1 seconds, skip it
            if ((DateTime.Now - loop.RemovedTime).TotalSeconds < 0.1f) continue;

            // Add it to the loop,
            justLooped.Add(loop);

            // And spawn it again
            if (loop.Item.Type == SpawnType.Self) OnSelf(loop.Item.Path, true);
            else if (loop.Item.Type == SpawnType.Target) OnTarget(loop.Item.Path, true);
        }

        // Remove the Vfx's that just looped
        ToLoop.RemoveAll(justLooped.Contains);
    }

    public void Clear()
    {
        foreach (var vfx in Vfxs)
        {
            // calls the actual remove function sig, which then calls the interop removed.
            if(vfx.Key == null) continue;

            _resourceLoader.ActorVfxRemove((IntPtr)vfx.Key.Vfx, (char)1);
        }
        Vfxs.Clear();
        ToLoop.Clear();
    }

    public void InteropRemoved(IntPtr data)
    {
        if (!GetVfx(data, out var vfx)) return;
        var item = Vfxs[vfx];

        // Do not include looping vfx spawns, we do that ourselves. (was here before)

        // Remove the vfx
        Vfxs.Remove(vfx); // Simply removes it from the list, not calls the remove actorVfx
    }

    public bool GetVfx(IntPtr data, out ActorVfx vfx)
    {
        vfx = null!;
        if (data == IntPtr.Zero || Vfxs.Count == 0) return false;
        return Vfxs.Keys.FindFirst(x => data == (IntPtr)x.Vfx, out vfx!);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Clear();
    }
}