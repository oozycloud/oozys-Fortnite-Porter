using FortniteObj.Core;
using System.Text.Json;
var result=JsonSerializer.Deserialize<ExportResult>(File.ReadAllText(args[0]))!;
foreach(var format in args.Length > 1 ? args[1].Split(",") : ExchangeExport.Formats.Skip(2)) {
 if(Directory.Exists(Path.Combine(result.Folder,format))) continue;
 var settings=new AppSettings{ExportFormat=format,TextureSizeLimit=1024,TriangulateMeshes=true};
 await ExchangeExport.Create(result,settings,new Progress<string>(Console.WriteLine),CancellationToken.None);
 Console.WriteLine("PASS "+format);
}
