using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BAndHB.OperationalAccess;
class Node { public string Name; public List<Node> Children = new List<Node>(); public Node Parent; public Node(string name) { Name=name; } public Node Add(Node n) { Children.Add(n); n.Parent=this; return n; } }
class OneShot<T> : IEnumerable<T> { readonly IEnumerable<T> data; bool read; public OneShot(IEnumerable<T> d) { data=d; } public IEnumerator<T> GetEnumerator() { if(read) throw new Exception("second enumeration"); read=true; return data.GetEnumerator(); } IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator(); }
static class Program
{
 static int assertions;
 static void Assert(bool result, string name) { if(!result) throw new Exception("FAIL: "+name); assertions++; Console.WriteLine("PASS: "+name); }
 static IEnumerable Children(object n) => ((Node)n).Children;
 static IEnumerable Parents(object n) { var p=((Node)n).Parent; for(int i=0;p!=null && i<20;i++,p=p.Parent) yield return p; }
 static void Main()
 {
  var belt=new Node("belt"); var mag=belt.Add(new Node("magazine")); var ammo=belt.Add(new Node("ammo")); var pouch=belt.Add(new Node("pouch")); var grenade=pouch.Add(new Node("grenade")); var medical=pouch.Add(new Node("medical")); var nested=pouch.Add(new Node("nested")); var nestedAmmo=nested.Add(new Node("nested ammo"));
  Assert(BeltAccessCore.TryCollect(belt,Children,out var items),"grid traversal succeeds");
  Assert(items.Count==7,"direct and nested contents included exactly once");
  Assert(!items.Contains(belt),"equipped root not returned as a consumable");
  Assert(items[0]==mag && items[1]==ammo,"direct grid order retained");
  Assert(items.Contains(grenade)&&items.Contains(medical)&&items.Contains(nestedAmmo),"grenades medicine and ammunition in pouches included");
  Assert(BeltAccessCore.IsGridDescendant(mag,belt,Parents,Children),"direct magazine reachable");
  Assert(BeltAccessCore.IsGridDescendant(nestedAmmo,belt,Parents,Children),"nested ammunition reachable");
  Assert(!BeltAccessCore.IsGridDescendant(belt,belt,Parents,Children),"root excluded from reachability promotion");
  var stash=new Node("stash"); var foreign=stash.Add(new Node("other ammo"));
  Assert(!BeltAccessCore.IsGridDescendant(foreign,belt,Parents,Children),"stash never promoted");
  var weapon=belt.Add(new Node("weapon")); var installedMag=new Node("installed mag"){Parent=weapon};
  Assert(!BeltAccessCore.IsGridDescendant(installedMag,belt,Parents,Children),"weapon slot is not a storage-grid path");
  Assert(BeltAccessCore.TryCollect(belt,Children,out items)&&!items.Contains(installedMag),"installed weapon magazine not enumerated");
  var pocket=new Node("pocket magazine"); var vanilla=new List<Node>{pocket,mag};
  var merged=BeltAccessCore.Merge(vanilla,items).ToList();
  Assert(merged[0]==pocket&&merged[1]==mag,"vanilla priority prefix unchanged");
  Assert(merged.Count(x=>x==mag)==1,"no duplicate candidates");
  Assert(vanilla.Count==2,"native list not mutated");
  var duplicates=new List<Node>{pocket,pocket};
  Assert(BeltAccessCore.Merge(duplicates,new List<object>{pocket,mag}).Take(2).SequenceEqual(duplicates),"native duplicate prefix preserved");
  Assert(ReferenceEquals(BeltAccessCore.Merge(vanilla,new List<object>()),vanilla),"empty extension is a no-op");
  Assert(BeltAccessCore.Merge(new OneShot<Node>(vanilla),new List<object>{mag}).Count()==2,"one-shot native enumerable not consumed twice");
  Assert(!BeltAccessCore.TryCollect(null,Children,out items)&&items.Count==0,"missing equipped belt fails closed");
  Assert(!BeltAccessCore.TryCollect(belt,_=>throw new Exception("shape"),out items)&&items.Count==0,"unknown grid shape fails closed without partial result");
  var cycle=new Node("cycle");cycle.Add(cycle);
  Assert(!BeltAccessCore.TryCollect(cycle,Children,out items)&&items.Count==0,"cycles rejected");
  var dupe=new Node("duplicate");dupe.Children.Add(mag);dupe.Children.Add(mag);
  Assert(!BeltAccessCore.TryCollect(dupe,Children,out items)&&items.Count==0,"repeated graph identities rejected");
  var deep=new Node("deep");var current=deep;for(int i=0;i<9;i++)current=current.Add(new Node("n"));
  Assert(!BeltAccessCore.TryCollect(deep,Children,out items)&&items.Count==0,"depth budget enforced");
  var many=new Node("many");for(int i=0;i<257;i++)many.Add(new Node("n"));
  Assert(!BeltAccessCore.TryCollect(many,Children,out items)&&items.Count==0,"item budget enforced");
  Assert(BeltAccessCore.TryCollect(new Node("empty"),Children,out items)&&items.Count==0,"empty equipped belt valid");
  belt.Children.Remove(pouch);pouch.Parent=stash;
  Assert(!BeltAccessCore.IsGridDescendant(nestedAmmo,belt,Parents,Children),"removed pouch loses reachability immediately");
  Assert(BeltAccessCore.TryCollect(belt,Children,out items)&&!items.Contains(nestedAmmo),"removed pouch not retained in a stale cache");
  Console.WriteLine("B&A&HB operational access: "+assertions+" assertions PASS; physical SPT runtime not exercised.");
 }
}
