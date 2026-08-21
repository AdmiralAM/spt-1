using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.tacticalhud.visuals", "SPT Tactical HUD Visual Layer", "1.9.0")]
    [BepInDependency("com.admiralam.spt.tacticalhud")]
    public sealed class VisualLayer : BaseUnityPlugin
    {
        static readonly Color KillPmc = new Color(.56f,.76f,.51f,1f);
        static readonly Color KillScav = new Color(.77f,.43f,.40f,1f);
        static readonly Color KillBoss = new Color(.86f,.62f,.28f,1f);
        static readonly Color KillRaider = new Color(.66f,.53f,.78f,1f);
        static readonly Color Neutral = new Color(.77f,.79f,.78f,1f);
        static readonly Color Head = new Color(.82f,.32f,.30f,1f);
        static readonly Color Water = new Color(.48f,.70f,.88f,1f);
        static readonly Color Energy = new Color(.90f,.72f,.28f,1f);

        Plugin runtime;
        HudIcons icons;
        Harmony harmony;
        Font hudFont;
        GUIStyle text;
        int dragCluster;
        Vector2 dragOffset;
        readonly Dictionary<string,FieldInfo> fields = new Dictionary<string,FieldInfo>();

        void Awake()
        {
            icons = new HudIcons();
            try
            {
                hudFont = Font.CreateDynamicFontFromOSFont(new[]{"Bahnschrift SemiCondensed","Bahnschrift","Arial Narrow","Arial"},14);
            }
            catch { hudFont = null; }
            try
            {
                harmony = new Harmony("com.admiralam.spt.tacticalhud.visuals.patch");
                MethodInfo target = typeof(Plugin).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
                MethodInfo prefix = typeof(VisualLayer).GetMethod(nameof(RuntimeOnGuiPrefix),BindingFlags.Static|BindingFlags.NonPublic);
                if (target != null && prefix != null) harmony.Patch(target,prefix:new HarmonyMethod(prefix));
            }
            catch (Exception ex) { Logger.LogWarning("HUD visual patch: "+ex.Message); }
        }

        void OnDestroy()
        {
            try { harmony?.UnpatchSelf(); } catch { }
        }

        static bool RuntimeOnGuiPrefix() { return false; }

        void Update()
        {
            if (runtime == null) runtime = FindObjectOfType<Plugin>();
        }

        void EnsureStyle(int size)
        {
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft,
                    clipping = TextClipping.Overflow,
                    padding = new RectOffset(),
                    margin = new RectOffset(),
                    richText = false
                };
                if (hudFont != null) text.font = hudFont;
            }
            text.fontSize = size;
        }

        FieldInfo F(string name)
        {
            FieldInfo f;
            if (fields.TryGetValue(name,out f)) return f;
            f = typeof(Plugin).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            fields[name] = f;
            return f;
        }

        T Value<T>(string name,T fallback=default(T))
        {
            try
            {
                FieldInfo f=F(name); if(f==null||runtime==null)return fallback;
                object v=f.GetValue(runtime); return v is T ? (T)v : fallback;
            }
            catch{return fallback;}
        }

        ConfigEntry<T> Entry<T>(string name)
        {
            try { return F(name)?.GetValue(runtime) as ConfigEntry<T>; } catch { return null; }
        }

        T Cfg<T>(string name,T fallback)
        {
            ConfigEntry<T> e=Entry<T>(name); return e==null?fallback:e.Value;
        }

        void OnGUI()
        {
            if(runtime==null)return;
            bool inRaid=Value("inRaid",false), debug=Cfg("workAlways",false), editing=Cfg("editMode",false);
            int mode=Value("mode",0);
            if((inRaid||debug)&&(mode>=1||editing)&&Cfg("popEnabled",true)) DrawPopulation(editing);
            if((inRaid||debug||Cfg("statusOutside",false))&&(mode>=2||editing)&&Cfg("statusEnabled",true)) DrawStatus(editing);
            if((inRaid||debug)&&Cfg("killEnabled",true)) DrawKillFeed(editing);
        }

        float Text(Rect root,string value,float x,float y,int size,float opacity,Color color)
        {
            if(string.IsNullOrEmpty(value))return x;
            EnsureStyle(size);
            Color main=color; main.a*=opacity;
            float w=text.CalcSize(new GUIContent(value)).x;
            Rect r=new Rect(root.x+x,root.y+y,w+5,size+7);
            Color outline=new Color(0f,0f,0f,Mathf.Clamp01(opacity*.82f));
            text.normal.textColor=outline;
            GUI.Label(new Rect(r.x-1,r.y,r.width,r.height),value,text);
            GUI.Label(new Rect(r.x+1,r.y,r.width,r.height),value,text);
            GUI.Label(new Rect(r.x,r.y-1,r.width,r.height),value,text);
            GUI.Label(new Rect(r.x,r.y+1,r.width,r.height),value,text);
            GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),value,text);
            text.normal.textColor=main;
            GUI.Label(r,value,text);
            return x+w+3;
        }

        float Icon(Rect root,string key,float x,float y,int size,float opacity,Color color,float scale=1f)
        {
            Texture2D t=icons.Get(key); if(t==null)return x;
            float px=Mathf.Max(12f,(size+7)*scale);
            Color old=GUI.color; Color c=color;c.a*=opacity;GUI.color=c;
            GUI.DrawTexture(new Rect(root.x+x,root.y+y-2,px,px),t,ScaleMode.ScaleToFit,true);
            GUI.color=old;
            return x+px+3;
        }

        void EditSurface(int id,Rect r,ConfigEntry<float> xEntry,ConfigEntry<float> yEntry,bool fromBottom)
        {
            if(!Cfg("editMode",false))return;
            Color old=GUI.color;GUI.color=new Color(1f,1f,1f,.09f);GUI.Box(r,string.Empty);GUI.color=old;
            Event e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&r.Contains(e.mousePosition)){dragCluster=id;dragOffset=e.mousePosition-new Vector2(r.x,r.y);e.Use();}
            if(e.type==EventType.MouseDrag&&dragCluster==id)
            {
                Vector2 p=e.mousePosition-dragOffset;
                p.x=Mathf.Clamp(p.x,-r.width+8,Screen.width-8);p.y=Mathf.Clamp(p.y,-r.height+6,Screen.height-6);
                if(xEntry!=null)xEntry.Value=p.x;
                if(yEntry!=null)yEntry.Value=fromBottom?Screen.height-p.y-r.height:p.y;
                e.Use();
            }
            if(e.type==EventType.MouseUp&&dragCluster==id){dragCluster=0;try{runtime.Config.Save();}catch{}e.Use();}
        }

        void DrawPopulation(bool editing)
        {
            int size=Cfg("popSize",10);float op=Cfg("popOpacity",.55f),xPos=Cfg("popX",8f),bottom=Cfg("popY",8f);
            Rect r=new Rect(xPos,Screen.height-bottom-(size+10),220,size+10);EditSurface(1,r,Entry<float>("popX"),Entry<float>("popY"),true);
            float x=0;
            Color pmc=Cfg("pmcColor",new Color(.55f,.72f,.58f));Color scav=Cfg("scavColor",new Color(.72f,.48f,.46f));Color boss=Cfg("bossColor",new Color(.78f,.60f,.38f));Color raid=Cfg("reinforcedColor",new Color(.63f,.51f,.72f));
            x=Icon(r,"usec",x,0,size,op,pmc);x=Text(r,Value("pmc",0).ToString(),x,0,size,op,pmc);
            x=Icon(r,"scav",x+4,0,size,op,scav);x=Text(r,Value("scav",0).ToString(),x,0,size,op,scav);
            x=Icon(r,"boss",x+4,0,size,op,boss);x=Text(r,Value("boss",0).ToString(),x,0,size,op,boss);
            x=Icon(r,"raider",x+4,0,size,op,raid);Text(r,Value("reinforced",0).ToString(),x,0,size,op,raid);
        }

        void DrawStatus(bool editing)
        {
            int size=Cfg("statusSize",10);float op=Cfg("statusOpacity",.55f),xPos=Cfg("statusX",8f),bottom=Cfg("statusY",24f);
            Rect r=new Rect(xPos,Screen.height-bottom-(size+10),210,size+10);EditSurface(2,r,Entry<float>("statusX"),Entry<float>("statusY"),true);
            float x=0;float hydration=Value("hydration",0f),energy=Value("energy",0f),weight=Value("weight",0f),over=Value("overweightLimit",0f),walk=Value("walkDrainLimit",0f);
            x=Icon(r,"water",x,0,size,op,Water);x=Text(r,Mathf.RoundToInt(hydration).ToString(),x,0,size,op,Neutral);
            x=Icon(r,"energy",x+5,0,size,op,Energy);x=Text(r,Mathf.RoundToInt(energy).ToString(),x,0,size,op,Neutral);
            Color wc=Cfg("weightOk",new Color(.58f,.75f,.52f));
            if(over>0&&weight>=over)wc=Cfg("weightHeavy",new Color(.78f,.68f,.39f));
            if(walk>0&&weight>=walk)wc=Cfg("weightCritical",new Color(.75f,.42f,.39f));
            x=Icon(r,"weight",x+5,0,size,op,Neutral);
            Icon(r,"weight1",x,0,size,op,wc,.88f);
        }

        void DrawKillFeed(bool editing)
        {
            int size=Cfg("killSize",10),max=Cfg("killMax",3);float op=Cfg("killOpacity",.55f),xPos=Cfg("killX",1500f),yPos=Cfg("killY",100f),life=Cfg("killLifetime",6f);
            string mode=Cfg("killMode","Normal");Rect r=new Rect(xPos,yPos,mode=="Detailed"?350:285,(size+9)*Mathf.Max(1,max));EditSurface(3,r,Entry<float>("killX"),Entry<float>("killY"),false);
            object listObj=F("kills")?.GetValue(runtime);IEnumerable list=listObj as IEnumerable;
            var entries=new List<object>();if(list!=null)foreach(object k in list)entries.Add(k);
            if(entries.Count==0)
            {
                if(editing)DrawKillRow(r,"USEC","Scav","AK-74","Head",187f,true,0,1f,mode,size,op);
                return;
            }
            int shown=0;
            for(int i=entries.Count-1;i>=0&&shown<max;i--,shown++)
            {
                object k=entries[i];float created=ToFloat(ReadMember(k,"Created"));float age=Time.unscaledTime-created;float fade=Mathf.Clamp01((life-age)/Mathf.Min(2f,life));
                DrawKillRow(r,ReadMember(k,"Killer")?.ToString(),ReadMember(k,"Victim")?.ToString(),ReadMember(k,"Weapon")?.ToString(),ReadMember(k,"Hit")?.ToString(),ToFloat(ReadMember(k,"Distance")),ToBool(ReadMember(k,"HasDistance")),shown,fade,mode,size,op);
            }
        }

        void DrawKillRow(Rect r,string killer,string victim,string weapon,string hit,float distance,bool hasDistance,int row,float fade,string mode,int size,float opacity)
        {
            float y=row*(size+9),x=0,op=opacity*fade;Color kc=RoleColor(killer),vc=RoleColor(victim);
            x=Icon(r,RoleIcon(killer),x,y,size,op,kc);x=Text(r,killer??"?",x,y,size,op,kc);
            if(mode!="Minimal")
            {
                x=Icon(r,WeaponKey(weapon),x+6,y,size,op,Neutral,1.15f);
                if(mode=="Detailed"&&!string.IsNullOrEmpty(weapon)&&weapon!="?")x=Text(r,weapon,x,y,size,op,Neutral);
            }
            x=Icon(r,RoleIcon(victim),x+7,y,size,op,vc);x=Text(r,victim??"?",x,y,size,op,vc);
            if(mode!="Minimal")
            {
                string hk=HitKey(hit);x=Icon(r,hk,x+6,y,size,op,hk=="head"?Head:Neutral,.92f);
                if(hasDistance)Text(r,Mathf.RoundToInt(distance)+"m",x+2,y,size,op,Neutral);
            }
        }

        static Color RoleColor(string role)
        {
            if(role=="USEC"||role=="BEAR"||role=="PMC")return KillPmc;if(role=="Scav")return KillScav;if(role=="Boss")return KillBoss;if(role=="Raider")return KillRaider;return Neutral;
        }
        static string RoleIcon(string role){if(role=="BEAR")return"bear";if(role=="Scav")return"scav";if(role=="Boss")return"boss";if(role=="Raider")return"raider";return"usec";}
        static string HitKey(string hit){string h=(hit??"").ToLowerInvariant();if(h.Contains("head"))return"head";if(h.Contains("arm"))return"arm";if(h.Contains("leg"))return"leg";if(h.Contains("stomach"))return"stomach";return"torso";}
        static string WeaponKey(string weapon)
        {
            string w=(weapon??"").ToLowerInvariant();
            if(w.Contains("ak")||w.Contains("rd-")||w.Contains("rpk"))return"ak";
            if(w.Contains("m4")||w.Contains("hk")||w.Contains("adar")||w.Contains("tx-15")||w.Contains("m16"))return"ar";
            if(w.Contains("mp-")||w.Contains("pp-")||w.Contains("vector")||w.Contains("ump")||w.Contains("p90"))return"smg";
            if(w.Contains("saiga-12")||w.Contains("mp-133")||w.Contains("mp-153")||w.Contains("mp-155")||w.Contains("m870"))return"shotgun";
            if(w.Contains("svd")||w.Contains("m700")||w.Contains("dvl")||w.Contains("t-5000")||w.Contains("mosin"))return"sniper";
            if(w.Contains("glock")||w.Contains("p226")||w.Contains("m9")||w.Contains("tt")||w.Contains("usp")||w.Contains("five-seven"))return"pistol";
            return"weapon";
        }

        static object ReadMember(object o,string n)
        {
            if(o==null)return null;Type t=o.GetType();
            try{PropertyInfo p=t.GetProperty(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null)return p.GetValue(o,null);}catch{}
            try{FieldInfo f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}catch{}
            return null;
        }
        static float ToFloat(object v){try{return Convert.ToSingle(v);}catch{return 0f;}}
        static bool ToBool(object v){return v is bool&&(bool)v;}
    }
}
