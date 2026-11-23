using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController
{
    static readonly Dictionary<string,Sprite> emojiSprites=new Dictionary<string,Sprite>();
    Button[] benchNavigation;

    void BuildCornerNavigation()
    {
        benchNavigation=PracticalUiStyle.Navigation(run, session.PlayUiSound, ("Study", "book", () => ShowStudy(false)),
            ("Tasks", "tasks", () => OpenPage("Tasks")), ("Readings", "readings", ShowReadings),
            ("Workbook", "notebook", () => OpenPage("Workbook")), ("All practicals", "launcher", session.ReturnToLauncher),
            ("Pause", "pause", () => session.Pause(true)));
        benchNavigation[4].gameObject.SetActive(PracticalLauncher.Available);
    }
    void RefreshBenchNavigation()
    {
        bool hasCaptures = session.Simulation.onset.captured || session.Simulation.completion.captured || session.CompletedTrials.Count > 0;
        benchNavigation[1].gameObject.SetActive(!session.steps.ShowHints);
        benchNavigation[2].gameObject.SetActive(session.At("heat") || hasCaptures);
        benchNavigation[3].gameObject.SetActive(session.At("notebook") || hasCaptures);
    }
    void BuildCompactZoom()
    {
        zoomStrip=Rect("InspectionControls",run,new Vector2(.12f,.045f),new Vector2(BenchLeft-.012f,.108f));
        Button(zoomStrip,"Bench",Vector2.zero,Vector2.one,session.ReturnToBench,21);
    }

    // Code-drawn emoji-style sprites avoid missing system emoji glyphs on Android.
    internal static Sprite EmojiSprite(string kind)
    {
        Sprite existing;if(emojiSprites.TryGetValue(kind,out existing))return existing;
        var p=new IconPaint();var white=new Color(.96f,.99f,1);var blue=new Color(.28f,.70f,.90f);var paper=new Color(1,.95f,.79f);var dark=new Color(.13f,.25f,.32f);
        if(kind=="readings"){p.Box(16,12,84,87,dark,8);p.Box(23,19,77,80,paper,5);p.Line(37,26,37,72,blue,4);p.Line(53,26,53,72,blue,4);p.Line(26,53,74,53,blue,4);p.Line(26,38,74,38,blue,4);}
        else if(kind=="resume"){p.Triangle(new Vector2(32,16),new Vector2(81,50),new Vector2(32,84),white);}
        else if(kind=="tasks"||kind=="notebook")
        {
            p.Box(20,10,80,90,dark,8);p.Box(24,14,76,86,paper,6);
            if(kind=="tasks"){p.Box(36,81,64,96,new Color(.67f,.75f,.80f),5);for(int y=30;y<=68;y+=19){p.Box(31,y-4,39,y+4,blue,1);p.Line(47,y,67,y,blue,4);}}
            else {p.Box(20,14,30,86,blue,3);for(int y=30;y<=72;y+=14)p.Line(39,y,67,y,new Color(.54f,.64f,.67f),3);p.Line(58,21,88,57,new Color(1,.65f,.18f),8);p.Line(56,19,61,25,dark,5);}
        }
        else if(kind=="book")
        {p.Box(8,19,48,84,blue,6);p.Box(52,19,92,84,blue,6);p.Box(12,24,47,80,paper,3);p.Box(53,24,88,80,paper,3);p.Line(50,16,50,84,dark,4);for(int y=37;y<=65;y+=14){p.Line(20,y,39,y,blue,3);p.Line(61,y,80,y,blue,3);}}
        else if(kind=="launcher")
        {
            p.Box(11,11,89,89,dark,13);
            p.Box(20,20,46,46,blue,5);p.Box(54,20,80,46,white,5);
            p.Box(20,54,46,80,white,5);p.Box(54,54,80,80,blue,5);
        }
        else if(kind=="power"){p.Circle(50,47,28,white,6);p.Line(50,52,50,88,white,7);}
        else if(kind=="pause"){p.Box(25,19,42,81,white,5);p.Box(58,19,75,81,white,5);}
        else if(kind=="inspect"){p.Circle(42,60,24,white,7);p.Circle(42,60,19,new Color(.40f,.84f,1,.35f));p.Line(60,41,84,16,white,10);}
        else if(kind=="plus"||kind=="minus"){p.Line(22,50,78,50,white,8);if(kind=="plus")p.Line(50,22,50,78,white,8);}
        else if(kind=="reset"){p.Circle(50,48,28,white,6);p.Box(60,64,92,96,Color.clear);p.Triangle(new Vector2(61,91),new Vector2(61,64),new Vector2(85,70),white);}
        else if(kind=="centre"){p.Circle(50,50,24,white,4);p.Circle(50,50,6,new Color(1,.66f,.23f));p.Line(50,10,50,30,white,4);p.Line(50,70,50,90,white,4);p.Line(10,50,30,50,white,4);p.Line(70,50,90,50,white,4);}
        else if(kind=="bench"){p.Box(25,15,75,61,paper,4);p.Triangle(new Vector2(12,58),new Vector2(50,90),new Vector2(88,58),blue);p.Box(42,15,58,45,dark,2);}
        else if(kind=="view"){p.Line(15,50,85,50,white,6);p.Line(50,15,50,85,white,6);p.Line(15,50,30,63,white,6);p.Line(15,50,30,37,white,6);p.Line(85,50,70,63,white,6);p.Line(85,50,70,37,white,6);p.Line(50,85,37,70,white,6);p.Line(50,85,63,70,white,6);p.Line(50,15,37,30,white,6);p.Line(50,15,63,30,white,6);}
        else if(kind=="move"){p.Line(15,50,85,50,white,7);p.Line(15,50,32,68,white,7);p.Line(15,50,32,32,white,7);p.Line(85,50,68,68,white,7);p.Line(85,50,68,32,white,7);}
        else if(kind=="fire"){p.Triangle(new Vector2(18,18),new Vector2(59,93),new Vector2(82,18),new Color(1,.55f,.14f));p.Triangle(new Vector2(35,18),new Vector2(45,64),new Vector2(66,18),new Color(1,.91f,.35f));}
        else if(kind=="check"){p.Line(18,48,42,25,white,8);p.Line(42,25,85,77,white,8);}
        else if(kind=="close"){p.Line(24,24,76,76,white,7);p.Line(24,76,76,24,white,7);}
        else if(kind=="band")p.Circle(50,50,29,new Color(1,.74f,.33f),7);
        else {p.Line(50,83,50,22,white,7);p.Line(50,22,27,45,white,7);p.Line(50,22,73,45,white,7);}
        var texture=new Texture2D(100,100,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;texture.SetPixels(p.pixels);texture.Apply();
        var sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,100,100),new Vector2(.5f,.5f),100);emojiSprites[kind]=sprite;return sprite;
    }
    class IconPaint
    {
        public Color[] pixels=new Color[10000];
        void Paint(System.Func<Vector2,float> distance,Color color)
        {for(int y=0;y<100;y++)for(int x=0;x<100;x++){float a=Mathf.Clamp01(.5f-distance(new Vector2(x+.5f,y+.5f)));if(a<=0)continue;int i=y*100+x;var c=color;c.a*=a;float alpha=c.a+pixels[i].a*(1-c.a);if(alpha>0){var mix=(c*c.a+pixels[i]*pixels[i].a*(1-c.a))/alpha;mix.a=alpha;pixels[i]=mix;}}}
        public void Box(float x0,float y0,float x1,float y1,Color color,float radius=0)
        {var centre=new Vector2((x0+x1)/2,(y0+y1)/2);var half=new Vector2((x1-x0)/2-radius,(y1-y0)/2-radius);Paint(v=>{var d=new Vector2(Mathf.Abs(v.x-centre.x),Mathf.Abs(v.y-centre.y))-half;return new Vector2(Mathf.Max(d.x,0),Mathf.Max(d.y,0)).magnitude+Mathf.Min(Mathf.Max(d.x,d.y),0)-radius;},color);}
        public void Circle(float x,float y,float radius,Color color,float width=0)
        {Paint(v=>width>0?Mathf.Abs(Vector2.Distance(v,new Vector2(x,y))-radius)-width/2:Vector2.Distance(v,new Vector2(x,y))-radius,color);}
        public void Line(float x0,float y0,float x1,float y1,Color color,float width)
        {var a=new Vector2(x0,y0);var b=new Vector2(x1,y1);Paint(v=>Vector2.Distance(v,a+(b-a)*Mathf.Clamp01(Vector2.Dot(v-a,b-a)/(b-a).sqrMagnitude))-width/2,color);}
        public void Triangle(Vector2 a,Vector2 b,Vector2 c,Color color)
        {Paint(v=>{float x=Cross(b-a,v-a),y=Cross(c-b,v-b),z=Cross(a-c,v-c);return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0)?-1:1;},color);}
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    }
}
