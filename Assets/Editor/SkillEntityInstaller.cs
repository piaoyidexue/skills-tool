using System.IO;
using UnityEditor;
using UnityEngine;

public class SkillSystemBootstrapper : Editor
{
    [MenuItem("Tools/Skills/Bootstrap All")]
    private static void BootstrapAll()
    {
        EnsureFolder("Assets/Scripts");
        EnsureFolder("Assets/Scripts/Data");
        EnsureFolder("Assets/Scripts/Runtime");
        EnsureFolder("Assets/Scripts/Graph");
        EnsureFolder("Assets/Scripts/Nodes");
        EnsureFolder("Assets/Scripts/VFX");
        EnsureFolder("Assets/Scripts/Editor");
        EnsureFolder("Assets/Scripts/Entity");
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Config");
        EnsureFolder("Assets/Examples");
        EnsureFolder("Assets/Examples/Graphs");

        EnsureCsv("Assets/Resources/Config/Skill.csv",
            "skill_id,name,graph_path,impact_vfx,beam_vfx,damage,damage_rate,cooldown,cast_range,delay_seconds,crit_chance,radius,chain_count,vfx_duration\n" +
            "1001,Fireball,Examples/Graphs/Skill_Fireball,HitSpark,,120,1.5,2,10,0.15,0.2,2,0,0.8\n" +
            "1002,Chain Lightning,,HitSpark,LightningBeam,45,0.8,5,8,0.05,0.25,4,3,0.35\n" +
            "1003,AOE Explosion,,ExplosionWave,,160,1.0,6,7,0.2,0.1,4.5,0,0.75\n" +
            "1004,Frost Nova,,FrostBurst,,80,1.1,8,5,0.1,0.15,5,0,0.9\n" +
            "1005,Crit Fireball,Examples/Graphs/Skill_CritFireball,HitSpark,LightningBeam,90,1.3,3,10,0.1,0.35,2,0,0.5\n");

        EnsureCsv("Assets/Resources/Config/Buff.csv",
            "buff_id,type,value,duration,icon_key\n" +
            "2001,burn,5,3,burn\n" +
            "2002,slow,0.3,2,slow\n" +
            "2003,freeze,1,1.5,freeze\n" +
            "2004,haste,0.25,4,haste\n");

        EnsureCsv("Assets/Resources/Config/Effect.csv",
            "effect_id,effect_key,prefab_name,scale,duration,warmup_count\n" +
            "1,HitSpark,HitSpark,1.0,0.4,8\n" +
            "2,LightningBeam,LightningBeam,1.0,0.25,12\n" +
            "3,ExplosionWave,ExplosionWave,1.4,0.75,8\n" +
            "4,FrostBurst,FrostBurst,1.25,0.6,8\n");

        EnsureCsv("Assets/Resources/Config/AITree.csv",
            "tree_id,tree_name,ai_type,priority,update_interval,chain_order,node_order,node_class,params\n" +
            "# === 战斗AI：低血量逃跑 > 目标攻击 > 传感器待机 ===\n" +
            "combat,战斗AI,Combat,50,0.1,1,1,IsHealthBelow,threshold=0.25\n" +
            "combat,战斗AI,Combat,50,0.1,1,2,Flee,safeDistance=15;fleeSpeedMultiplier=1.5\n" +
            "combat,战斗AI,Combat,50,0.1,2,1,HasTarget,\n" +
            "combat,战斗AI,Combat,50,0.1,2,2,TargetObserver,maxObserveDistance=30\n" +
            "combat,战斗AI,Combat,50,0.1,2,3,IsTargetInRange,range=2.5\n" +
            "combat,战斗AI,Combat,50,0.1,2,4,Cooldown,cooldownTime=1\n" +
            "combat,战斗AI,Combat,50,0.1,2,5,AttackTarget,damage=10;attackRange=2.5;attackSpeed=1\n" +
            "combat,战斗AI,Combat,50,0.1,2,6,MoveTo,arriveDistance=1.5\n" +
            "combat,战斗AI,Combat,50,0.1,3,1,SensorScan,scanInterval=0.5\n" +
            "combat,战斗AI,Combat,50,0.1,3,2,Idle,duration=0.5\n" +
            "# === 巡逻AI：遇敌追击 > 巡逻 ===\n" +
            "patrol,巡逻AI,Patrol,50,0.15,1,1,HasEnemyDetected,\n" +
            "patrol,巡逻AI,Patrol,50,0.15,1,2,TargetObserver,maxObserveDistance=25\n" +
            "patrol,巡逻AI,Patrol,50,0.15,1,3,MoveTo,arriveDistance=2\n" +
            "patrol,巡逻AI,Patrol,50,0.15,1,4,AttackTarget,damage=8;attackRange=2.5\n" +
            "patrol,巡逻AI,Patrol,50,0.15,2,1,PatrolWaypoints,waitTime=1.5;mode=Loop\n" +
            "# === Boss AI：三阶段战斗 ===\n" +
            "boss, Boss AI,Boss,80,0,1,1,IsHealthBelow,threshold=0.3\n" +
            "boss, Boss AI,Boss,80,0,1,2,CastSkill,skillId=boss_ultimate;castRange=15\n" +
            "boss, Boss AI,Boss,80,0,1,3,TargetObserver,maxObserveDistance=50\n" +
            "boss, Boss AI,Boss,80,0,1,4,AttackTarget,damage=25;attackSpeed=2\n" +
            "boss, Boss AI,Boss,80,0,2,1,IsHealthBelow,threshold=0.6\n" +
            "boss, Boss AI,Boss,80,0,2,2,CastSkill,skillId=boss_summon;castRange=20\n" +
            "boss, Boss AI,Boss,80,0,2,3,TargetObserver,maxObserveDistance=50\n" +
            "boss, Boss AI,Boss,80,0,2,4,AttackTarget,damage=18;attackSpeed=1.2\n" +
            "boss, Boss AI,Boss,80,0,3,1,TargetObserver,maxObserveDistance=50\n" +
            "boss, Boss AI,Boss,80,0,3,2,AttackTarget,damage=12;attackSpeed=1\n" +
            "# === 守卫AI：警戒待机，有敌追击 ===\n" +
            "guard,守卫AI,Guard,40,0.1,1,1,HasEnemyDetected,\n" +
            "guard,守卫AI,Guard,40,0.1,1,2,MoveTo,arriveDistance=3\n" +
            "guard,守卫AI,Guard,40,0.1,1,3,AttackTarget,damage=10\n" +
            "guard,守卫AI,Guard,40,0.1,2,1,SensorScan,scanInterval=0.3\n" +
            "guard,守卫AI,Guard,40,0.1,2,2,Idle,duration=1\n");

        AssetDatabase.Refresh();
        Debug.Log("[SkillSystemBootstrapper] Bootstrap complete. Existing runtime code was preserved.");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void EnsureCsv(string path, string content)
    {
        if (File.Exists(path)) return;

        File.WriteAllText(path, content);
    }
}