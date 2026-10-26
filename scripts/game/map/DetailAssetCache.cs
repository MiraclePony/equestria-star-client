using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EquestriaStar.Game.Map;

public interface IDetailResourceLoader
{
    Error Request(string path);
    ResourceLoader.ThreadLoadStatus Status(string path);
    PackedScene? Get(string path);
}
public sealed class ThreadedDetailLoader : IDetailResourceLoader
{
    public Error Request(string path)=>ResourceLoader.Exists(path)?ResourceLoader.LoadThreadedRequest(path,"PackedScene"):Error.FileNotFound;
    public ResourceLoader.ThreadLoadStatus Status(string path)=>ResourceLoader.LoadThreadedGetStatus(path);
    public PackedScene? Get(string path)=>ResourceLoader.LoadThreadedGet(path) as PackedScene;
}

/// <summary>Shared resources only; never holds tile nodes or performs instantiation in a callback.</summary>
public sealed class DetailAssetCache(IDetailResourceLoader loader)
{
    private sealed class Entry {public PackedScene? Scene;public bool Started,Failed;public double LastUsed;public int Uses;}
    public static DetailAssetCache Shared {get;}=new(new ThreadedDetailLoader());
    private readonly Dictionary<string,Entry> _entries=new();
    private readonly Dictionary<ulong,HashSet<string>> _owners=new();
    public int Requests {get;private set;}
    public int CacheHits {get;private set;}
    public int Evictions {get;private set;}
    public int ResidentCount=>_entries.Values.Count(e=>e.Scene!=null);
    public int LoadingCount=>_entries.Values.Count(e=>e.Started && !e.Failed && e.Scene==null);
    public int FailedCount=>_entries.Values.Count(e=>e.Failed);
    public void Publish(ulong owner,IEnumerable<string> paths)=>_owners[owner]=paths.ToHashSet();
    public void Release(ulong owner)=>_owners.Remove(owner);
    public void ClearAtShutdown(){_owners.Clear();_entries.Clear();}
    public PackedScene? Peek(string path)=>_entries.GetValueOrDefault(path)?.Scene;
    public string State(string path)=>!_entries.TryGetValue(path,out var e)?"unloaded":e.Failed?"failed":e.Scene!=null?"ready":e.Started?"loading":"queued";
    public void Tick(double now,ZoomDetailSettings settings)
    {
        var wanted=_owners.Values.SelectMany(p=>p).Distinct().ToArray();var set=wanted.ToHashSet();
        foreach(var path in wanted)
        {
            if(!_entries.TryGetValue(path,out var entry)){entry=new Entry();_entries[path]=entry;}
            entry.LastUsed=now;
        }
        foreach(var entry in _entries.ToArray())
        {
            var e=entry.Value;
            if(!e.Started || e.Failed || e.Scene!=null)continue;
            var status=loader.Status(entry.Key);
            if(status==ResourceLoader.ThreadLoadStatus.Loaded){e.Scene=loader.Get(entry.Key);e.Failed=e.Scene==null;}
            else if(status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)e.Failed=true;
        }
        var active=LoadingCount;
        foreach(var path in wanted)
        {
            var e=_entries[path];if(e.Started || e.Failed)continue;
            if(active>=settings.MaxConcurrentLoads)break;
            e.Started=true;Requests++;
            if(loader.Request(path)!=Error.Ok)e.Failed=true;else active++;
        }
        foreach(var pair in _entries.Where(p=>!set.Contains(p.Key) && (p.Value.Scene!=null || p.Value.Failed)).OrderBy(p=>p.Value.LastUsed).ToArray())
            if(now-pair.Value.LastUsed>settings.CacheGraceSeconds || ResidentCount>settings.MaxCachedResources)
            {_entries.Remove(pair.Key);Evictions++;}
    }
    public void CountUse(IEnumerable<string> paths)
    {foreach(var path in paths)if(_entries.TryGetValue(path,out var entry)){if(entry.Uses>0)CacheHits++;entry.Uses++;}}
}
