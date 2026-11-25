using System;
using SkiaSharp;
namespace FortniteObj.Core;
public static class MaterialTextureProcessing
{
    // Unreal Desaturation uses its linear colour luminance coefficients.
    // Preserve alpha and bake the result so Blender and Studio use identical pixels.
    public static byte[] Desaturate(byte[] png,float amount)
    {
        amount=Math.Clamp(amount,0,1);
        using var bitmap=SKBitmap.Decode(png)??throw new System.IO.IOException("Invalid base-colour image.");
        var pixels=bitmap.Pixels;
        static double Linear(byte x){double s=x/255.0;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
        static byte Srgb(double x)=>(byte)Math.Clamp((int)Math.Round(255*(x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055)),0,255);
        for(int i=0;i<pixels.Length;i++){
            var p=pixels[i];double r=Linear(p.Red),g=Linear(p.Green),b=Linear(p.Blue),l=.3*r+.59*g+.11*b;
            pixels[i]=new SKColor(Srgb(r+(l-r)*amount),Srgb(g+(l-g)*amount),Srgb(b+(l-b)*amount),p.Alpha);
        }
        bitmap.Pixels=pixels;using var encoded=bitmap.Encode(SKEncodedImageFormat.Png,100);return encoded.ToArray();
    }
}
