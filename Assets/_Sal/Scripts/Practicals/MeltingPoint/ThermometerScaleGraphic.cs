using UnityEngine;
using UnityEngine.UI;

// UI instrument, not a second game camera. Snapshot textures include numbered graduations.
public class ThermometerScaleGraphic : MaskableGraphic
{
    public float reading = 25, minimum = 0, maximum = 100;
    public void SetReading(float value) { reading = value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect r=rectTransform.rect;
        Box(vh,r,0,0,1,1,new Color(.96f,.97f,.95f));
        Box(vh,r,.21f,.07f,.33f,.94f,new Color(.78f,.81f,.8f));
        float top=Mathf.Lerp(.08f,.93f,Mathf.InverseLerp(minimum,maximum,reading));
        Box(vh,r,.24f,.08f,.30f,top,new Color(.78f,.13f,.2f));
        for(int n=Mathf.CeilToInt(minimum);n<=maximum;n++)
        {
            float y=Mathf.Lerp(.08f,.93f,(n-minimum)/(maximum-minimum));
            Box(vh,r,.36f,y,n%5==0?.66f:.52f,y+.002f,new Color(.15f,.20f,.23f));
        }
    }
    static void Box(VertexHelper vh,Rect r,float x0,float y0,float x1,float y1,Color c)
    {
        int i=vh.currentVertCount;
        vh.AddVert(new Vector3(r.x+x0*r.width,r.y+y0*r.height),c,Vector2.zero);
        vh.AddVert(new Vector3(r.x+x0*r.width,r.y+y1*r.height),c,Vector2.zero);
        vh.AddVert(new Vector3(r.x+x1*r.width,r.y+y1*r.height),c,Vector2.zero);
        vh.AddVert(new Vector3(r.x+x1*r.width,r.y+y0*r.height),c,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    public static Texture2D Snapshot(float value)
    {
        const int w=240,h=500;
        var pixels=new Color32[w*h]; var bg=new Color32(244,248,244,255);
        for(int i=0;i<pixels.Length;i++)pixels[i]=bg;
        float low=Mathf.Clamp(Mathf.Floor(value/10)*10-5,0,80),high=low+20;
        var dark=new Color32(29,43,48,255);var red=new Color32(196,35,49,255);
        Draw(pixels,w,h,43,34,73,469,new Color32(190,202,200,255));
        Draw(pixels,w,h,51,36,65,36+Mathf.RoundToInt(Mathf.InverseLerp(low,high,value)*430),red);
        for(int n=(int)low;n<=high;n++)
        {
            int y=36+Mathf.RoundToInt((n-low)/(high-low)*430);
            Draw(pixels,w,h,80,y,n%5==0?145:119,y+2,dark);
            if(n%5==0)Digits(pixels,w,h,n.ToString(),154,y-10,dark);
        }
        // The analogue divisions cannot show a decimal exactly: save the captured
        // numeric value in the image as well as the observation's serialized data.
        Digits(pixels,w,h,value.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),80,477,dark);
        Draw(pixels,w,h,160,488,166,494,dark);Draw(pixels,w,h,171,477,175,497,dark);
        Draw(pixels,w,h,175,477,187,481,dark);Draw(pixels,w,h,175,493,187,497,dark);
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
        return texture;
    }
    static void Draw(Color32[] p,int w,int h,int x0,int y0,int x1,int y1,Color32 c)
    {for(int y=Mathf.Max(0,y0);y<Mathf.Min(h,y1);y++)for(int x=Mathf.Max(0,x0);x<Mathf.Min(w,x1);x++)p[y*w+x]=c;}
    static readonly string[] Font={"111101101101111","010110010010111","111001111100111","111001111001111","101101111001001","111100111001111","111100111101111","111001001001001","111101111101111","111101111001111"};
    static void Digits(Color32[] p,int w,int h,string s,int x,int y,Color32 c)
    {
        foreach(char ch in s)
        {
            if(ch=='.'){Draw(p,w,h,x,y,x+4,y+4,c);x+=8;continue;}
            if(ch=='-'){Draw(p,w,h,x,y+8,x+12,y+12,c);x+=16;continue;}
            string bits=Font[ch-'0'];
            for(int row=0;row<5;row++)for(int col=0;col<3;col++)if(bits[row*3+col]=='1')
                Draw(p,w,h,x+col*4,y+(4-row)*4,x+col*4+4,y+(4-row)*4+4,c);
            x+=16;
        }
    }
}
