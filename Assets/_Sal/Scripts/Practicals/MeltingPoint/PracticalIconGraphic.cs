using UnityEngine;
using UnityEngine.UI;

// Small vector icons stay legible without a font glyph or imported bitmap dependency.
public class PracticalIconGraphic : MaskableGraphic
{
    public string kind;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(kind=="inspect")
        {Ring(vh,new Vector2(.43f,.57f),.23f);Line(vh,new Vector2(.59f,.40f),new Vector2(.83f,.16f));}
        else if(kind=="fire")
        {Triangle(vh,new Vector2(.22f,.17f),new Vector2(.78f,.17f),new Vector2(.57f,.90f));Triangle(vh,new Vector2(.38f,.22f),new Vector2(.62f,.22f),new Vector2(.45f,.65f));}
        else if(kind=="move")
        {Line(vh,new Vector2(.15f,.5f),new Vector2(.85f,.5f));Line(vh,new Vector2(.15f,.5f),new Vector2(.32f,.68f));Line(vh,new Vector2(.15f,.5f),new Vector2(.32f,.32f));Line(vh,new Vector2(.85f,.5f),new Vector2(.68f,.68f));Line(vh,new Vector2(.85f,.5f),new Vector2(.68f,.32f));}
        else if(kind=="check")
        {Line(vh,new Vector2(.18f,.5f),new Vector2(.42f,.28f));Line(vh,new Vector2(.42f,.28f),new Vector2(.84f,.76f));}
        else if(kind=="close")
        {Line(vh,new Vector2(.25f,.25f),new Vector2(.75f,.75f));Line(vh,new Vector2(.25f,.75f),new Vector2(.75f,.25f));}
        else if(kind=="band")Ring(vh,new Vector2(.5f,.5f),.28f);
        else if(kind=="grind")
        {Ring(vh,new Vector2(.5f,.5f),.28f);Triangle(vh,new Vector2(.75f,.58f),new Vector2(.93f,.58f),new Vector2(.83f,.34f));}
        else
        {Line(vh,new Vector2(.5f,.83f),new Vector2(.5f,.22f));Line(vh,new Vector2(.5f,.22f),new Vector2(.27f,.45f));Line(vh,new Vector2(.5f,.22f),new Vector2(.73f,.45f));}
    }
    Vector2 Point(Vector2 p){var r=rectTransform.rect;return new Vector2(r.x+p.x*r.width,r.y+p.y*r.height);}
    void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c)
    {int i=vh.currentVertCount;vh.AddVert(Point(a),color,Vector2.zero);vh.AddVert(Point(b),color,Vector2.zero);vh.AddVert(Point(c),color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);}
    void Line(VertexHelper vh,Vector2 a,Vector2 b)
    {
        var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*.035f;
        Triangle(vh,a+n,b+n,b-n);Triangle(vh,a+n,b-n,a-n);
    }
    void Ring(VertexHelper vh,Vector2 centre,float radius)
    {for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;Line(vh,centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,centre+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}}
}
