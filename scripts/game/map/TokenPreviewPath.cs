using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EquestriaStar.Game.Map;

/// <summary>Local visual avoidance for an already authorized map edge. No server path/topology edits.</summary>
public static class TokenPreviewPath
{
    public static List<Vector3> Plan(MeshClearanceQuery query,Vector3 start,Vector3 end,float radius)
    {
        bool Clear(Vector3 a,Vector3 b)
        {
            var steps=Math.Max(1,(int)Math.Ceiling(a.DistanceTo(b)/(radius*.09f)));
            for(var i=0;i<=steps;i++)if(!query.IsFree(a.Lerp(b,i/(float)steps),radius,roof:true))return false;
            return true;
        }
        if(Clear(start,end))return [start,end];
        var step=radius*.16f;
        var origin=new Vector3(Math.Min(start.X,end.X)-radius*1.35f,0,Math.Min(start.Z,end.Z)-radius*1.35f);
        var width=(int)Math.Ceiling((Math.Abs(start.X-end.X)+radius*2.7f)/step)+1;
        var height=(int)Math.Ceiling((Math.Abs(start.Z-end.Z)+radius*2.7f)/step)+1;
        (int X,int Z) Key(Vector3 p)=>((int)Mathf.Round((p.X-origin.X)/step),(int)Mathf.Round((p.Z-origin.Z)/step));
        Vector3 Point((int X,int Z) p)=>new(origin.X+p.X*step,0,origin.Z+p.Z*step);
        var first=Key(start);
        var queue=new PriorityQueue<(int X,int Z),float>();
        var cost=new Dictionary<(int X,int Z),float>();
        var previous=new Dictionary<(int X,int Z),(int X,int Z)>();
        var free=new Dictionary<(int X,int Z),bool>();
        // Exact landing anchors are virtual endpoints: a rounded grid point can lie in a wall
        // even when the actual anchor is clear. Connect to nearby visible free grid points.
        for(var x=-3;x<=3;x++)for(var z=-3;z<=3;z++)
        {
            var key=(X:first.X+x,Z:first.Z+z);var p=Point(key);
            if(key.X<0 || key.Z<0 || key.X>=width || key.Z>=height || !Clear(start,p))continue;
            var value=start.DistanceTo(p)/step;
            cost[key]=value;queue.Enqueue(key,value+p.DistanceTo(end)/step);
        }
        (int X,int Z) last=default;
        var found=false;
        while(queue.Count>0)
        {
            var current=queue.Dequeue();
            if(Point(current).DistanceTo(end)<=step*4 && Clear(Point(current),end)){last=current;found=true;break;}
            for(var x=-1;x<=1;x++)for(var z=-1;z<=1;z++)
            {
                if(x==0 && z==0)continue;
                var next=(X:current.X+x,Z:current.Z+z);
                if(next.X<0 || next.Z<0 || next.X>=width || next.Z>=height)continue;
                if(!free.TryGetValue(next,out var safe)){safe=query.IsFree(Point(next),radius);free[next]=safe;}
                if(!safe || !Clear(Point(current),Point(next)))continue;
                var value=cost[current]+Mathf.Sqrt(x*x+z*z);
                if(cost.TryGetValue(next,out var old) && value>=old)continue;
                cost[next]=value;previous[next]=current;
                queue.Enqueue(next,value+Point(next).DistanceTo(end)/step);
            }
        }
        if(!found)return [];
        var keys=new List<(int X,int Z)>{last};
        while(previous.TryGetValue(keys[^1],out var prior))keys.Add(prior);
        keys.Reverse();var raw=keys.Select(Point).ToList();raw.Insert(0,start);raw.Add(end);
        var result=new List<Vector3>{start};var index=0;
        while(index<raw.Count-1)
        {
            var target=raw.Count-1;while(target>index+1 && !Clear(raw[index],raw[target]))target--;
            result.Add(raw[target]);index=target;
        }
        return result;
    }
    public static Vector3 Sample(IReadOnlyList<Vector3> points,float progress)
    {
        if(points.Count==0)return Vector3.Zero;
        var total=0f;for(var i=1;i<points.Count;i++)total+=points[i-1].DistanceTo(points[i]);
        var remaining=total*Mathf.Clamp(progress,0,1);
        for(var i=1;i<points.Count;i++)
        {
            var length=points[i-1].DistanceTo(points[i]);
            if(remaining<=length)return points[i-1].Lerp(points[i],remaining/Math.Max(.0001f,length));
            remaining-=length;
        }
        return points[^1];
    }
}
