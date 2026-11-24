using System;
using System.Collections.Generic;
using System.IO;

// Ordinary unshifted PC DOS X720 wall ornaments only. No calibrated or legacy fallback.
internal sealed class DosOrnamentGeometryResolver
{
    private static readonly int[] Sets = {
        7,1,1,1,0,0,0,8,0,8,0,2,2,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,2,2,1,1,1,1,1,4,4,4,5,0,
        0,1,0,0,0,2,0,0,0,0,2,6,6,6,6
    }; // Original G0194 data, transcribed in the verified Python prototype.
    private static readonly string[] Views = {
        "D3 far-left right", "D3 far-right left", "D3 left right", "D3 right left",
        "D3 left front", "D3 center front", "D3 right front", "D2 left right",
        "D2 right left", "D2 left front", "D2 center front", "D2 right front",
        "D1 left right", "D1 right left", "D1 center front"
    };
    private struct Record { public int Kind, Parent, A, B; }
    private readonly Dictionary<int, Record> records;
    private readonly int[] widths = new int[713], heights = new int[713];
    private static int U16(byte[] b, int p) { return b[p] | (b[p+1] << 8); }
    private static int S16(byte[] b, int p) { return unchecked((short)U16(b,p)); }
    public DosOrnamentGeometryResolver(byte[] b)
    {
        if (b == null || b.Length < 4 + 8*713 || U16(b,0)!=0x8001 || U16(b,2)!=713)
            throw new InvalidDataException("Unsupported or truncated DOS GRAPHICS.DAT");
        for (int i=0;i<713;i++) { widths[i]=U16(b,4+4*713+4*i); heights[i]=U16(b,6+4*713+4*i); }
        Dictionary<int,Record> found = null;
        for (int p=0;p<=b.Length-96;p++)
        {
            if (U16(b,p)!=0xfc0d || U16(b,p+2)!=23) continue;
            int count=0; bool valid=true;
            for (int i=0;i<23;i++) { int a=U16(b,p+4+4*i), z=U16(b,p+6+4*i); if(z<a){valid=false;break;} count+=z-a+1; }
            if(!valid || count!=1133 || p+96+count*8>b.Length) continue;
            var candidate=new Dictionary<int,Record>(); int q=p+96;
            for(int i=0;i<23;i++) for(int j=U16(b,p+4+4*i);j<=U16(b,p+6+4*i);j++)
            {
                if(candidate.ContainsKey(j)) throw new InvalidDataException("Duplicate layout index");
                candidate.Add(j,new Record{Kind=S16(b,q),Parent=S16(b,q+2),A=S16(b,q+4),B=S16(b,q+6)}); q+=8;
            }
            if(found!=null) throw new InvalidDataException("Ambiguous resource 696");
            found=candidate;
        }
        records=found ?? throw new InvalidDataException("Resource 696 missing");
    }
    public sealed class Result
    {
        public int Type, CoordinateSet, View, Zone, Graphic, NativeWidth, NativeHeight, ScaleX, ScaleY;
        public string ViewName, PaletteGroup;
        public bool Mirrored;
        public int X,Y,Width,Height,ClippedWidth,ClippedHeight;
        public int? ClippedX,ClippedY,SourceX,SourceY;
    }
    private static int Scaled(int d,int s) { return ((d*s)+(s>>1))>>5; }
    private static void Anchor(int kind,int w,int h,ref int x,ref int y)
    {
        if(kind<0 || kind>8) throw new NotSupportedException("Unsupported DOS anchor "+kind);
        if(kind==0 || kind==5 || kind==7) x-=(w+1)>>1;
        else if(kind==2 || kind==3 || kind==6) x-=w-1;
        if(kind==0 || kind==6 || kind==8) y-=(h+1)>>1;
        else if(kind==3 || kind==4 || kind==7) y-=h-1;
    }
    public Result Resolve(int type,int view)
    {
        if(type<0 || type>=Sets.Length) throw new ArgumentOutOfRangeException(nameof(type));
        if(view<0 || view>=15) throw new ArgumentOutOfRangeException(nameof(view));
        var r=new Result{Type=type,CoordinateSet=Sets[type],View=view,ViewName=Views[view]};
        r.Zone=1004+15*r.CoordinateSet+view;
        r.Mirrored=view==1 || view==3 || view==8 || view==13;
        bool front=view>=4 && view!=7 && view!=8 && view!=12 && view!=13;
        r.Graphic=259+2*type+(front?1:0);
        r.NativeWidth=widths[r.Graphic]; r.NativeHeight=heights[r.Graphic];
        r.ScaleX=view<=1?30:view<=6?14:view<=11?21:32;
        r.ScaleY=view<=6?14:view<=11?21:32;
        r.PaletteGroup=view<=1?"D3 special":view<=6?"D3":view<=11?"D2":"native";
        r.Width=Scaled(r.NativeWidth,r.ScaleX); r.Height=Scaled(r.NativeHeight,r.ScaleY);
        if(r.Width<=0 || r.Height<=0) throw new InvalidDataException("Invalid scaled dimensions");
        Record child=records[r.Zone]; int kind=child.Kind;
        if(kind<0 || kind>8) throw new NotSupportedException("Unsupported child kind "+kind);
        int x=child.A,y=child.B,cl=0,ct=0,cw=20000,ch=20000;
        var seen=new HashSet<int>{r.Zone};
        while(child.Parent!=0)
        {
            if(!seen.Add(child.Parent)) throw new InvalidDataException("Layout parent cycle");
            Record parent=records[child.Parent];
            if(parent.Kind==1){ x+=parent.A;y+=parent.B;cl+=parent.A;ct+=parent.B; }
            else if(parent.Kind==9)
            {
                int px=child.A,py=child.B; Anchor(child.Kind,parent.A,parent.B,ref px,ref py);
                int l=Math.Max(cl,px),t=Math.Max(ct,py),right=Math.Min(cl+cw,px+parent.A),bottom=Math.Min(ct+ch,py+parent.B);
                cl=l;ct=t;cw=right-l;ch=bottom-t;
            }
            else throw new NotSupportedException("Unsupported parent kind "+parent.Kind);
            child=parent;
        }
        Anchor(kind,r.Width,r.Height,ref x,ref y); r.X=x;r.Y=y;
        int left=Math.Max(x,cl),top=Math.Max(y,ct),rr=Math.Min(x+r.Width,cl+cw),bb=Math.Min(y+r.Height,ct+ch);
        if(rr>left && bb>top){r.ClippedX=left;r.ClippedY=top;r.ClippedWidth=rr-left;r.ClippedHeight=bb-top;r.SourceX=left-x;r.SourceY=top-y;}
        return r;
    }
}
