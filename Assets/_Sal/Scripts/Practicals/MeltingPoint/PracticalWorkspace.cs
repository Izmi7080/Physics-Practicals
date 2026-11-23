using System;
using System.Collections.Generic;
using UnityEngine;

// Scene geometry belongs here, rather than in the touch controller or chemical definition.
public class PracticalWorkspace : MonoBehaviour
{
    [Serializable] public class Station { public string id; public Transform point; }
    public List<Station> stations = new List<Station>();
    public Transform movementOrigin;
    public Vector3 movementMinimum = new Vector3(-.82f,0,-.46f);
    public Vector3 movementMaximum = new Vector3(.82f,1,.36f);
    readonly HashSet<string> stored = new HashSet<string>();
    NaphthalenePracticalSession session;
    public void Initialize(NaphthalenePracticalSession owner) { session=owner; }
    public Transform StationPoint(string id) { var s=stations.Find(x=>x.id==id);return s==null?null:s.point; }
    public bool IsStored(string id) { return stored.Contains(id); }
    public void ResetTray()
    {
        stored.Clear();
        foreach(var b in session.items) { stored.Add(b.id);b.item.gameObject.SetActive(false); }
    }
    // Retrieval is independent of permission to perform a procedure action.
    public bool TakeOut(string id)
    {
        var b=Array.Find(session.items,x=>x.id==id);
        if(b==null)return false;
        stored.Remove(id);b.item.gameObject.SetActive(true);return true;
    }
    // Keep upcoming apparatus offstage; offer real objects on the bench as they become usable.
    public void OfferAvailable()
    {
        foreach(var b in session.items)if(b.item.canDrag)TakeOut(b.id);
        if(session.IsBoiling&&session.At("sample"))TakeOut("fusion");
    }
    public Vector3 Constrain(Draggable item,Vector3 target)
    {
        if(movementOrigin==null)return target;
        var p=movementOrigin.InverseTransformPoint(target);
        p.x=Mathf.Clamp(p.x,movementMinimum.x,movementMaximum.x);
        p.y=Mathf.Clamp(p.y,movementMinimum.y,movementMaximum.y);
        p.z=Mathf.Clamp(p.z,movementMinimum.z,movementMaximum.z);
        target=movementOrigin.TransformPoint(p);target.y=Mathf.Max(item.minimumHeight,target.y);return target;
    }
    public static string Label(string id)
    {
        switch(id) {
            case "fusion":return "Fusion tube";case "tripod":return "Tripod"; case "gauze":return "Wire gauze";case "beaker":return "Beaker";
            case "sample":return "Sample";case "pestle":return "Pestle";case "capillary":return "Capillary";
            case "thermometer":return "Thermometer";case "stirrer":return "Stirrer";default:return id;
        }
    }
}
