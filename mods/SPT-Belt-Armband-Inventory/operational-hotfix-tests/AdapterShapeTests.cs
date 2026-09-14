// Shape fixtures exercise the production adapter code, NOT native EFT or real Harmony detouring.
// The production DLL is separately compiled against real BepInEx/Harmony. No fixture is shipped.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAndHB.OperationalAccess;

namespace BepInEx
{
 [AttributeUsage(AttributeTargets.Class)] public sealed class BepInPlugin : Attribute { public BepInPlugin(string id,string name,string version){} }
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public sealed class BepInDependency : Attribute { public enum DependencyFlags{HardDependency,SoftDependency} public BepInDependency(string id,DependencyFlags f){} }
 public class BaseUnityPlugin { protected FixtureLog Logger=new FixtureLog(); }
 public class FixtureLog { public void LogInfo(object x){} public void LogWarning(object x){} public void LogError(object x){} }
}
namespace BepInEx.Bootstrap
{
 public class PluginInfo { public object Instance; }
 public static class Chainloader { public static Dictionary<string,PluginInfo> PluginInfos=new Dictionary<string,PluginInfo>(); }
}
namespace HarmonyLib
{
 public static class Priority { public const int Last=0; }
 public class HarmonyMethod { public MethodInfo method; public int priority; public HarmonyMethod(MethodInfo m){method=m;} }
 public class Patch { public string owner; public MethodInfo PatchMethod; }
 public class Patches { public List<Patch> Postfixes=new List<Patch>(); }
 public class Harmony
 {
  readonly string id; static Dictionary<MethodBase,Patches> patches=new Dictionary<MethodBase,Patches>();
  public Harmony(string value){id=value;}
  public void Patch(MethodBase target,HarmonyMethod postfix){if(!patches.ContainsKey(target))patches[target]=new Patches(); patches[target].Postfixes.Add(new Patch{owner=id,PatchMethod=postfix.method});}
  public static Patches GetPatchInfo(MethodBase target)=>patches.TryGetValue(target,out var p)?p:null;
  public void UnpatchSelf(){foreach(var p in patches.Values)p.Postfixes.RemoveAll(x=>x.owner==id);}
 }
}
namespace EFT.InventoryLogic
{
 public enum EquipmentSlot{Pockets=0,Backpack=1,Belt=15}
 public class Item { public string StringTemplateId{get;set;} public Item Parent; public bool Known=true; }
 public class Grid { public List<Item> Items=new List<Item>(); }
 public class Container:Item { public Grid[] Grids=new[]{new Grid()}; public T Add<T>(T item)where T:Item{Grids[0].Items.Add(item);item.Parent=this;return item;} }
 public class Grenade:Item{}
 public class Slot { public Item ContainedItem{get;set;} }
 public class InventoryEquipment { public Slot Belt=new Slot(); public Slot GetSlot(EquipmentSlot s)=>s==EquipmentSlot.Belt?Belt:null; }
 public class Inventory
 {
  public InventoryEquipment Equipment{get;set;}=new InventoryEquipment();
  public static EquipmentSlot[] FastAccessSlots=new[]{EquipmentSlot.Pockets};
  public static EquipmentSlot[] BindAvailableSlotsExtended=new[]{EquipmentSlot.Pockets};
  public IEnumerable<Item> GetItemsInSlots(IEnumerable<EquipmentSlot> slots)=>Array.Empty<Item>();
 }
 public class InventoryController
 {
  public Inventory Inventory{get;set;}=new Inventory();
  public bool IsAtReachablePlace(Item item)=>false;
  public bool Examined(Item item)=>item.Known;
 }
 public static class NativeGrenades { public static List<Grenade> GetThrowablePriorityGrenadesList(InventoryController c)=>new List<Grenade>(); }
 public static class NativeParents { public static IEnumerable<Item> GetAllParentItems(Item i){for(var p=i.Parent;p!=null;p=p.Parent)yield return p;} }
}
namespace PackNStrap.Core.Items { public class CustomBeltItemClass:EFT.InventoryLogic.Container{} }
namespace SPTBeltArmbandInventory
{
 public static class ReloadCandidateBridgeRuntime { public static object OriginalFastAccessSlots=new EFT.InventoryLogic.EquipmentSlot[]{EFT.InventoryLogic.EquipmentSlot.Pockets}; public static object OriginalBindAvailableSlots; }
 public static class FastAccessSlotPatches
 {
  static MethodInfo FindGetAllParentItems(Type itemType)=>typeof(EFT.InventoryLogic.NativeParents).GetMethod("GetAllParentItems");
 }
}
public static class AdapterShapeTests
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception("ADAPTER FAIL: "+name);passed++;Console.WriteLine("ADAPTER PASS: "+name);}
 static object Call(string name,Type[] types,params object[] args){var m=typeof(AccessRuntime).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).MakeGenericMethod(types);m.Invoke(null,args);return args[args.Length-1];}
 [ModuleInitializer] public static void Run()
 {
  AccessRuntime.Bind(typeof(AdapterShapeTests).Assembly);
  AccessRuntime.Install(new HarmonyLib.Harmony(Plugin.Id));
  Check(AccessRuntime.Enabled,"exact fixture binding and registration");
  var controller=new EFT.InventoryLogic.InventoryController();var inventory=controller.Inventory;
  var belt=new PackNStrap.Core.Items.CustomBeltItemClass{StringTemplateId="imported-belt"};inventory.Equipment.Belt.ContainedItem=belt;
  var loose=belt.Add(new EFT.InventoryLogic.Item{StringTemplateId="ammo"});
  var pouch=belt.Add(new EFT.InventoryLogic.Container{StringTemplateId="pouch"});
  var med=pouch.Add(new EFT.InventoryLogic.Item{StringTemplateId="medicine"});
  var grenade=pouch.Add(new EFT.InventoryLogic.Grenade());var unknown=pouch.Add(new EFT.InventoryLogic.Grenade{Known=false});
  var original=new EFT.InventoryLogic.Item[]{loose};
  var slotTypes=new[]{typeof(EFT.InventoryLogic.Item),typeof(EFT.InventoryLogic.EquipmentSlot)};
  var result=(IEnumerable<EFT.InventoryLogic.Item>)Call("ItemsPostfix",slotTypes,inventory,new[]{EFT.InventoryLogic.EquipmentSlot.Belt},original);
  Check(result.Contains(med)&&result.Contains(grenade),"production grid accessors enumerate nested contents");
  Check(result.First()==loose&&result.Count(x=>x==loose)==1,"production adapter preserves priority and deduplicates");
  Check(original.Length==1,"original query result unchanged");
  var unrelated=Call("ItemsPostfix",slotTypes,inventory,new[]{EFT.InventoryLogic.EquipmentSlot.Backpack},original);
  Check(ReferenceEquals(unrelated,original),"unrelated selectors untouched");
  var fast=(IEnumerable<EFT.InventoryLogic.Item>)Call("ItemsPostfix",slotTypes,inventory,EFT.InventoryLogic.Inventory.FastAccessSlots,original);
  Check(fast.Contains(med),"known native fast-access array works without Belt value");
  var cached=(IEnumerable<EFT.InventoryLogic.Item>)Call("ItemsPostfix",slotTypes,inventory,SPTBeltArmbandInventory.ReloadCandidateBridgeRuntime.OriginalFastAccessSlots,original);
  Check(cached.Contains(med),"captured original fast-access array supported");
  var itemTypes=new[]{typeof(EFT.InventoryLogic.Item)};
  Check((bool)Call("ReachablePostfix",itemTypes,controller,med,false),"production reachability promotes nested medicine");
  var foreign=new EFT.InventoryLogic.Item();
  Check(!(bool)Call("ReachablePostfix",itemTypes,controller,foreign,false),"unrelated item not promoted");
  Check((bool)Call("ReachablePostfix",itemTypes,controller,foreign,true),"native true result never overridden");
  var nativeGrenades=new List<EFT.InventoryLogic.Grenade>();
  var grenades=(List<EFT.InventoryLogic.Grenade>)Call("GrenadesPostfix",new[]{typeof(EFT.InventoryLogic.Grenade)},controller,nativeGrenades);
  Check(grenades.SequenceEqual(new[]{grenade}),"production grenade bridge includes only examined grenades");
  Check(nativeGrenades.Count==0,"native grenade list not mutated");
  pouch.Grids[0].Items.Remove(med);med.Parent=null;
  Check(!(bool)Call("ReachablePostfix",itemTypes,controller,med,false),"removed child immediately unreachable through overlay");
  inventory.Equipment.Belt.ContainedItem=null;
  Check(ReferenceEquals(Call("ItemsPostfix",slotTypes,inventory,EFT.InventoryLogic.Inventory.FastAccessSlots,original),original),"unequipped Belt does not contribute");
  inventory.Equipment.Belt.ContainedItem=new EFT.InventoryLogic.Container{StringTemplateId="foreign-container"};
  Check(ReferenceEquals(Call("ItemsPostfix",slotTypes,inventory,EFT.InventoryLogic.Inventory.FastAccessSlots,original),original),"unknown Belt root rejected");
  AccessRuntime.Enabled=false;
  inventory.Equipment.Belt.ContainedItem=belt;
  Check(ReferenceEquals(Call("ItemsPostfix",slotTypes,inventory,EFT.InventoryLogic.Inventory.FastAccessSlots,original),original),"disabled adapter is a no-op");
  Console.WriteLine("Adapter shape fixtures: "+passed+" assertions PASS; not a physical EFT runtime test.");
 }
}
