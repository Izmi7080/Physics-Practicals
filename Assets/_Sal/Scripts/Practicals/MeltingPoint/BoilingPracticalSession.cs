using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PracticalSelection { public static string ExperimentId=""; }

public partial class NaphthalenePracticalSession
{
    public bool IsBoiling { get { return Definition.workflow==PracticalWorkflow.Boiling; } }
    public Vector3 HeatControlPosition { get { return IsBoiling&&heaterIndicator!=null?heaterIndicator.position:flameVisual.position; } }
    public Transform fusionTube,solventLiquid,dropper,heaterIndicator,electricHeaterBody;
    readonly List<Material> boilingMaterials=new List<Material>();
    readonly List<Transform> bubbles=new List<Transform>();
    Material heaterMaterial;
    public PracticalItemBinding[] TrayItems { get { return IsBoiling?items.Where(b=>b.id!="sample"&&b.id!="pestle"&&b.id!="tripod"&&b.id!="gauze").ToArray():items; } }
    public void SelectExperiment(string id)
    {
        if(!InMenu)return;PracticalDefinitions.Load(id).ValidateForLaunch();
        PracticalSelection.ExperimentId=id;SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    void ConfigureExperimentGeometry()
    {
        foreach(var t in gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TextMesh>(true)))
            if(t.name=="NaphthaleneLabel")t.text=Definition.chemicalName;
        if(!IsBoiling)return;
        bandAttachedTarget.localPosition=new Vector3(.0075f,.065f,-.002f);rubberBand.localScale=new Vector3(.034f,.0018f,.020f);
        var thermometerBinding=items.Single(b=>b.id=="thermometer");thermometerBinding.homePosition=new Vector3(-.61f,.84f,3.24f);thermometerBinding.homeRotation=Quaternion.identity;thermometerBinding.item.transform.SetPositionAndRotation(thermometerBinding.homePosition,thermometerBinding.homeRotation);
        var cap=items.Single(b=>b.id=="capillary").item;
        var glass=cap.transform.Find("VisibleCapillary").GetComponent<MeshRenderer>().sharedMaterial;
        var tube=new GameObject("Fusion tube",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider),typeof(Draggable));
        tube.transform.SetParent(transform.parent,false);tube.transform.position=mortarCentre.position+new Vector3(.10f,.005f,-.02f);
        tube.GetComponent<MeshFilter>().sharedMesh=cap.transform.Find("VisibleCapillary").GetComponent<MeshFilter>().sharedMesh;
        tube.GetComponent<MeshRenderer>().sharedMaterial=glass;tube.transform.localScale=Vector3.one;
        // Keep the draggable root at unit scale; only its visible mesh is enlarged.
        var shell=new GameObject("Fusion glass",typeof(MeshFilter),typeof(MeshRenderer));shell.transform.SetParent(tube.transform,false);shell.transform.localScale=new Vector3(4,.05f/.07f,4);shell.GetComponent<MeshFilter>().sharedMesh=tube.GetComponent<MeshFilter>().sharedMesh;shell.GetComponent<MeshRenderer>().sharedMaterial=glass;tube.GetComponent<MeshRenderer>().enabled=false;
        var pick=tube.GetComponent<BoxCollider>();pick.center=new Vector3(0,.025f,0);pick.size=new Vector3(.030f,.064f,.030f);
        var d=tube.GetComponent<Draggable>();d.id="fusion";d.minimumHeight=.835f;d.liftHeight=.015f;
        fusionTube=tube.transform;items=items.Concat(new[]{new PracticalItemBinding{id="fusion",item=d,homePosition=tube.transform.position,homeRotation=Quaternion.identity}}).ToArray();
        Part("Closed fusion-tube base",PrimitiveType.Sphere,fusionTube,new Vector3(0,.0007f,0),new Vector3(.011f,.0015f,.011f),glass);
        var liquidMaterial=OwnMaterial(waterVisual.GetComponent<Renderer>().sharedMaterial,new Color(.72f,.88f,.91f,.23f));
        solventLiquid=Part("Liquid sample",PrimitiveType.Cylinder,tube.transform,new Vector3(0,.009f,0),new Vector3(.008f,.008f,.008f),liquidMaterial);
        var capTarget=NewZone("InvertCapillary","capillary",fusionTube,new Vector3(0,.042f,0),Quaternion.Euler(180,0,0));
        var holderTarget=NewZone("AttachFusion","fusion",thermometer,new Vector3(.013f,.017f,-.005f),Quaternion.identity);
        zones=zones.Concat(new[]{capTarget,holderTarget}).ToArray();
        var dark=OwnMaterial(waterVisual.GetComponent<Renderer>().sharedMaterial,new Color(.14f,.18f,.20f,1));
        dropper=new GameObject("Dropper").transform;dropper.SetParent(transform,false);dropper.position=new Vector3(-1.03f,.94f,3.27f);
        Part("Dropper bulb",PrimitiveType.Sphere,dropper,Vector3.zero,new Vector3(.018f,.025f,.018f),dark);
        Part("Dropper glass",PrimitiveType.Cylinder,dropper,new Vector3(0,-.035f,0),new Vector3(.002f,.027f,.002f),glass).SetParent(dropper,true);
        for(int i=0;i<14;i++)bubbles.Add(Part("Vapour bubble "+i,PrimitiveType.Sphere,fusionTube,new Vector3(0,.003f,0),Vector3.one*.0009f,liquidMaterial));
        var mortar=gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="MortarAndPestle_Obj");
        if(mortar!=null){foreach(var renderer in mortar.GetComponentsInChildren<Renderer>())renderer.enabled=false;foreach(var collider in mortar.GetComponentsInChildren<Collider>())collider.enabled=false;}
        foreach(var renderer in gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Renderer>(true)))
            if(renderer.name=="Burner"||renderer.name=="BunsenMain"||renderer.transform.parent?.name=="BunsenBurner")
            {renderer.enabled=false;foreach(var collider in renderer.GetComponents<Collider>())collider.enabled=false;}
        var body=OwnMaterial(waterVisual.GetComponent<Renderer>().sharedMaterial,new Color(.55f,.60f,.61f,1));
        Vector3 bath=zones.Single(z=>z.name=="PlaceBeaker").transform.position;
        electricHeaterBody=Part("Electric bath heater",PrimitiveType.Cube,transform,bath+new Vector3(0,-.035f,0),new Vector3(.19f,.05f,.18f),body);
        electricHeaterBody.gameObject.AddComponent<BoxCollider>();
        for(int i=0;i<4;i++)Part("Heater support",PrimitiveType.Cylinder,transform,bath+new Vector3(i%2==0?-.07f:.07f,-.145f,i<2?-.06f:.06f),new Vector3(.014f,.11f,.014f),dark);
        heaterMaterial=OwnMaterial(body,new Color(.20f,.23f,.25f,1));heaterIndicator=Part("Electric heating plate",PrimitiveType.Cylinder,transform,bath-Vector3.up*.007f,new Vector3(.13f,.003f,.13f),heaterMaterial);
        Part("Power control",PrimitiveType.Sphere,transform,bath+new Vector3(.045f,-.035f,-.095f),new Vector3(.025f,.025f,.015f),dark);
        // Glass preparation is separate from heating a volatile solvent.
        Vector3 sealingMouth=new Vector3(-.95f,.99f,3.35f);
        (burnerMouth!=null?burnerMouth:flameVisual).position=sealingMouth;
        Part("Empty-capillary sealing burner",PrimitiveType.Cylinder,transform,sealingMouth-Vector3.up*.066f,new Vector3(.025f,.065f,.025f),dark);
        Part("Sealing burner base",PrimitiveType.Cylinder,transform,sealingMouth-Vector3.up*.13f,new Vector3(.07f,.005f,.07f),dark);
        var seal=zones.Single(z=>z.name=="SealCapillary");seal.transform.position=sealingMouth+new Vector3(.006f,.027f,0);
    }
    SnapZone NewZone(string name,string id,Transform parent,Vector3 position,Quaternion rotation)
    {
        var zone=new GameObject(name).AddComponent<SnapZone>();zone.transform.SetParent(parent,false);zone.transform.localPosition=position;zone.transform.localRotation=rotation;zone.acceptsId=id;zone.snapRadius=.08f;
        // Small ghost shell has no selectable colliders.
        var target=id=="fusion"?fusionTube.Find("Fusion glass"):items.Single(b=>b.id=="capillary").item.transform.Find("VisibleCapillary");
        var ghost=new GameObject("Destination silhouette",typeof(MeshFilter),typeof(MeshRenderer));ghost.transform.SetParent(zone.transform,false);ghost.transform.localScale=target.localScale;ghost.GetComponent<MeshFilter>().sharedMesh=target.GetComponent<MeshFilter>().sharedMesh;ghost.GetComponent<MeshRenderer>().sharedMaterial=zones[0].highlight.GetComponentInChildren<Renderer>(true).sharedMaterial;zone.highlight=ghost;ghost.SetActive(false);return zone;
    }
    Transform Part(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=parent==transform?transform.InverseTransformPoint(position):position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go.transform;
    }
    Material OwnMaterial(Material source,Color color)
    {
        var material=new Material(source);material.color=color;
        if(color.a>=.99f){material.SetFloat("_Surface",0);material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_ZWrite",1);material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.renderQueue=2000;}
        boilingMaterials.Add(material);return material;
    }
    void ReleaseBoilingMaterials(){foreach(var material in boilingMaterials)if(material!=null)Destroy(material);}
    public void AddLiquidDrop()
    {
        if(!IsBoiling||!At("sample")||!CanInteract||!preparation.capillarySealed)return;
        workspace.TakeOut("fusion");StartCoroutine(DispenseDrop());
    }
    IEnumerator DispenseDrop()
    {
        Busy=true;var home=dropper.position;var rotation=dropper.rotation;var target=fusionTube.position+Vector3.up*.09f;
        for(float t=0;t<.45f;t+=Time.deltaTime){dropper.position=Vector3.Lerp(home,target,Mathf.Sin(t/.45f*Mathf.PI));yield return null;}
        dropper.SetPositionAndRotation(home,rotation);preparation.liquidFraction=Mathf.Min(.60f,preparation.liquidFraction+1f/12f);
        if(steps.ShowHints&&preparation.liquidFraction>=.32f&&preparation.liquidFraction<=.38f)AcceptLiquidInternal();
        Busy=false;Message="Fusion tube: "+Mathf.RoundToInt(preparation.liquidFraction*100)+"% full.";ConfigureInputs();RefreshReadiness();
    }
    public void AcceptLiquid()
    {if(IsBoiling&&CanInteract&&At("sample")&&preparation.liquidFraction>0){AcceptLiquidInternal();ConfigureInputs();RefreshReadiness();}}
    void AcceptLiquidInternal(){preparation.sample=true;workspace.TakeOut("fusion");}
    public void EmptyLiquid()
    {if(IsBoiling&&CanInteract&&At("sample")){preparation.liquidFraction=0;preparation.sample=false;}}
    bool PlaceBoilingApparatus(string id,string zone)
    {
        if(!IsBoiling)return false;
        if(id=="capillary"&&zone=="InvertCapillary")
        {preparation.capillaryInverted=true;lookup["capillary"].item.transform.SetParent(fusionTube,true);workspace.TakeOut("thermometer");}
        else if(id=="fusion"&&zone=="AttachFusion")
        {preparation.capillaryPlaced=true;fusionTube.SetParent(thermometer,true);QueueActionView("Attachment");}
        else return false;
        Message="Apparatus placed.";ConfigureInputs();RefreshReadiness();return true;
    }
    void ConfigureBoilingInputs()
    {
        if(At("setup")&&!preparation.beaker||!steps.ShowHints&&!preparation.beaker)Enable("beaker","PlaceBeaker");
        if((At("water")||!steps.ShowHints)&&!preparation.water)ConfigureWaterInputs();
        if((At("seal")||!steps.ShowHints)&&!preparation.capillarySealed)Enable("capillary","SealCapillary");
        if((At("invert")||!steps.ShowHints)&&preparation.sample&&preparation.capillarySealed&&!preparation.capillaryInverted)Enable("capillary","InvertCapillary");
        if((At("attach")||!steps.ShowHints)&&preparation.capillaryInverted&&!preparation.capillaryPlaced)Enable("fusion","AttachFusion");
        if((At("immerse")||!steps.ShowHints)&&TaskAvailable("immerse"))
        {
            if(!preparation.clamped)Enable("thermometer","ClampThermometer");
            else if(!preparation.immersed){var d=lookup["thermometer"].item;d.canDrag=true;d.movement=DragMovement.Vertical;d.minY=immersedRootY-.02f;d.maxY=immersedRootY+.16f;}
        }
        if((At("heat")||!steps.ShowHints)&&preparation.burnerStarted&&!preparation.stirrer&&!Simulation.HasRequiredObservations)Enable("stirrer","PlaceStirrer");
    }
    void RestoreBoilingApparatus()
    {
        preparation.tripod=preparation.gauze=true;
        if(preparation.sample)workspace.TakeOut("fusion");
        if(preparation.capillaryInverted){Restore("capillary","InvertCapillary",true);lookup["capillary"].item.transform.SetParent(fusionTube,true);}
        if(preparation.capillaryPlaced){workspace.TakeOut("thermometer");Restore("fusion","AttachFusion",true);fusionTube.SetParent(thermometer,true);}
    }
    void UpdateBoilingVisuals()
    {
        if(!IsBoiling)return;
        samplePortion.gameObject.SetActive(false);powderBed.gameObject.SetActive(false);sampleChunks.gameObject.SetActive(false);packedSolid.gameObject.SetActive(false);packedLiquid.gameObject.SetActive(false);
        lookup["tripod"].item.gameObject.SetActive(false);lookup["gauze"].item.gameObject.SetActive(false);
        float height=.05f*preparation.liquidFraction;
        solventLiquid.gameObject.SetActive(height>0);solventLiquid.localPosition=new Vector3(0,.001f+height/2,0);solventLiquid.localScale=new Vector3(.008f,Mathf.Max(.00001f,height/2),.008f);
        var boiling=Simulation as BoilingSimulation;float intensity=boiling==null?0:boiling.bubbleIntensity;
        for(int i=0;i<bubbles.Count;i++){bubbles[i].gameObject.SetActive(i<Mathf.CeilToInt(intensity*bubbles.Count));bubbles[i].localPosition=new Vector3(Mathf.Sin(i*2.4f)*.0012f,.002f+Mathf.Repeat(Time.time*.009f+i*.001f,Mathf.Max(.002f,height-.002f)),Mathf.Cos(i*2.4f)*.0012f);}
        heaterMaterial.color=Simulation.lit?new Color(.77f,.26f,.08f):new Color(.20f,.23f,.25f);
        flameVisual.gameObject.SetActive(Sealing);
    }
}
