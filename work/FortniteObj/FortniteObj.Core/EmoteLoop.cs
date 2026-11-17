using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;

namespace FortniteObj.Core;

public static class EmoteLoop
{
    public record Section(string Name, string Next, double Start);
    public record Window(double Start, double End, double Rate = 1);
    public static Window? FindCycle(IReadOnlyList<Section> sections, double length)
    {
        var ordered=sections.OrderBy(s=>s.Start).ToArray();
        var byName=ordered.GroupBy(s=>s.Name).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        foreach(var seed in ordered.OrderByDescending(s=>s.Name.Contains("loop",StringComparison.OrdinalIgnoreCase))) {
            var path=new List<Section>();var indices=new Dictionary<string,int>(StringComparer.Ordinal);var current=seed;
            while(true) {
                if(indices.TryGetValue(current.Name,out int first)) {
                    var cycle=path.Skip(first).ToArray();
                    double start=cycle.Min(s=>s.Start), end=cycle.Max(s=>ordered.FirstOrDefault(n=>n.Start>s.Start+1e-6)?.Start ?? length);
                    if(end>start+1e-5 && start>=0 && end<=length+1e-4) return new(start,end);
                    break;
                }
                indices[current.Name]=path.Count;path.Add(current);
                if(!byName.TryGetValue(current.Next,out var next)) break;
                current=next;
            }
        }
        return null;
    }
    public static Window? SourceWindow(UAnimationAsset asset, CAnimSequence sequence)
    {
        if(asset is not UAnimMontage montage) return null;
        var cycle=FindCycle(montage.CompositeSections.Select(s=>new Section(s.SectionName.Text,s.NextSectionName.Text,s.GetTime())).ToArray(),montage.CalculateSequenceLength());
        if(cycle==null) return null;
        foreach(var slot in montage.SlotAnimTracks.Where(s=>s.SlotName.Text==sequence.SlotName))
        foreach(var segment in slot.AnimTrack.AnimSegments) {
            if(segment.AnimReference.ResolvedObject?.Name.Text!=sequence.OriginalSequence.Name) continue;
            if(cycle.Start<segment.StartPos-1e-4 || cycle.End>segment.StartPos+segment.GetLength()+1e-4) continue;
            // One source cycle, even if the montage repeats this segment many times.
            double rate=Math.Abs(segment.GetValidPlayRate());
            if(segment.AnimPlayRate<0) return null;
            double start=segment.AnimStartTime+(cycle.Start-segment.StartPos)*rate;
            double end=Math.Min(segment.AnimEndTime,segment.AnimStartTime+(cycle.End-segment.StartPos)*rate);
            if(end>start) return new(start,end,rate);
        }
        return null;
    }
}
