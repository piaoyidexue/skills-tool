using System.IO;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为树编辑器菜单和示例生成器。
    /// </summary>
    public static class AIGraphMenu
    {
        private const string GraphFolder = "Assets/Examples/AI";

        [MenuItem("Tools/Skills/AI/Generate Sample AI Trees", false, 101)]
        private static void GenerateSampleAITrees()
        {
            EnsureFolder("Assets/Examples");
            EnsureFolder(GraphFolder);

            GenerateCombatAITree();
            GeneratePatrolAITree();
            GenerateBossAITree();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AIGraphMenu] Generated sample AI behavior trees under " + GraphFolder);
        }

        /// <summary>
        ///     战斗AI模板：检测敌人 → 追击 → 攻击，低血量逃跑
        /// </summary>
        private static void GenerateCombatAITree()
        {
            const string path = GraphFolder + "/AI_Combat.asset";
            var graph = LoadOrCreateGraph(path, "AI_Combat");

            var rootSelector = graph.AddNode<PrioritizedSelector>();
            rootSelector.dynamic = true;
            rootSelector.position = new Vector2(300, 200);

            // ---- 分支1: 低血量逃跑 ----
            var fleeSeq = graph.AddNode<Sequencer>();
            fleeSeq.position = new Vector2(300, 50);
            graph.ConnectNodes(rootSelector, fleeSeq);

            var healthLow = graph.AddNode<IsHealthBelow>();
            healthLow.threshold = new BBParameter<float>(0.25f);
            healthLow.position = new Vector2(80, 50);
            graph.ConnectNodes(fleeSeq, healthLow);

            var fleeAction = graph.AddNode<Flee>();
            fleeAction.position = new Vector2(320, 50);
            graph.ConnectNodes(healthLow, fleeAction);

            // ---- 分支2: 有目标 → 攻击 ----
            var attackSeq = graph.AddNode<Sequencer>();
            attackSeq.position = new Vector2(300, 180);
            graph.ConnectNodes(rootSelector, attackSeq);

            var hasTarget = graph.AddNode<HasTarget>();
            hasTarget.position = new Vector2(80, 180);
            graph.ConnectNodes(attackSeq, hasTarget);

            var targetObs = graph.AddNode<TargetObserver>();
            targetObs.position = new Vector2(320, 160);
            graph.ConnectNodes(hasTarget, targetObs);

            var inRangeSeq = graph.AddNode<Selector>();
            inRangeSeq.position = new Vector2(560, 160);
            graph.ConnectNodes(targetObs, inRangeSeq);

            var inRange = graph.AddNode<IsTargetInRange>();
            inRange.range = new BBParameter<float>(2.5f);
            inRange.position = new Vector2(560, 100);
            graph.ConnectNodes(inRangeSeq, inRange);

            var attackCooldown = graph.AddNode<Cooldown>();
            attackCooldown.cooldownTime = new BBParameter<float>(1f);
            attackCooldown.position = new Vector2(560, 200);
            graph.ConnectNodes(inRangeSeq, attackCooldown);

            var attack = graph.AddNode<AttackTarget>();
            attack.position = new Vector2(800, 200);
            graph.ConnectNodes(attackCooldown, attack);

            var moveTo = graph.AddNode<MoveTo>();
            moveTo.position = new Vector2(800, 100);
            graph.ConnectNodes(inRange, moveTo);

            // ---- 分支3: 无目标 → 传感器扫描 + 待机 ----
            var idleSeq = graph.AddNode<Sequencer>();
            idleSeq.position = new Vector2(300, 320);
            graph.ConnectNodes(rootSelector, idleSeq);

            var scan = graph.AddNode<SensorScan>();
            scan.position = new Vector2(80, 320);
            graph.ConnectNodes(idleSeq, scan);

            var idle = graph.AddNode<Idle>();
            idle.duration = new BBParameter<float>(0.5f);
            idle.position = new Vector2(320, 320);
            graph.ConnectNodes(scan, idle);

            EditorUtility.SetDirty(graph);
        }

        /// <summary>
        ///     巡逻AI模板：按路径点巡逻，遇到敌人切换到战斗
        /// </summary>
        private static void GeneratePatrolAITree()
        {
            const string path = GraphFolder + "/AI_Patrol.asset";
            var graph = LoadOrCreateGraph(path, "AI_Patrol");

            var rootSelector = graph.AddNode<PrioritizedSelector>();
            rootSelector.dynamic = true;
            rootSelector.position = new Vector2(300, 200);

            // ---- 分支1: 检测到敌人 → 追击 ----
            var chaseSeq = graph.AddNode<Sequencer>();
            chaseSeq.position = new Vector2(300, 60);
            graph.ConnectNodes(rootSelector, chaseSeq);

            var hasEnemy = graph.AddNode<HasEnemyDetected>();
            hasEnemy.position = new Vector2(80, 60);
            graph.ConnectNodes(chaseSeq, hasEnemy);

            var moveToEnemy = graph.AddNode<MoveTo>();
            moveToEnemy.position = new Vector2(320, 60);
            graph.ConnectNodes(hasEnemy, moveToEnemy);

            var attackEnemy = graph.AddNode<AttackTarget>();
            attackEnemy.position = new Vector2(560, 60);
            graph.ConnectNodes(moveToEnemy, attackEnemy);

            // ---- 分支2: 巡逻 ----
            var patrol = graph.AddNode<PatrolWaypoints>();
            patrol.position = new Vector2(300, 180);
            graph.ConnectNodes(rootSelector, patrol);

            EditorUtility.SetDirty(graph);
        }

        /// <summary>
        ///     Boss AI模板：多阶段战斗 + 召唤技能
        /// </summary>
        private static void GenerateBossAITree()
        {
            const string path = GraphFolder + "/AI_Boss.asset";
            var graph = LoadOrCreateGraph(path, "AI_Boss");

            var rootSelector = graph.AddNode<PrioritizedSelector>();
            rootSelector.dynamic = true;
            rootSelector.position = new Vector2(300, 200);

            // ---- 阶段3（终局）：血量<30% → 狂暴攻击 ----
            var phase3Seq = graph.AddNode<Sequencer>();
            phase3Seq.position = new Vector2(300, 40);
            graph.ConnectNodes(rootSelector, phase3Seq);

            var hpBelow30 = graph.AddNode<IsHealthBelow>();
            hpBelow30.threshold = new BBParameter<float>(0.3f);
            hpBelow30.position = new Vector2(80, 40);
            graph.ConnectNodes(phase3Seq, hpBelow30);

            var castUltimate = graph.AddNode<CastSkill>();
            castUltimate.skillId = "boss_ultimate";
            castUltimate.position = new Vector2(320, 40);
            graph.ConnectNodes(hpBelow30, castUltimate);

            var enrageSeq = graph.AddNode<Sequencer>();
            enrageSeq.position = new Vector2(560, 40);
            graph.ConnectNodes(castUltimate, enrageSeq);

            var targetObs3 = graph.AddNode<TargetObserver>();
            targetObs3.position = new Vector2(560, 40);
            graph.ConnectNodes(enrageSeq, targetObs3);

            var attackRapid = graph.AddNode<AttackTarget>();
            attackRapid.attackSpeed = new BBParameter<float>(2f);
            attackRapid.position = new Vector2(800, 40);
            graph.ConnectNodes(targetObs3, attackRapid);

            // ---- 阶段2：血量<60% → 召唤+技能 ----
            var phase2Seq = graph.AddNode<Sequencer>();
            phase2Seq.position = new Vector2(300, 140);
            graph.ConnectNodes(rootSelector, phase2Seq);

            var hpBelow60 = graph.AddNode<IsHealthBelow>();
            hpBelow60.threshold = new BBParameter<float>(0.6f);
            hpBelow60.position = new Vector2(80, 140);
            graph.ConnectNodes(phase2Seq, hpBelow60);

            var castSummon = graph.AddNode<CastSkill>();
            castSummon.skillId = "boss_summon";
            castSummon.position = new Vector2(320, 140);
            graph.ConnectNodes(hpBelow60, castSummon);

            var targetObs2 = graph.AddNode<TargetObserver>();
            targetObs2.position = new Vector2(560, 140);
            graph.ConnectNodes(castSummon, targetObs2);

            var attack2 = graph.AddNode<AttackTarget>();
            attack2.position = new Vector2(800, 140);
            graph.ConnectNodes(targetObs2, attack2);

            // ---- 阶段1（默认）：常规攻击 ----
            var phase1Seq = graph.AddNode<Sequencer>();
            phase1Seq.position = new Vector2(300, 260);
            graph.ConnectNodes(rootSelector, phase1Seq);

            var targetObs1 = graph.AddNode<TargetObserver>();
            targetObs1.position = new Vector2(80, 260);
            graph.ConnectNodes(phase1Seq, targetObs1);

            var attack1 = graph.AddNode<AttackTarget>();
            attack1.position = new Vector2(320, 260);
            graph.ConnectNodes(targetObs1, attack1);

            EditorUtility.SetDirty(graph);
        }

        private static AIGraph LoadOrCreateGraph(string path, string assetName)
        {
            AssetDatabase.DeleteAsset(path); // 总是重新生成
            var graph = ScriptableObject.CreateInstance<AIGraph>();
            graph.TreeName = assetName;
            AssetDatabase.CreateAsset(graph, path);
            return graph;
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
    }
}
