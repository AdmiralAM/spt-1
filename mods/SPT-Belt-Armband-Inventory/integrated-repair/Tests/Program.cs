using System.Collections;
using System.Reflection;
using EFT.InventoryLogic;
using SPTBeltArmbandInventory;
using HarmonyLib;

int count=0;
void Check(bool value,string name){ if(!value)throw new Exception("FAIL: "+name); count++;Console.WriteLine("PASS: "+name); }
var diagnostics=new List<string>();
Check(IntegratedBeltAccess.Install(diagnostics.Add,diagnostics.Add),"production binder and emitted wrapper installation: "+string.Join(" | ",diagnostics));
var magazine=new Item("mag"); var ammo=new Item("ammo");var meds=new Item("meds");var grenade=new Grenade("grenade");var unknown=new Grenade("unknown"){Known=false};
var installed=new Item("installed-mag");var weapon=new Item("weapon"){Slots=new[]{installed}};
var pouch=new Item("pouch"){Grids=new[]{new Grid(ammo,meds,grenade,unknown)}};
var belt=new Item("68ac0000000000000000000c"){Grids=new[]{new Grid(magazine,pouch,weapon)}};
var inventory=new Inventory();inventory.Equipment.Belt.ContainedItem=belt;var controller=new InventoryController{Inventory=inventory};
var native=new Item[]{new Item("rig"),magazine};
object[] args1={inventory,Inventory.FastAccessSlots,native};
Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);
var found=((IEnumerable<Item>)args1[2]).ToArray();
Check(ReferenceEquals(found[0],native[0]),"native candidate priority preserved");
Check(found.Count(i=>ReferenceEquals(i,magazine))==1,"duplicate magazine removed only from extension");
Check(found.Contains(ammo)&&found.Contains(meds),"loose ammo and medicine inside pouch discovered");
Check(found.Contains(grenade),"nested grenade discovered");
Check(!found.Contains(installed),"weapon slot children excluded");
Check(native.Length==2,"native array not mutated");
object[] reachable={controller,meds,false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);
Check((bool)reachable[2],"emitted bool-ref postfix promotes nested medicine");
reachable=new object[]{controller,new Item("foreign"),false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check(!(bool)reachable[2],"unrelated inventory item not promoted");
reachable=new object[]{controller,installed,false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check(!(bool)reachable[2],"installed weapon magazine not reachable");
reachable=new object[]{controller,belt,false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check(!(bool)reachable[2],"equipped root itself not promoted");
reachable=new object[]{controller,new Item("native"),true};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check((bool)reachable[2],"native true result unchanged");
var oldGrenades=new List<Grenade>();object[] ga={controller,oldGrenades};Harmony.Patches["GetThrowablePriorityGrenadesList"].Invoke(null,ga);
Check(((List<Grenade>)ga[1]).SequenceEqual(new[]{grenade}),"emitted typed-list postfix includes examined grenades only");Check(oldGrenades.Count==0,"native grenade list unmodified");
var oldSlots=new List<Slot>();object[] sa={inventory.Equipment,oldSlots};Harmony.Patches["get_GrenadeThrowingSlots"].Invoke(null,sa);
Check(((List<Slot>)sa[1]).Single()==inventory.Equipment.Belt,"Belt added to grenade slot copy");Check(oldSlots.Count==0,"native grenade slot list unmodified");
object[] untouched={inventory,new[]{EquipmentSlot.Backpack},native};Harmony.Patches["GetItemsInSlots"].Invoke(null,untouched);Check(ReferenceEquals(untouched[2],native),"unrelated selector is no-op");
belt.Grids=new[]{new Grid(magazine)};reachable=new object[]{controller,meds,false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check(!(bool)reachable[2],"removed pouch immediately loses reachability");
inventory.Equipment.Belt.ContainedItem=null;args1=new object[]{inventory,Inventory.FastAccessSlots,native};Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);Check(ReferenceEquals(args1[2],native),"unequipped belt no-op");
inventory.Equipment.Belt.ContainedItem=new Item("unknown-root"){Grids=new[]{new Grid(meds)}};reachable=new object[]{controller,meds,false};Harmony.Patches["IsAtReachablePlace"].Invoke(null,reachable);Check(!(bool)reachable[2],"unknown belt rejected");
inventory.Equipment.Belt.ContainedItem=belt;belt.Grids=new[]{new Grid(belt)};args1=new object[]{inventory,Inventory.FastAccessSlots,native};Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);Check(ReferenceEquals(args1[2],native),"cycle fails closed without partial result");
belt.Grids=new[]{new Grid(meds,meds)};args1=new object[]{inventory,Inventory.FastAccessSlots,native};Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);Check(ReferenceEquals(args1[2],native),"duplicate graph node fails closed");
var deep=meds;for(int i=0;i<10;i++)deep=new Item("p"){Grids=new[]{new Grid(deep)}};belt.Grids=new[]{new Grid(deep)};args1=new object[]{inventory,Inventory.FastAccessSlots,native};Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);Check(ReferenceEquals(args1[2],native),"depth bounded");
belt.Grids=new[]{new Grid(Enumerable.Range(0,520).Select(i=>new Item("x")).ToArray())};args1=new object[]{inventory,Inventory.FastAccessSlots,native};Harmony.Patches["GetItemsInSlots"].Invoke(null,args1);Check(ReferenceEquals(args1[2],native),"item count bounded");
IntegratedBeltAccess.Dispose();Check(!IntegratedBeltAccess.IsActive(),"dispose disables primary-assembly access owner");
Console.WriteLine($"Integrated production code + emitted wrapper fixtures: {count} assertions PASS; no native EFT/Harmony detour or physical raid exercised.");

namespace EFT.InventoryLogic
{
 public enum EquipmentSlot{Pockets=0,Backpack=1,ArmBand=14,Belt=15}
 public class Item{public string StringTemplateId;public Grid[] Grids;public Item[] Slots;public Item(string id){StringTemplateId=id;}}
 public class Grenade:Item{public bool Known=true;public Grenade(string id):base(id){}}
 public class Grid{public IEnumerable<Item> Items;public Grid(params Item[] items){Items=items;}}
 public class Slot{public Item ContainedItem;}
 public class InventoryEquipment{public Slot Belt=new();public Slot GetSlot(EquipmentSlot slot)=>slot==EquipmentSlot.Belt?Belt:null;public List<Slot> GrenadeThrowingSlots=>new();}
 public class Inventory{public static EquipmentSlot[] FastAccessSlots={EquipmentSlot.Pockets};public static EquipmentSlot[] BindAvailableSlotsExtended={EquipmentSlot.Pockets};public InventoryEquipment Equipment=new();public IEnumerable<Item> GetItemsInSlots(IEnumerable<EquipmentSlot> slots)=>Array.Empty<Item>();}
 public class InventoryController{public Inventory Inventory;public bool IsAtReachablePlace(Item item)=>false;public bool Examined(Item item)=>item is not Grenade g||g.Known;}
 public static class GrenadeExtensions{public static List<Grenade> GetThrowablePriorityGrenadesList(InventoryController controller)=>new();}
}
namespace HarmonyLib
{
 public class HarmonyMethod{public MethodInfo Method;public int priority;public HarmonyMethod(MethodInfo method){Method=method;}}
 public class Harmony{public static Dictionary<string,MethodInfo>Patches=new();public Harmony(string id){} public void Patch(MethodBase original,HarmonyMethod prefix=null,HarmonyMethod postfix=null){Patches.Add(original.Name,postfix.Method);}public void UnpatchSelf(){Patches.Clear();}}
}
