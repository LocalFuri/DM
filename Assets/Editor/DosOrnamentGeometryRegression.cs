using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class DosOrnamentGeometryRegression
{
    [MenuItem("Tools/DOS Ornament Geometry Regression")]
    private static void Run()
    {
        string dataPath=DosOrnamentGeometryResolver.ProjectDataPath(Application.dataPath);
        string csv=EditorUtility.OpenFilePanel("Select corrected 900-row DOS geometry CSV", @"C:\Unity\DM\Research_Results", "csv");
        if(string.IsNullOrEmpty(csv)) return;
        try
        {
            var resolver=new DosOrnamentGeometryResolver(File.ReadAllBytes(dataPath));
            string[] lines=File.ReadAllLines(csv);
            const string header="label,ornament_type,coordinate_set,view_index,view_name,zone_index,native_graphic,native_width,native_height,scale_x,scale_y,palette_group,mirrored_horizontally,x,y,width,height,clipped_x,clipped_y,clipped_width,clipped_height,source_x,source_y";
            if(lines.Length!=901 || lines[0]!=header) throw new InvalidDataException("Expected the corrected 900-row CSV with original header");
            var seen=new HashSet<int>(); int matches=0,errors=0; var details=new List<string>();
            for(int i=1;i<lines.Length;i++)
            {
                string[] fields=lines[i].Split(','); if(fields.Length!=23) throw new InvalidDataException("Invalid CSV row "+i);
                int type=int.Parse(fields[1],CultureInfo.InvariantCulture),view=int.Parse(fields[3],CultureInfo.InvariantCulture);
                var r=resolver.Resolve(type,view);
                if(!seen.Add(type*15+view)) throw new InvalidDataException("Duplicate ornament/view row "+i);
                object[] actual={"Type_"+type.ToString("D2",CultureInfo.InvariantCulture),r.Type,r.CoordinateSet,r.View,r.ViewName,r.Zone,r.Graphic,r.NativeWidth,r.NativeHeight,r.ScaleX,r.ScaleY,r.PaletteGroup,r.Mirrored?1:0,r.X,r.Y,r.Width,r.Height,r.ClippedX,r.ClippedY,r.ClippedWidth,r.ClippedHeight,r.SourceX,r.SourceY};
                bool match=true;
                for(int j=0;j<actual.Length;j++)
                {
                    string value=Convert.ToString(actual[j],CultureInfo.InvariantCulture) ?? "";
                    if(value==fields[j]) continue;
                    match=false;errors++;
                    if(details.Count<20) details.Add("Row "+i+", "+header.Split(',')[j]+": expected "+fields[j]+", actual "+value);
                }
                if(match) matches++;
            }
            string summary="DOS geometry regression: "+matches+"/900 matching rows; "+errors+" field differences.";
            if(errors==0) Debug.Log(summary); else Debug.LogError(summary+"\n"+string.Join("\n",details));
        }
        catch(Exception e){Debug.LogError("DOS geometry regression failed: "+e.Message);}
    }
}
