using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace FortniteObj.Core;

public static class VehicleTextureTemplates
{
    public sealed record Face(string Mesh, float[][] Points);
    public sealed record Group(string Material, string UV, Face[] Faces);
    public sealed record Layout(Group[] Groups, string[] Warnings);
    const int Size = 2048;

    public static async Task<ExportResult> Create(ExportResult result, IProgress<string> progress, CancellationToken ct)
    {
        string? blend=Directory.EnumerateFiles(result.Folder,"*.blend").SingleOrDefault();
        if(blend==null)throw new IOException("A packed Blender vehicle is needed to create texture templates.");
        string blenderRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");
        string? blender=Directory.Exists(blenderRoot)?Directory.EnumerateFiles(blenderRoot,"blender.exe",SearchOption.AllDirectories).OrderDescending().FirstOrDefault():null;
        if(blender==null)throw new IOException("Install Blender to export vehicle UV templates.");
        string stage=Path.Combine(result.Folder,".uv-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try {
            progress.Report("Reading vehicle UVs for labelled texture templates…");
            string layout=Path.Combine(stage,"uv.json"),request=Path.Combine(stage,"request.json");
            await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new{Blend=blend,Result=layout}),ct);
            var start=new ProcessStartInfo(blender){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--background","--factory-startup","--python-exit-code","1","--python",Path.Combine(AppContext.BaseDirectory,"vehicle_template.py"),"--",request})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new IOException("Could not start vehicle template worker.");
            Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
            try {await process.WaitForExitAsync(ct);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
            await stdout;string errors=await stderr;
            if(process.ExitCode!=0||!File.Exists(layout))throw new IOException("Vehicle template extraction failed: "+errors[..Math.Min(errors.Length,1500)]);
            var data=JsonSerializer.Deserialize<Layout>(await File.ReadAllTextAsync(layout,ct))??throw new IOException("No vehicle UV layout was returned.");
            string output=Path.Combine(result.Folder,"texture-templates");Directory.CreateDirectory(output);
            var index=new List<object>();var warnings=data.Warnings.ToList();int sheet=0;
            foreach(var group in data.Groups){
                ct.ThrowIfCancellationRequested();
                var tiles=group.Faces.Select(f=>(U:(int)Math.Floor(f.Points.Average(p=>p[0])),V:(int)Math.Floor(f.Points.Average(p=>p[1])))).Distinct().OrderBy(t=>t.V).ThenBy(t=>t.U).ToArray();
                if(tiles.Length>64){warnings.Add(group.Material+": more than 64 UV tiles; template generation was skipped for this tiled surface.");continue;}
                foreach(var tile in tiles){
                    ct.ThrowIfCancellationRequested();
                    var faces=group.Faces.Where(f=>(int)Math.Floor(f.Points.Average(p=>p[0]))==tile.U&&(int)Math.Floor(f.Points.Average(p=>p[1]))==tile.V).ToArray();
                    string stem=$"{++sheet:D3}_{Installation.SafeName(group.Material)[..Math.Min(40,Installation.SafeName(group.Material).Length)]}_u{tile.U}_v{tile.V}";
                    var islands=Islands(faces);Draw(Path.Combine(output,stem+"-uv.png"),group,tile,faces,islands,false,Path.GetFileNameWithoutExtension(blend));
                    Draw(Path.Combine(output,stem+"-labelled.png"),group,tile,faces,islands,true,Path.GetFileNameWithoutExtension(blend));
                    index.Add(new{group.Material,group.UV,Tile=tile.U+","+tile.V,WireGuide=stem+"-uv.png",Guide=stem+"-labelled.png",Islands=islands.Select((f,i)=>new{Number=i+1,Meshes=f.Select(x=>x.Mesh).Distinct()})});
                }
            }
            await File.WriteAllTextAsync(Path.Combine(output,"index.json"),JsonSerializer.Serialize(index,new JsonSerializerOptions{WriteIndented=true}),ct);
            await File.WriteAllTextAsync(Path.Combine(output,"README.txt"),"VEHICLE TEXTURE TEMPLATES\n2048 x 2048 PNG, original model UV coordinates.\n\n-uv.png: transparent wire guide for painting on a layer underneath.\n-labelled.png: reference guide with numbered UV islands and mesh names. Labels are reference marks; do not paint them onto the final skin.\n\nEach material and used UV tile has its own sheet. Repeated/overlapping UVs intentionally share texture pixels. These guides do not re-unwrap the model. Material/mesh names identify the source regions; anatomical panel names are not guessed. index.json maps island numbers to meshes.\n\nKeep your paint aligned to the full square. Replace the matching material base-colour image in Blender to apply your custom texture. Custom-painted exports are local assets and do not install decals into the game.\n",ct);
            progress.Report($"Saved {sheet} labelled vehicle texture sheet(s).");
            return result with{Warnings=result.Warnings.Concat(warnings).Distinct().ToArray()};
        } finally {if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }

    static List<Face[]> Islands(Face[] faces)
    {
        int[] parent=Enumerable.Range(0,faces.Length).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        var edges=new Dictionary<string,int>();
        string Point(float[] p)=>Math.Round(p[0],5).ToString("F5",System.Globalization.CultureInfo.InvariantCulture)+","+Math.Round(p[1],5).ToString("F5",System.Globalization.CultureInfo.InvariantCulture);
        for(int i=0;i<faces.Length;i++)for(int j=0;j<faces[i].Points.Length;j++){
            string a=Point(faces[i].Points[j]),b=Point(faces[i].Points[(j+1)%faces[i].Points.Length]);string key=string.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a;
            if(edges.TryGetValue(key,out int other))parent[Root(i)]=Root(other);else edges[key]=i;
        }
        return faces.Select((f,i)=>(Face:f,Root:Root(i))).GroupBy(x=>x.Root).Select(g=>g.Select(x=>x.Face).ToArray()).OrderByDescending(g=>g.Length).ToList();
    }

    static void Draw(string path,Group group,(int U,int V) tile,Face[] faces,List<Face[]> islands,bool labelled,string vehicle)
    {
        using var bitmap=new SKBitmap(Size,Size,SKColorType.Rgba8888,SKAlphaType.Premul);using var canvas=new SKCanvas(bitmap);canvas.Clear(labelled?new SKColor(245,245,242):SKColors.Transparent);
        using var line=new SKPaint{Color=labelled?new SKColor(90,95,100,150):new SKColor(60,65,70,150),StrokeWidth=1,Style=SKPaintStyle.Stroke,IsAntialias=true};
        foreach(var face in faces){using var outline=new SKPath();for(int i=0;i<face.Points.Length;i++){float x=(face.Points[i][0]-tile.U)*Size,y=(1-face.Points[i][1]+tile.V)*Size;if(i==0)outline.MoveTo(x,y);else outline.LineTo(x,y);}outline.Close();canvas.DrawPath(outline,line);}
        if(labelled){
            using var background=new SKPaint{Color=new SKColor(18,20,23,235)};using var text=new SKPaint{Color=SKColors.White,IsAntialias=true,TextSize=25,Typeface=SKTypeface.FromFamilyName("Segoe UI")};
            canvas.DrawRect(0,0,Size,155,background);canvas.DrawText(vehicle[..Math.Min(vehicle.Length,95)],24,35,text);text.TextSize=20;canvas.DrawText(group.Material[..Math.Min(group.Material.Length,110)],24,67,text);canvas.DrawText($"{group.UV}  ·  tile {tile.U}, {tile.V}  ·  numbered UV islands  ·  reference only",24,99,text);
            string meshes=string.Join(", ",faces.Select(f=>f.Mesh).Distinct());canvas.DrawText("Meshes: "+meshes[..Math.Min(meshes.Length,145)],24,131,text);
            for(int i=0;i<islands.Count;i++){
                var points=islands[i].SelectMany(f=>f.Points).ToArray();float x=Math.Clamp((points.Average(p=>p[0])-tile.U)*Size,20,Size-50),y=Math.Clamp((1-points.Average(p=>p[1])+tile.V)*Size,175,Size-55);
                float width=(points.Max(p=>p[0])-points.Min(p=>p[0]))*Size,height=(points.Max(p=>p[1])-points.Min(p=>p[1]))*Size;if(width*height<500)continue;
                string label=(i+1).ToString("D2");text.TextSize=21;canvas.DrawRoundRect(x-5,y-25,42,32,5,5,background);canvas.DrawText(label,x,y,text);
            }
            canvas.DrawRect(0,Size-42,Size,42,background);text.TextSize=19;canvas.DrawText("Match island numbers to mesh names in index.json. Use the separate UV PNG while painting.",24,Size-14,text);
        }
        using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(path);encoded.SaveTo(file);
    }
}
