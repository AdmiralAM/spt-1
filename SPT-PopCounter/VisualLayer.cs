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
    [BepInPlugin("com.admiralam.spt.tacticalhud.visuals", "SPT Tactical HUD Visual Layer", "1.10.0")]
    [BepInDependency("com.admiralam.spt.tacticalhud")]
    public sealed class VisualLayer : BaseUnityPlugin
    {
        static readonly Color KillPmc = new Color(.56f,.76f,.51f,1f);
        static readonly Color KillScav = new Color(.77f,.43f,.40f,1f);
        static readonly Color KillBoss = new Color(.86f,.62f,.28f,1f);
        static readonly Color KillRaider = new Color(.66f,.53f,.78f,1f);
        static readonly Color Neutral = new Color(.78f,.80f,.79f,1f);
        static readonly Color Muted = new Color(.62f,.64f,.63f,1f);
        static readonly Color Head = new Color(.82f,.32f,.30f,1f);
        static readonly Color Water = new Color(.49f,.69f,.86f,1f);
        static readonly Color Energy = new Color(.86f,.70f,.31f,1f);

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
            try { hudFont = Font.CreateDynamicFontFromOSFont(new[]{"Bahnschrift SemiCondensed","Bahnschrift","Arial Narrow","Arial"},14); }
            catch { hudFont = null; }
            try
            {
                harmony = new Harmony("com.admiralam.spt.tacticalhud.visuals.patch");
                MethodInfo target = typeof(Plugin).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
                MethodInfo prefix = typeof(VisualLayer).GetMethod(nameof(RuntimeOnGuiPrefix),BindingFlags.Static|BindingFlags.NonPublic);
                if(target!=null&&prefix!=null) harmony.Patch(target,prefix:new HarmonyMethod(prefix));
            }
            catch(Exception ex){Logger.LogWarning("HUD visual patch: "+ex.Message);}
        }

        void OnDestroy(){try{harmony?.UnpatchSelf();}catch{}}
        static bool RuntimeOnGuiPrefix(){return false;}
        void Update(){if(runtime==null)runtime=FindObjectOfType<Plugin>();}

        void EnsureStyle(int size)
        {
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Normal,alignment=TextAnchor.UpperLeft,clipping=TextClipping.Overflow,padding=new RectOffset(),margin=new RectOffset(),richText=false};
                if(hudFont!=null)text.font=hudFont;
            }
            text.fontSize=size;
        }

        FieldInfo F(string name){FieldInfo f;if(fields.TryGetValue(name,out f))return f;f=typeof(Plugin).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);fields[name]=f;return f;}
        T Value<T>(string name,T fallback=default(T)){try{FieldInfo f=F(name);if(f==null||runtime==null)return fallback;object v=f.GetValue(runtime);return v is T?(T)v:fallback;}catch{return fallback;}}
        ConfigEntry<T> Entry<T>(string name){try{return F(name)?.GetValue(runtime) as ConfigEntry<T>;}catch{return null;}}
        T Cfg<T>(string name,T fallback){ConfigEntry<T> e=Entry<T>(name);return e==null?fallback:e.Value;}

        void OnGUI()
        {
            if(runtime==null)return;
            bool inRaid=Value("inRaid",false),debug=Cfg("workAlways",false),editing=Cfg("editMode",false);int mode=Value("mode",0);
            if((inRaid||debug)&&(mode>=1||editing)&&Cfg("popEnabled",true))DrawPopulation(editing);
            if((inRaid||debug||Cfg("statusOutside",false))&&(mode>=2||editing)&&Cfg("statusEnabled",true))DrawStatus(editing);
            if((inRaid||debug)&&Cfg("killEnabled",true))DrawKillFeed(editing);
        }

        float Text(Rect root,string value,float x,float y,int size,float opacity,Color color,float alphaScale=1f)
        {
            if(string.IsNullOrEmpty(value))return x;
            EnsureStyle(size);float effective=Mathf.Clamp01(opacity*alphaScale);Color main=color;main.a*=effective;
            float w=text.CalcSize(new GUIContent(value)).x;Rect r=new Rect(root.x+x,root.y+y,w+4,size+7);
            Color old=text.normal.textColor;
            text.normal.textColor=new Color(0,0,0,Mathf.Clamp01(effective*.88f));
            int[,] o={{-1,0},{1,0},{0,-1},{0,1},{-1,-1},{1,-1},{-1,1},{1,1}};
            for(int i=0;i<8;i++)GUI.Label(new Rect(r.x+o[i,0],r.y+o[i,1],r.width,r.height),value,text);
            text.normal.textColor=new Color(0,0,0,Mathf.Clamp01(effective*.48f));
            GUI.Label(new Rect(r.x+2,r.y+2,r.width,r.height),value,text);
            text.normal.textColor=main;GUI.Label(r,value,text);text.normal.textColor=old;
            return x+w+3;
        }

        float Icon(Rect root,string key,float x,float y,int size,float opacity,Color color,float scale=1f)
        {
            Texture2D t=icons.Get(key);if(t==null)return x;float px=Mathf.Max(11f,(size+6)*scale);Rect rr=new Rect(root.x+x,root.y+y-2,px,px);
            Color old=GUI.color;GUI.color=new Color(0,0,0,Mathf.Clamp01(opacity*.68f));GUI.DrawTexture(new Rect(rr.x+1,rr.y+1,rr.width,rr.height),t,ScaleMode.ScaleToFit,true);
            Color c=color;c.a*=opacity;GUI.color=c;GUI.DrawTexture(rr,t,ScaleMode.ScaleToFit,true);GUI.color=old;return x+px+2;
        }

        float Sep(Rect root,float x,float y,int size,float opacity)
        {
            return Text(root,"·",x+2,y-1,size,opacity,Muted,.72f)+1;
        }

        void EditSurface(int id,Rect r,ConfigEntry<float>xEntry,ConfigEntry<float>yEntry,bool fromBottom)
        {
            if(!Cfg("editMode",false))return;Color old=GUI.color;GUI.color=new Color(1f,1f,1f,.055f);GUI.Box(r,string.Empty);GUI.color=old;Event e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&r.Contains(e.mousePosition)){dragCluster=id;dragOffset=e.mousePosition-new Vector2(r.x,r.y);e.Use();}
            if(e.type==EventType.MouseDrag&&dragCluster==id){Vector2 p=e.mousePosition-dragOffset;p.x=Mathf.Clamp(p.x,-r.width+8,Screen.width-8);p.y=Mathf.Clamp(p.y,-r.height+6,Screen.height-6);if(xEntry!=null)xEntry.Value=p.x;if(yEntry!=null)yEntry.Value=fromBottom?Screen.height-p.y-r.height:p.y;e.Use();}
            if(e.type==EventType.MouseUp&&dragCluster==id){dragCluster=0;try{runtime.Config.Save();}catch{}e.Use();}
        }

        void DrawPopulation(bool editing)
        {
            int size=Cfg("popSize",10);float op=Cfg("popOpacity",.55f),xPos=Cfg("popX",8f),bottom=Cfg("popY",8f);Rect r=new Rect(xPos,Screen.height-bottom-(size+9),184,size+9);EditSurface(1,r,Entry<float>("popX"),Entry<float>("popY"),true);float x=0;
            Color pmc=Cfg("pmcColor",new Color(.55f,.72f,.58f)),scav=Cfg("scavColor",new Color(.72f,.48f,.46f)),boss=Cfg("bossColor",new Color(.78f,.60f,.38f)),raid=Cfg("reinforcedColor",new Color(.63f,.51f,.72f));
            x=Icon(r,"usec",x,0,size,op,pmc,.88f);x=Text(r,Value("pmc",0).ToString(),x,0,size,op,Neutral);x=Sep(r,x,0,size,op);
            x=Icon(r,"scav",x,0,size,op,scav,.88f);x=Text(r,Value("scav",0).ToString(),x,0,size,op,Neutral);x=Sep(r,x,0,size,op);
            x=Icon(r,"boss",x,0,size,op,boss,.88f);x=Text(r,Value("boss",0).ToString(),x,0,size,op,Neutral);x=Sep(r,x,0,size,op);
            x=Icon(r,"raider",x,0,size,op,raid,.88f);Text(r,Value("reinforced",0).ToString(),x,0,size,op,Neutral);
        }

        void DrawStatus(bool editing)
        {
            int size=Cfg("statusSize",10);float op=Cfg("statusOpacity",.55f),xPos=Cfg("statusX",8f),bottom=Cfg("statusY",24f);Rect r=new Rect(xPos,Screen.height-bottom-(size+9),154,size+9);EditSurface(2,r,Entry<float>("statusX"),Entry<float>("statusY"),true);float x=0;
            float hydration=Value("hydration",0f),energy=Value("energy",0f),weight=Value("weight",0f),over=Value("overweightLimit",0f),walk=Value("walkDrainLimit",0f);
            x=Icon(r,"water",x,0,size,op,Water,.88f);x=Text(r,Mathf.RoundToInt(hydration).ToString(),x,0,size,op,Neutral);x=Sep(r,x,0,size,op);
            x=Icon(r,"energy",x,0,size,op,Energy,.88f);x=Text(r,Mathf.RoundToInt(energy).ToString(),x,0,size,op,Neutral);x=Sep(r,x,0,size,op);
            Color wc=Cfg("weightOk",new Color(.58f,.75f,.52f));if(over>0&&weight>=over)wc=Cfg("weightHeavy",new Color(.78f,.68f,.39f));if(walk>0&&weight>=walk)wc=Cfg("weightCritical",new Color(.75f,.42f,.39f));
            x=Icon(r,"weight",x,0,size,op,Muted,.84f);Icon(r,"weight1",x,0,size,op,wc,.72f);
        }

        void DrawKillFeed(bool editing)
        {
            int size=Cfg("killSize",10),max=Cfg("killMax",3);float op=Cfg("killOpacity",.55f),xPos=Cfg("killX",1500f),yPos=Cfg("killY",100f),life=Cfg("killLifetime",6f);string mode=Cfg("killMode","Normal");
            object listObj=F("kills")?.GetValue(runtime);IEnumerable list=listObj as IEnumerable;var entries=new List<object>();if(list!=null)foreach(object k in list)entries.Add(k);
            int rows=editing?Mathf.Max(1,Mathf.Min(max,entries.Count==0?1:entries.Count)):Mathf.Max(1,Mathf.Min(max,entries.Count));float width=mode=="Detailed"?280f:205f;Rect r=new Rect(xPos,yPos,width,(size+7)*rows);EditSurface(3,r,Entry<float>("killX"),Entry<float>("killY"),false);
            if(entries.Count==0){if(editing)DrawKillRow(r,"USEC","Scav","AK-74","Head",187f,true,0,1f,mode,size,op);return;}
            int shown=0;for(int i=entries.Count-1;i>=0&&shown<max;i--,shown++){object k=entries[i];float created=ToFloat(ReadMember(k,"Created")),age=Time.unscaledTime-created,fade=Mathf.Clamp01((life-age)/Mathf.Min(2f,life));DrawKillRow(r,ReadMember(k,"Killer")?.ToString(),ReadMember(k,"Victim")?.ToString(),CleanWeapon(ReadMember(k,"Weapon")?.ToString()),ReadMember(k,"Hit")?.ToString(),ToFloat(ReadMember(k,"Distance")),ToBool(ReadMember(k,"HasDistance")),shown,fade,mode,size,op);}
        }

        void DrawKillRow(Rect r,string killer,string victim,string weapon,string hit,float distance,bool hasDistance,int row,float fade,string mode,int size,float opacity)
        {
            float y=row*(size+7),x=0,op=opacity*fade;Color kc=RoleColor(killer),vc=RoleColor(victim);
            x=Icon(r,RoleIcon(killer),x,y,size,op,kc,.82f);x=Text(r,ShortRole(killer),x,y,size,op,kc);x=Sep(r,x,y,size,op);
            if(mode!="Minimal"){x=Icon(r,WeaponKey(weapon),x,y,size,op,Neutral,.90f);if(mode=="Detailed"&&weapon!="?"){x=Text(r,weapon,x,y,size-1,op,Neutral,.94f);x=Sep(r,x,y,size,op);}}
            x=Icon(r,RoleIcon(victim),x,y,size,op,vc,.82f);x=Text(r,ShortRole(victim),x,y,size,op,vc);
            if(mode!="Minimal"){x=Sep(r,x,y,size,op);string hk=HitKey(hit);x=Icon(r,hk,x,y,size,op,hk=="head"?Head:Muted,.78f);if(hasDistance)Text(r,Mathf.RoundToInt(distance)+"m",x+1,y,size-1,op,Neutral,.90f);}
        }

        static string ShortRole(string r){if(r=="USEC")return"USEC";if(r=="BEAR")return"BEAR";if(r=="Scav")return"SCAV";if(r=="Boss")return"BOSS";if(r=="Raider")return"RAID";if(r=="PMC")return"PMC";return"?";}
        static Color RoleColor(string role){if(role=="USEC"||role=="BEAR"||role=="PMC")return KillPmc;if(role=="Scav")return KillScav;if(role=="Boss")return KillBoss;if(role=="Raider")return KillRaider;return Neutral;}
        static string RoleIcon(string role){if(role=="BEAR")return"bear";if(role=="Scav")return"scav";if(role=="Boss")return"boss";if(role=="Raider")return"raider";return"usec";}
        static string HitKey(string hit){string h=(hit??"").ToLowerInvariant();if(h.Contains("head"))return"head";if(h.Contains("arm"))return"arm";if(h.Contains("leg"))return"leg";if(h.Contains("stomach"))return"stomach";return"torso";}

        static string CleanWeapon(string raw)
        {
            if(string.IsNullOrWhiteSpace(raw))return"?";string s=raw.Trim();int b=s.IndexOf('[');if(b>=0)s=s.Substring(0,b).Trim();s=s.Replace("ShortName","").Replace("Template","").Trim(' ','[',']','(',')','{','}');if(s.Length==0)return"?";
            string compact=s.Replace("-","").Replace("_","").Replace(" ","");bool hexLike=compact.Length>=20;for(int i=0;i<compact.Length&&hexLike;i++)if(!Uri.IsHexDigit(compact[i]))hexLike=false;if(hexLike)return"?";if(s.Length>22)s=s.Substring(0,22).Trim();return s;
        }

        static string WeaponKey(string weapon)
        {
            string w=(weapon??"").ToLowerInvariant();
            if(w.Contains("ak")||w.Contains("rpk")||w.Contains("rd-704")||w.Contains("vpo-136")||w.Contains("vpo-209"))return"ak";
            if(w.Contains("m4")||w.Contains("hk416")||w.Contains("hk 416")||w.Contains("adar")||w.Contains("tx-15")||w.Contains("tx15")||w.Contains("m16")||w.Contains("mdr")||w.Contains("scar")||w.Contains("aug")||w.Contains("g36")||w.Contains("mcx"))return"ar";
            if(w.Contains("mp5")||w.Contains("mp7")||w.Contains("mp9")||w.Contains("pp-")||w.Contains("pp19")||w.Contains("vector")||w.Contains("ump")||w.Contains("p90")||w.Contains("kedr")||w.Contains("klin"))return"smg";
            if(w.Contains("saiga-12")||w.Contains("mp-133")||w.Contains("mp-153")||w.Contains("mp-155")||w.Contains("m870")||w.Contains("590a1")||w.Contains("ks-23")||w.Contains("benelli"))return"shotgun";
            if(w.Contains("svd")||w.Contains("m700")||w.Contains("dvl")||w.Contains("t-5000")||w.Contains("mosin")||w.Contains("axmc")||w.Contains("vpo-215")||w.Contains("sv-98"))return"sniper";
            if(w.Contains("glock")||w.Contains("p226")||w.Contains("m9")||w.Contains("tt")||w.Contains("usp")||w.Contains("five-seven")||w.Contains("1911")||w.Contains("aps")||w.Contains("pm pistol"))return"pistol";
            return"weapon";
        }

        static object ReadMember(object o,string n){if(o==null)return null;Type t=o.GetType();try{PropertyInfo p=t.GetProperty(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null)return p.GetValue(o,null);}catch{}try{FieldInfo f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}catch{}return null;}
        static float ToFloat(object v){try{return Convert.ToSingle(v);}catch{return 0f;}}
        static bool ToBool(object v){return v is bool&&(bool)v;}
    }
}
