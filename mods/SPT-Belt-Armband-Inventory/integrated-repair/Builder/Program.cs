using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

const string InputHash = "ca2774ba4fc6cc1183863b8f008916f40a78e2b517e4934344286e233c26453a";
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
IEnumerable<TypeDefinition> All(ModuleDefinition module) { foreach (var t in module.Types) foreach (var n in Walk(t)) yield return n; }
IEnumerable<TypeDefinition> Walk(TypeDefinition type) { yield return type; foreach (var t in type.NestedTypes) foreach (var n in Walk(t)) yield return n; }
string Fingerprint(MethodDefinition m)
{
 if (!m.HasBody) return "NO-BODY";
 var b=m.Body; string Operand(object o) => o switch { Instruction i => "I"+b.Instructions.IndexOf(i), Instruction[] v => string.Join(",",v.Select(i=>"I"+b.Instructions.IndexOf(i))), ParameterDefinition p=>"P"+p.Index, VariableDefinition v=>"V"+v.Index, MemberReference r=>r.FullName, null=>"", _=>o.ToString() };
 return string.Join("\n",b.Instructions.Select(i=>i.OpCode.Name+" "+Operand(i.Operand)))+string.Join(";",b.Variables.Select(v=>v.VariableType.FullName))+string.Join(";",b.ExceptionHandlers.Select(e=>$"{e.HandlerType}:{Operand(e.TryStart)}:{Operand(e.TryEnd)}:{Operand(e.HandlerStart)}:{Operand(e.HandlerEnd)}:{e.CatchType?.FullName}"));
}
if(args.Length>=2 && args[0]=="dump")
{
 using var mod=ModuleDefinition.ReadModule(args[1]);
 Console.WriteLine("ASSEMBLY "+mod.Assembly.Name+" SHA256="+Hash(args[1]));
 string typeFilter=args.Length>2?args[2]:"";
 string[] names=args.Length>3?args[3].Split('|'):Array.Empty<string>();
 foreach(var t in All(mod).Where(t=>t.FullName.Contains(typeFilter,StringComparison.Ordinal)))
 {
  Console.WriteLine("TYPE "+t.FullName+" BASE "+t.BaseType);
  foreach(var f in t.Fields)Console.WriteLine("FIELD "+f.FullName);
  foreach(var p in t.Properties)Console.WriteLine("PROPERTY "+p.FullName);
  foreach(var m in t.Methods.Where(m=>names.Length==0||names.Any(n=>m.Name.Contains(n,StringComparison.Ordinal))))
  {
   Console.WriteLine("METHOD "+m.FullName+" TOKEN="+m.MetadataToken);
   if(!m.HasBody)continue;
   foreach(var v in m.Body.Variables)Console.WriteLine("LOCAL "+v.Index+" "+v.VariableType);
   foreach(var i in m.Body.Instructions)Console.WriteLine(i.ToString());
  }
 }
 return;
}
if(args.Length==2 && args[0]=="inspect")
{
 using var mod=ModuleDefinition.ReadModule(args[1]);
 foreach(var t in All(mod)) foreach(var m in t.Methods) if(new[]{"IsAtReachablePlace","GetAllParentItems","GetItemsInSlots","GetThrowablePriorityGrenadesList","GetReachableItemsOfTypeNonAlloc","GetReachableItemsOfType","FindCompatibleAmmo"}.Contains(m.Name)) Console.WriteLine(m.FullName);
 return;
}
if(args.Length!=4) throw new ArgumentException("Usage: input-client.dll build-runtime.dll output-client.dll source-sha; inspect assembly.dll; dump assembly.dll [type-filter] [method-filter]");
if(Hash(args[0])!=InputHash) throw new InvalidOperationException("Unknown input client; no output written.");
if(args[3].Length!=40 || !args[3].All(Uri.IsHexDigit)) throw new ArgumentException("Exact source SHA required");
using var basis=ModuleDefinition.ReadModule(args[0],new ReaderParameters{ReadSymbols=false,InMemory=true});
using var donor=ModuleDefinition.ReadModule(args[1],new ReaderParameters{ReadSymbols=false,InMemory=true});
string[] wanted={"SPTBeltArmbandInventory.IntegratedBeltAccess","SPTBeltArmbandInventory.NativeBeltActions"};
var sources=wanted.Select(name=>donor.Types.Single(t=>t.FullName==name)).ToArray();
if(sources.Any(t=>t.HasNestedTypes||t.HasGenericParameters)||donor.Types.Any(t=>t.Name!="<Module>"&&!wanted.Contains(t.FullName)))throw new InvalidOperationException("Unexpected donor type shape");
if(basis.Types.Any(t=>wanted.Contains(t.FullName)))throw new InvalidOperationException("Already integrated input");
var before=All(basis).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Fingerprint);
var resourceHashes=basis.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Convert.ToHexString(SHA256.HashData(r.GetResourceData())));
var typesBefore=All(basis).Select(t=>t.FullName).ToArray();
var typeMap=new Dictionary<string,TypeDefinition>();
foreach(var src in sources){var dst=new TypeDefinition(src.Namespace,src.Name,src.Attributes,basis.ImportReference(src.BaseType));basis.Types.Add(dst);typeMap.Add(src.FullName,dst);}
TypeReference T(TypeReference t)=>typeMap.TryGetValue(t.FullName,out var mapped)?mapped:basis.ImportReference(t);
var fields=new Dictionary<string,FieldDefinition>();var methods=new Dictionary<string,MethodDefinition>();
var methodPairs=new List<(MethodDefinition Source,MethodDefinition Target)>();
foreach(var src in sources)
{
 var dst=typeMap[src.FullName];
 foreach(var f in src.Fields)
 {
  var n=new FieldDefinition(f.Name,f.Attributes,T(f.FieldType));if(f.HasConstant)n.Constant=f.Constant;
  foreach(var a in f.CustomAttributes)
  {
   if(a.ConstructorArguments.Count!=0||a.HasFields||a.HasProperties)throw new InvalidOperationException("Unexpected field attribute payload");
   n.CustomAttributes.Add(new CustomAttribute(basis.ImportReference(a.Constructor)));
  }
  dst.Fields.Add(n);fields.Add(f.FullName,n);
 }
 foreach(var m in src.Methods)
 {
  if(m.HasGenericParameters)throw new InvalidOperationException("Unexpected generic donor method");
  var n=new MethodDefinition(m.Name,m.Attributes,T(m.ReturnType)){ImplAttributes=m.ImplAttributes,CallingConvention=m.CallingConvention};
  foreach(var p in m.Parameters)n.Parameters.Add(new ParameterDefinition(p.Name,p.Attributes,T(p.ParameterType)));
  dst.Methods.Add(n);methods.Add(m.FullName,n);methodPairs.Add((m,n));
 }
}
object Import(object o)=>o switch
{
 TypeReference t=>T(t),
 MethodReference m when methods.ContainsKey(m.FullName)=>methods[m.FullName],
 MethodReference m=>basis.ImportReference(m),
 FieldReference f when fields.ContainsKey(f.FullName)=>fields[f.FullName],
 FieldReference f=>basis.ImportReference(f),
 _=>o
};
foreach(var pair in methodPairs)
{
 var old=pair.Source;var n=pair.Target;if(!old.HasBody)continue;
 n.Body=new MethodBody(n){InitLocals=old.Body.InitLocals,MaxStackSize=old.Body.MaxStackSize};
 foreach(var v in old.Body.Variables)n.Body.Variables.Add(new VariableDefinition(T(v.VariableType)));
 var map=new Dictionary<Instruction,Instruction>();
 foreach(var i in old.Body.Instructions){var j=Instruction.Create(OpCodes.Nop);j.OpCode=i.OpCode;n.Body.Instructions.Add(j);map.Add(i,j);}
 Instruction I(Instruction i)=>i==null?null:map[i];
 foreach(var i in old.Body.Instructions)map[i].Operand=i.Operand switch{Instruction target=>I(target),Instruction[] targets=>targets.Select(I).ToArray(),VariableDefinition v=>n.Body.Variables[v.Index],ParameterDefinition p=>n.Parameters[p.Index],_=>Import(i.Operand)};
 foreach(var e in old.Body.ExceptionHandlers)n.Body.ExceptionHandlers.Add(new ExceptionHandler(e.HandlerType){TryStart=I(e.TryStart),TryEnd=I(e.TryEnd),HandlerStart=I(e.HandlerStart),HandlerEnd=I(e.HandlerEnd),FilterStart=I(e.FilterStart),CatchType=e.CatchType==null?null:T(e.CatchType)});
}
var entry=typeMap[wanted[1]];
var fast=basis.Types.Single(t=>t.FullName=="SPTBeltArmbandInventory.FastAccessSlotPatches");
var reach=fast.Methods.Single(m=>m.Name=="TryInstallReloadReachability");
var bridge=fast.Methods.Single(m=>m.Name=="TryInstallReloadCandidateBridge");
var destroy=basis.Types.Single(t=>t.FullName=="SPTBeltArmbandInventory.Plugin").Methods.Single(m=>m.Name=="OnDestroy");
var changed=new HashSet<string>{reach.FullName,bridge.FullName,destroy.FullName};
reach.Body=new MethodBody(reach);var il=reach.Body.GetILProcessor();
il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,fast.Fields.Single(f=>f.Name=="logInfo"));il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,fast.Fields.Single(f=>f.Name=="logWarning"));il.Emit(OpCodes.Call,entry.Methods.Single(m=>m.Name=="Install"));il.Emit(OpCodes.Ret);
bridge.Body=new MethodBody(bridge);il=bridge.Body.GetILProcessor();il.Emit(OpCodes.Call,entry.Methods.Single(m=>m.Name=="IsActive"));il.Emit(OpCodes.Ret);
il=destroy.Body.GetILProcessor();il.InsertBefore(destroy.Body.Instructions[0],Instruction.Create(OpCodes.Call,entry.Methods.Single(m=>m.Name=="Dispose")));
var attr=basis.Assembly.CustomAttributes.Single(a=>a.AttributeType.FullName=="System.Reflection.AssemblyInformationalVersionAttribute");
attr.ConstructorArguments[0]=new CustomAttributeArgument(basis.TypeSystem.String,"0.3.0+integrated.rc3."+args[3]);
basis.Mvid=Guid.NewGuid();
string output=Path.GetFullPath(args[2]);Directory.CreateDirectory(Path.GetDirectoryName(output));basis.Write(output,new WriterParameters{WriteSymbols=false});
using var check=ModuleDefinition.ReadModule(output);
var after=All(check).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Fingerprint);
foreach(var p in before)if(!changed.Contains(p.Key)&&(!after.TryGetValue(p.Key,out var value)||value!=p.Value))throw new InvalidOperationException("Unintended method change: "+p.Key);
foreach(var name in typesBefore)if(!All(check).Any(t=>t.FullName==name))throw new InvalidOperationException("Lost type: "+name);
foreach(var original in resourceHashes){var r=check.Resources.OfType<EmbeddedResource>().Single(r=>r.Name==original.Key);if(original.Value!=Convert.ToHexString(SHA256.HashData(r.GetResourceData())))throw new InvalidOperationException("Changed resource: "+r.Name);}
if(check.AssemblyReferences.Any(r=>r.Name==donor.Assembly.Name.Name||r.Name=="BAndHB.OperationalAccess"))throw new InvalidOperationException("External repair assembly dependency");
var nativeCheck=check.Types.Single(t=>t.FullName==wanted[1]);
if(!nativeCheck.Fields.Single(f=>f.Name=="prefixes").CustomAttributes.Any(a=>a.AttributeType.FullName=="System.ThreadStaticAttribute"))throw new InvalidOperationException("Lost ThreadStatic attribute");
int plugins=All(check).Count(t=>t.CustomAttributes.Any(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin"));
if(plugins!=1)throw new InvalidOperationException("Expected one plugin");
var report=new{InputSha256=InputHash,OutputSha256=Hash(output),SourceSha=args[3],Revision="integrated.rc3",PreservedOriginalMethods=before.Count-changed.Count,ModifiedOriginalMethods=changed,PreservedOriginalTypes=typesBefore.Length,IntegratedTypes=wanted,ThreadLocalAttributePreserved=true,EmbeddedResources=resourceHashes,BepInExPluginCount=plugins,SeparateRuntimeRepairAssembly=false,PhysicalRuntimeTest="NOT_RUN"};
File.WriteAllText(output+".verification.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
