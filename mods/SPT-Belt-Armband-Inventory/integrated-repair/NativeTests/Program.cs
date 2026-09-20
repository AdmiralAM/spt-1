using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using EFT.InventoryLogic;
using HarmonyLib;
using SPTBeltArmbandInventory;
int passed=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
var logs=new List<string>();
Check(NativeBeltActions.Install(logs.Add,logs.Add),"complete production binder/transpiler execution: "+string.Join(" | ",logs));
Check(Harmony.Replacements.Count==8,"eight concrete native caller boundaries covered");
Check(Harmony.Replacements.Values.Sum(x=>x.Count(m=>!m.Name.Contains("unused")))>=10,"nine inventory call sites plus native sort wrapped");
var rig=new Magazine("rig");var spare=new Magazine("spare");var ammo=new Ammo("round");var rejected=new Ammo("wrong");
var pouch=new Item("pouch"){Grids=new[]{new Grid(ammo,rejected)}};
var weapon=new Item("weapon"){Slots=new[]{new Magazine("mounted")}};
var belt=new Item("68ac0000000000000000000c"){Grids=new[]{new Grid(spare,pouch,weapon)}};
var inventory=new Inventory();inventory.Equipment.Belt.ContainedItem=belt;
var controller=new InventoryController{Inventory=inventory,NativeItems=new Item[]{rig}};
var external=Harmony.Replacements["EFT.FirearmHandsInputTranslator.ReloadExternalMagazine"];
var mags=new List<Magazine>();Predicate<Magazine> allow=_=>true;
external[0].Invoke(null,new object[]{controller,mags,allow});
Check(controller.NativeCalls==1,"original native generic enumeration executed exactly once");
Check(mags.SequenceEqual(new[]{rig,spare}),"native-first direct belt magazine candidates");
external[1].Invoke(null,new object[]{mags,new Comparison<Magazine>((a,b)=>a==spare?-1:b==spare?1:0)});
Check(mags.SequenceEqual(new[]{rig,spare}),"native sorting retained inside groups without prioritizing belt over rig");
var ammunition=new List<Ammo>();
var ammoWrapper=Harmony.Replacements["EFT.FirearmHandsInputTranslator.ReloadWithAmmo"][0];
ammoWrapper.Invoke(null,new object[]{controller,ammunition,new Predicate<Ammo>(a=>a.StringTemplateId=="round")});
Check(ammunition.SequenceEqual(new[]{ammo}),"nested loose ammunition found and exact native predicate respected");
Check(controller.NativeCalls==2,"no fallback retry of original reload query");
controller.NativeItems=new Item[]{rig,spare};mags.Clear();external[0].Invoke(null,new object[]{controller,mags,allow});
Check(mags.Count==2,"already-native belt candidate is not duplicated");
var seq=Harmony.Replacements["EFT.FirearmHandsInputTranslator.Reload"][0].Invoke(null,new object[]{controller,new Predicate<Ammo>(a=>a==ammo)});
Check(((IEnumerable<Ammo>)seq).SequenceEqual(new[]{ammo}),"returning generic reload path receives nested ammo");
var menu=Harmony.Replacements["EFT.UI.ItemUiContext.FindCompatibleAmmo"];
var items=new List<Item>{ammo};menu[0].Invoke(null,new object[]{new Item("stash"),items});
Check(items.Count==1,"stash inventory is not widened by equipment-only packing integration");
menu[1].Invoke(null,new object[]{inventory.Equipment,items});
Check(items.Count==2&&items.Contains(rejected)&&!items.Contains(spare),"menu enumeration adds actual ammo instances only and deduplicates before native filters");
var actual=new List<Ammo>();
Harmony.Replacements["EFT.UI.ItemUiContext+CG_MoveNext1+Struct1069.MoveNext"][0].Invoke(null,new object[]{inventory.Equipment,actual});
Check(actual.SequenceEqual(new[]{ammo,rejected}),"actual asynchronous magazine-loading path receives same ammo objects as menu");
Check(!items.Contains(weapon),"packing does not include weapons or mounted magazines");
ammunition.Clear();ammoWrapper.Invoke(null,new object[]{controller,ammunition,new Predicate<Ammo>(_=>throw new InvalidOperationException("predicate"))});
Check(ammunition.Count==0,"predicate exception retains original result without partial belt additions");
belt.Grids=new[]{new Grid(spare)};ammunition.Clear();ammoWrapper.Invoke(null,new object[]{controller,ammunition,new Predicate<Ammo>(_=>true)});
Check(ammunition.Count==0,"removed pouch stops supplying ammo immediately");
inventory.Equipment.Belt.ContainedItem=null;mags.Clear();controller.NativeItems=new Item[]{rig};external[0].Invoke(null,new object[]{controller,mags,allow});
Check(mags.SequenceEqual(new[]{rig}),"unequipped belt preserves native-only reload");
inventory.Equipment.Belt.ContainedItem=belt;belt.Grids=new[]{new Grid(belt)};mags.Clear();external[0].Invoke(null,new object[]{controller,mags,allow});
Check(mags.SequenceEqual(new[]{rig}),"cyclic private storage fails closed preserving native result");
Check(Harmony.LabelsPreserved,"transpiler instruction copies preserve labels");
NativeBeltActions.Dispose();Check(!NativeBeltActions.IsActive(),"both integrated owners disabled on disposal");
Console.WriteLine($"Native action production/transpiler/wrapper fixtures: {passed} assertions PASS. Actual game CIL inspected separately; these fixtures are not real Harmony detours or a raid.");

namespace EFT.InventoryLogic
{
 public enum EquipmentSlot{Pockets=0,Backpack=1,ArmBand=14,Belt=15}
 public class Item{public string StringTemplateId;public Grid[] Grids;public Item[] Slots;public Item(string id){StringTemplateId=id;}}
 public class Ammo:Item{public Ammo(string id):base(id){}}
 public class Magazine:Item{public Magazine(string id):base(id){}}
 public class Grenade:Item{public Grenade(string id):base(id){}}
 public class Grid{public IEnumerable<Item> Items;public Grid(params Item[] items){Items=items;}}
 public class Slot{public Item ContainedItem;}
 public class InventoryEquipment:Item{public InventoryEquipment():base("equipment"){}public Slot Belt=new();public Slot GetSlot(EquipmentSlot slot)=>slot==EquipmentSlot.Belt?Belt:null;public List<Slot> GrenadeThrowingSlots=>new();}
 public class Inventory{public static EquipmentSlot[] FastAccessSlots={EquipmentSlot.Pockets};public static EquipmentSlot[] BindAvailableSlotsExtended={EquipmentSlot.Pockets};public InventoryEquipment Equipment=new();public IEnumerable<Item> GetItemsInSlots(IEnumerable<EquipmentSlot> slots)=>Array.Empty<Item>();}
 public class InventoryController
 {
  public Inventory Inventory;public Item[] NativeItems=Array.Empty<Item>();public int NativeCalls;
  public bool IsAtReachablePlace(Item item)=>false;public bool Examined(Item item)=>true;
  public void GetReachableItemsOfTypeNonAlloc<T>(IList<T> destination,Predicate<T> filter)where T:Item{NativeCalls++;foreach(var item in NativeItems.OfType<T>())if(filter==null||filter(item))destination.Add(item);}
  public IEnumerable<T> GetReachableItemsOfType<T>(Predicate<T> filter)where T:Item{NativeCalls++;return NativeItems.OfType<T>().Where(i=>filter==null||filter(i)).ToList();}
 }
 public static class GrenadeExtensions{public static List<Grenade> GetThrowablePriorityGrenadesList(InventoryController c)=>new();}
 public static class ItemExtensions{public static void GetAllAssembledItemsNonAlloc(Item root,List<Item> result){}public static void GetAllAssembledItems<T>(Item root,List<T> result)where T:Item{}}
}
namespace EFT
{
 public class FirearmHandsInputTranslator
 {
  public void Reload(){}public void ReloadBarrels(Item weapon){}public void ReloadRevolverDrum(Item weapon,bool value){}public void ReloadWithAmmo(Item weapon){}public void ReloadExternalMagazine(Item weapon,bool value){}
  public class CG_LoadAmmoToChamber{public void method_0(List<Ammo> list,Item weapon){}}
 }
}
namespace EFT.UI
{
 public class ItemUiContext{public void FindCompatibleAmmo(Magazine mag){}public class CG_MoveNext1{public struct Struct1069{public void MoveNext(){}}}}
}
namespace HarmonyLib
{
 public class CodeInstruction{public OpCode opcode;public object operand;public List<int> labels=new();public CodeInstruction(OpCode op,object value){opcode=op;operand=value;labels.Add(71);}public CodeInstruction(CodeInstruction other){opcode=other.opcode;operand=other.operand;labels=new(other.labels);}}
 public class HarmonyMethod{public MethodInfo Method;public int priority;public HarmonyMethod(MethodInfo method){Method=method;}}
 public class Harmony
 {
  public static Dictionary<string,List<MethodInfo>> Replacements=new();public static bool LabelsPreserved=true;string id;public Harmony(string owner){id=owner;}
  public void Patch(MethodBase original,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null)
  {
   if(transpiler==null)return;
   var input=new List<CodeInstruction>();var type=typeof(InventoryController);string name=original.Name;
   if(original.DeclaringType.FullName.StartsWith("EFT.FirearmHandsInputTranslator"))
   {
    var element=name=="ReloadExternalMagazine"?typeof(Magazine):typeof(Ammo);
    var query=type.GetMethod(name=="Reload"?"GetReachableItemsOfType":"GetReachableItemsOfTypeNonAlloc").MakeGenericMethod(element);
    input.Add(new CodeInstruction(OpCodes.Callvirt,query));
    if(name=="ReloadExternalMagazine")input.Add(new CodeInstruction(OpCodes.Callvirt,typeof(List<Magazine>).GetMethod("Sort",new[]{typeof(Comparison<Magazine>)})));
   }
   else if(name=="FindCompatibleAmmo")
   {
    var query=typeof(ItemExtensions).GetMethod("GetAllAssembledItemsNonAlloc");input.Add(new CodeInstruction(OpCodes.Call,query));input.Add(new CodeInstruction(OpCodes.Call,query));
   }
   else input.Add(new CodeInstruction(OpCodes.Call,typeof(ItemExtensions).GetMethod("GetAllAssembledItems").MakeGenericMethod(typeof(Ammo))));
   var output=(IEnumerable<CodeInstruction>)transpiler.Method.Invoke(null,new object[]{input,original});
   var rewritten=output.ToList();LabelsPreserved&=rewritten.All(x=>x.labels.SequenceEqual(new[]{71}));
   Replacements.Add(original.DeclaringType.FullName+"."+name,rewritten.Select(x=>(MethodInfo)x.operand).ToList());
  }
  public void UnpatchSelf(){}
 }
}
