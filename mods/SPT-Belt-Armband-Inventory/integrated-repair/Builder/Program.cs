using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

const string InputHash = "ca2774ba4fc6cc1183863b8f008916f40a78e2b517e4934344286e233c26453a";
const string RuntimeType = "SPTBeltArmbandInventory.IntegratedBeltAccess";
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
IEnumerable<TypeDefinition> All(ModuleDefinition module) { foreach (var t in module.Types) foreach (var n in Walk(t)) yield return n; }
IEnumerable<TypeDefinition> Walk(TypeDefinition type) { yield return type; foreach (var t in type.NestedTypes) foreach (var n in Walk(t)) yield return n; }
string Fingerprint(MethodDefinition m)
{
 if (!m.HasBody) return "NO-BODY";
 var b=m.Body; string Operand(object o) => o switch { Instruction i => "I"+b.Instructions.IndexOf(i), Instruction[] v => string.Join(",",v.Select(i=>"I"+b.Instructions.IndexOf(i))), ParameterDefinition p=>"P"+p.Index, VariableDefinition v=>"V"+v.Index, MemberReference r=>r.FullName, null=>"", _=>o.ToString() };
 return string.Join("\n",b.Instructions.Select(i=>i.OpCode.Name+" "+Operand(i.Operand)))+string.Join(";",b.Variables.Select(v=>v.VariableType.FullName))+string.Join(";",b.ExceptionHandlers.Select(e=>$"{e.HandlerType}:{Operand(e.TryStart)}:{Operand(e.TryEnd)}:{Operand(e.HandlerStart)}:{Operand(e.HandlerEnd)}:{e.CatchType?.FullName}"));
}
if(args.Length==2 && args[0]=="inspect")
{
 using var mod=ModuleDefinition.ReadModule(args[1]);
 foreach(var t in All(mod)) foreach(var m in t.Methods) if(new[]{"IsAtReachablePlace","GetAllParentItems","GetItemsInSlots","GetThrowablePriorityGrenadesList","get_Grids","get_Items"}.Contains(m.Name)) Console.WriteLine(m.FullName);
 return;
}
if(args.Length!=4) throw new ArgumentException("Usage: input-client.dll build-runtime.dll output-client.dll source-sha; or inspect assembly.dll");
if(Hash(args[0])!=InputHash) throw new InvalidOperationException("Unknown input client; no output written.");
using var basis=ModuleDefinition.ReadModule(args[0],new ReaderParameters{ReadSymbols=false,InMemory=true});
using var donor=ModuleDefinition.ReadModule(args[1],new ReaderParameters{ReadSymbols=false,InMemory=true});
var src=donor.Types.Single(t=>t.FullName==RuntimeType);
if(src.HasNestedTypes || src.HasGenericParameters) throw new InvalidOperationException("Integrator requires a single non-generic runtime type.");
if(basis.Types.Any(t=>t.FullName==RuntimeType)) throw new InvalidOperationException("Already integrated.");
var before=All(basis).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Fingerprint);
var resourceHashes=basis.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Convert.ToHexString(SHA256.HashData(r.GetResourceData())));
var typesBefore=All(basis).Select(t=>t.FullName).ToArray();
var dst=new TypeDefinition(src.Namespace,src.Name,src.Attributes,basis.ImportReference(src.BaseType)); basis.Types.Add(dst);
var fields=new Dictionary<FieldDefinition,FieldDefinition>(); var methods=new Dictionary<MethodDefinition,MethodDefinition>();
TypeReference T(TypeReference t) { if(t.FullName==src.FullName) return dst; return basis.ImportReference(t); }
foreach(var f in src.Fields) { var n=new FieldDefinition(f.Name,f.Attributes,T(f.FieldType)); if(f.HasConstant)n.Constant=f.Constant; dst.Fields.Add(n); fields.Add(f,n); }
foreach(var m in src.Methods)
{
 if(m.HasGenericParameters) throw new InvalidOperationException("Unexpected generic runtime method");
 var n=new MethodDefinition(m.Name,m.Attributes,T(m.ReturnType)){ImplAttributes=m.ImplAttributes,CallingConvention=m.CallingConvention};
 foreach(var p in m.Parameters) n.Parameters.Add(new ParameterDefinition(p.Name,p.Attributes,T(p.ParameterType)));
 dst.Methods.Add(n); methods.Add(m,n);
}
object Import(object o) => o switch
{
 TypeReference t=>T(t),
 MethodDefinition m when methods.ContainsKey(m)=>methods[m],
 MethodReference m when m.DeclaringType.FullName==src.FullName=>methods[src.Methods.Single(x=>x.FullName==m.FullName)],
 MethodReference m=>basis.ImportReference(m),
 FieldDefinition f when fields.ContainsKey(f)=>fields[f],
 FieldReference f when f.DeclaringType.FullName==src.FullName=>fields[src.Fields.Single(x=>x.Name==f.Name)],
 FieldReference f=>basis.ImportReference(f),
 _=>o
};
foreach(var pair in methods)
{
 var old=pair.Key; var n=pair.Value; if(!old.HasBody)continue;
 n.Body=new MethodBody(n){InitLocals=old.Body.InitLocals,MaxStackSize=old.Body.MaxStackSize};
 foreach(var v in old.Body.Variables)n.Body.Variables.Add(new VariableDefinition(T(v.VariableType)));
 var map=new Dictionary<Instruction,Instruction>();
 foreach(var i in old.Body.Instructions) { var j=Instruction.Create(OpCodes.Nop); j.OpCode=i.OpCode; n.Body.Instructions.Add(j); map.Add(i,j); }
 Instruction I(Instruction i)=>i==null?null:map[i];
 foreach(var i in old.Body.Instructions) map[i].Operand=i.Operand switch { Instruction target=>I(target), Instruction[] targets=>targets.Select(I).ToArray(), VariableDefinition v=>n.Body.Variables[v.Index], ParameterDefinition p=>n.Parameters[p.Index], _=>Import(i.Operand) };
 foreach(var e in old.Body.ExceptionHandlers)n.Body.ExceptionHandlers.Add(new ExceptionHandler(e.HandlerType){TryStart=I(e.TryStart),TryEnd=I(e.TryEnd),HandlerStart=I(e.HandlerStart),HandlerEnd=I(e.HandlerEnd),FilterStart=I(e.FilterStart),CatchType=e.CatchType==null?null:T(e.CatchType)});
}
var fast=basis.Types.Single(t=>t.FullName=="SPTBeltArmbandInventory.FastAccessSlotPatches");
var reach=fast.Methods.Single(m=>m.Name=="TryInstallReloadReachability");
var bridge=fast.Methods.Single(m=>m.Name=="TryInstallReloadCandidateBridge");
var destroy=basis.Types.Single(t=>t.FullName=="SPTBeltArmbandInventory.Plugin").Methods.Single(m=>m.Name=="OnDestroy");
var changed=new HashSet<string>{reach.FullName,bridge.FullName,destroy.FullName};
reach.Body=new MethodBody(reach); var il=reach.Body.GetILProcessor();
il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,fast.Fields.Single(f=>f.Name=="logInfo"));il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,fast.Fields.Single(f=>f.Name=="logWarning"));il.Emit(OpCodes.Call,dst.Methods.Single(m=>m.Name=="Install"));il.Emit(OpCodes.Ret);
bridge.Body=new MethodBody(bridge);il=bridge.Body.GetILProcessor();il.Emit(OpCodes.Call,dst.Methods.Single(m=>m.Name=="IsActive"));il.Emit(OpCodes.Ret);
il=destroy.Body.GetILProcessor();il.InsertBefore(destroy.Body.Instructions[0],Instruction.Create(OpCodes.Call,dst.Methods.Single(m=>m.Name=="Dispose")));
var attr=basis.Assembly.CustomAttributes.Single(a=>a.AttributeType.FullName=="System.Reflection.AssemblyInformationalVersionAttribute");
attr.ConstructorArguments[0]=new CustomAttributeArgument(basis.TypeSystem.String,"0.3.0+integrated.rc2."+args[3]);
basis.Mvid=Guid.NewGuid();
string output=Path.GetFullPath(args[2]);Directory.CreateDirectory(Path.GetDirectoryName(output));
basis.Write(output,new WriterParameters{WriteSymbols=false});
using var check=ModuleDefinition.ReadModule(output);
var after=All(check).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Fingerprint);
foreach(var p in before)if(!changed.Contains(p.Key)&&(!after.TryGetValue(p.Key,out var value)||value!=p.Value))throw new InvalidOperationException("Unintended method change: "+p.Key);
foreach(var name in typesBefore)if(!All(check).Any(t=>t.FullName==name))throw new InvalidOperationException("Lost type: "+name);
foreach(var r in check.Resources.OfType<EmbeddedResource>())if(resourceHashes.TryGetValue(r.Name,out var h)&&h!=Convert.ToHexString(SHA256.HashData(r.GetResourceData())))throw new InvalidOperationException("Changed embedded resource: "+r.Name);
if(check.AssemblyReferences.Any(r=>r.Name==donor.Assembly.Name.Name||r.Name=="BAndHB.OperationalAccess"))throw new InvalidOperationException("External repair assembly dependency");
int plugins=All(check).Count(t=>t.CustomAttributes.Any(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin"));
if(plugins!=1)throw new InvalidOperationException("Expected one BepInEx plugin, found "+plugins);
var report=new { InputSha256=InputHash, OutputSha256=Hash(output),SourceSha=args[3],PreservedOriginalMethods=before.Count-changed.Count,ModifiedOriginalMethods=changed,PreservedOriginalTypes=typesBefore.Length,EmbeddedResources=resourceHashes,BepInExPluginCount=plugins,SeparateRuntimeRepairAssembly=false,PhysicalRuntimeTest="NOT_RUN" };
File.WriteAllText(output+".verification.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
