using System;
namespace FortniteObj.Core;
public sealed class RocketAppearance
{
 public string DecalId {get;set;}="Default";
 public string Primary {get;set;}="#195CB8";
 public string Secondary {get;set;}="#EF8124";
 public string BodyPaint {get;set;}="Default";
 public string WheelId {get;set;}="";
 public string WheelPaint {get;set;}="Default";
 public string PrimaryFinish {get;set;}="Glossy";
 public string SecondaryFinish {get;set;}="Glossy";
 public static readonly string[] Paints={"Default","Crimson","Lime","Black","Sky Blue","Cobalt","Burnt Sienna","Forest Green","Purple","Pink","Orange","Grey","Titanium White","Saffron","Gold"};
 public static readonly string[] Finishes={"Glossy","Matte","Metallic","Pearlescent","Anodized","Anodized Pearl","Brushed Metal","Carbon Fiber"};
 public static bool ValidColor(string value)=>System.Text.RegularExpressions.Regex.IsMatch(value??"","^#[0-9a-fA-F]{6}$");
 public void Validate(){if(!ValidColor(Primary)||!ValidColor(Secondary))throw new ArgumentException("Use a six-digit colour such as #195CB8.");if(!Array.Exists(Paints,p=>p==BodyPaint)||!Array.Exists(Paints,p=>p==WheelPaint)||!Array.Exists(Finishes,p=>p==PrimaryFinish)||!Array.Exists(Finishes,p=>p==SecondaryFinish))throw new ArgumentException("Unknown Rocket League paint or finish.");}
}
