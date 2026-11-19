using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse_Conversion.Sounds;

namespace FortniteObj.Core;
public sealed partial class GameSession
{
    private sealed record EmoteAudioTrack(string File,string Source,double Start,double Duration,double Gain,bool Loop=false);
    private static bool AudioGraph(UObject value)=>value is UAnimationAsset or USoundWave || value.ExportType is "SoundCue" || value.ExportType.StartsWith("SoundNode",StringComparison.Ordinal) || value.ExportType.Contains("MetaSound",StringComparison.OrdinalIgnoreCase);
    private string? audioTrace;
    private object[] ExportEmoteAudio(UAnimationAsset animation,string folder,List<string> warnings,CancellationToken ct)
    {
        audioTrace=Path.Combine(folder,"audio-progress.log");
        var tracks=new List<EmoteAudioTrack>();var queue=new Queue<(UObject Object,double Start,int Depth)>();
        var seen=new HashSet<string>();var graph=new List<object>();queue.Enqueue((animation,0,0));
        while(queue.Count>0 && seen.Count<512){
            ct.ThrowIfCancellationRequested();var (value,start,depth)=queue.Dequeue();
            if(depth>16||!seen.Add(value.GetPathName()+"|"+start.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)))continue;
            graph.Add(new{Path=value.GetPathName(),Class=value.ExportType,Start=start});
            File.AppendAllText(Path.Combine(folder,"audio-progress.log"),value.GetPathName()+"\n");
            if(value is USoundWave wave && !value.ExportType.Contains("MetaSound",StringComparison.OrdinalIgnoreCase)){
                try{
                    wave.Decode(false,out string format,out byte[]? bytes);
                    if(bytes==null||bytes.Length==0)throw new IOException("No cooked audio payload was available.");
                    string audio=Path.Combine(folder,"audio");Directory.CreateDirectory(audio);
                    string name=$"{tracks.Count:D3}_{Installation.SafeName(wave.Name)}";
                    string raw=Path.Combine(audio,name+"."+format.ToLowerInvariant());File.WriteAllBytes(raw,bytes);
                    string wav=Path.Combine(audio,name+".wav");
                    if(format.Equals("WAV",StringComparison.OrdinalIgnoreCase)){if(raw!=wav)File.Copy(raw,wav,true);}
                    else{
                        string tool=format.Equals("RADA",StringComparison.OrdinalIgnoreCase)?RadaDecoder():Path.Combine(AppContext.BaseDirectory,"audio-tools","vgmstream-cli.exe");
                        var info=new ProcessStartInfo(tool){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                        foreach(string arg in format.Equals("RADA",StringComparison.OrdinalIgnoreCase)?new[]{"-i",raw,"-o",wav}:new[]{"-i","-o",wav,raw})info.ArgumentList.Add(arg);
                        using var process=Process.Start(info)??throw new IOException("Audio decoder did not start.");
                        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                        using var registration=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
                        if(!process.WaitForExit(120000)){process.Kill(true);throw new IOException("Audio decoder timed out.");}
                        ct.ThrowIfCancellationRequested();if(process.ExitCode!=0||!File.Exists(wav))throw new IOException(error.GetAwaiter().GetResult());
                        output.GetAwaiter().GetResult();
                    }
                    tracks.Add(new EmoteAudioTrack("audio/"+Path.GetFileName(wav),wave.GetPathName(),Math.Max(0,start),WavDuration(wav),1,wave.Name.EndsWith("_Loop",StringComparison.OrdinalIgnoreCase)));
                }catch(Exception ex) when(ex is not OperationCanceledException){warnings.Add(wave.Name+": audio export failed: "+ex.Message);}
                continue;
            }
            if(value is UAnimSequenceBase sequence && sequence.Notifies!=null){
                foreach(var notify in sequence.Notifies){
                    foreach(var reference in new[]{notify.NotifyStateClass,notify.Notify}){
                        if(reference==null||reference.IsNull)continue;
                        var obj=reference.Load();if(obj==null)continue;
                        double offset=start+notify.GetTime();
                        var sound3=obj.Properties.FirstOrDefault(p=>p.Name.Text=="EmoteSound3P");
                        var sounds=ReferencedObjects(sound3?.Tag?.GenericValue,warnings).ToArray();
                        if(sounds.Length==0){var sound1=obj.Properties.FirstOrDefault(p=>p.Name.Text=="EmoteSound1P");sounds=ReferencedObjects(sound1?.Tag?.GenericValue,warnings).ToArray();}
                        if(sounds.Length>0){foreach(var child in sounds)queue.Enqueue((child,offset,depth+1));}
                        else if(obj.ExportType.Contains("Sound",StringComparison.OrdinalIgnoreCase))queue.Enqueue((obj,offset,depth+1));
                    }
                }
            }
            foreach(var property in value.Properties){
                string key=property.Name.Text;
                if(key is "Skeleton" or "PreviewSkeletalMesh" or "Notifies" or "AnimNotifies")continue;
                if(value is UAnimationAsset && !key.Contains("Anim",StringComparison.OrdinalIgnoreCase)&&!key.Contains("Sound",StringComparison.OrdinalIgnoreCase)&&key!="SlotAnimTracks")continue;
                var children=ReferencedObjects(property.Tag?.GenericValue,warnings).ToArray();
                if(value.ExportType.Contains("Random",StringComparison.OrdinalIgnoreCase))children=children.Take(1).ToArray();
                foreach(var child in children)if(AudioGraph(child))queue.Enqueue((child,start,depth+1));
            }
        }
        for(int i=0;i<tracks.Count;i++)if(tracks[i].Loop){string prefix=tracks[i].Source.Replace("_Loop","_Intro",StringComparison.OrdinalIgnoreCase);var intro=tracks.FirstOrDefault(t=>t.Source.Equals(prefix,StringComparison.OrdinalIgnoreCase));if(intro!=null)tracks[i]=tracks[i] with{Start=tracks[i].Start+intro.Duration};}
        File.WriteAllText(Path.Combine(folder,"audio-report.json"),JsonSerializer.Serialize(new{Tracks=tracks,Graph=graph},new JsonSerializerOptions{WriteIndented=true}));
        if(tracks.Count==0 && graph.Count>1)warnings.Add("No playable emote sound was resolved from the animation's authored sound references.");
        return tracks.ToArray();
    }
    private static double WavDuration(string path){
        using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);stream.Position=12;uint rate=0,size=0;
        while(stream.Position+8<=stream.Length){string id=new string(reader.ReadChars(4));uint length=reader.ReadUInt32();long next=stream.Position+length+(length%2);if(id=="fmt "&&length>=16){reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();rate=reader.ReadUInt32();}if(id=="data")size=length;if(next>stream.Length)break;stream.Position=next;}
        return rate>0?(double)size/rate:0;
    }
    private static string RadaDecoder(){
        string path=Path.Combine(AppSettings.DataFolder,"audio-tools","radadec.exe");
        if(!File.Exists(path))throw new IOException("RAD audio decoder is not installed. Source audio was retained; install the local radadec decoder to create WAV audio.");
        const string checksum="4D0022C501D61EA97495EDD58645C82AA78461B08E6BE2AE79FC37A7B7B3A7F2";
        if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))!=checksum)throw new IOException("The local RAD audio decoder failed its integrity check.");return path;
    }

}
