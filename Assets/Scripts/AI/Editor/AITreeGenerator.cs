using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     AI 行为树自动生成器 —— 从 AITree.csv 读取配置，自动创建 AIGraph 资产。
    ///     遵循"CSV 是唯一数据源"的设计原则，策划修改 CSV 即可批量生成/更新行为树。
    ///
    ///     设计思路：
    ///     1. CSV 中每行定义一个节点，tree_id 分组、chain_order 分链、node_order 排序
    ///     2. 每个 chain 自动包裹为一个 Sequencer，所有 chain 挂载到 PrioritizedSelector 根节点
    ///     3. 节点参数通过 key=value;key=value 格式传入，自动设置到对应字段
    ///     4. 生成后可在 NodeCanvas 编辑器中手动微调
    /// </summary>
    public static class AITreeGenerator
    {
        private const string CsvPath = "Assets/Resources/Config/AITree.csv";
        private const string OutputFolder = "Assets/Resources/AITrees";

        /// <summary>节点类名 → System.Type 映射表</summary>
        private static readonly Dictionary<string, Type> NodeTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // ---- 组合节点 ----
            { "PrioritizedSelector", typeof(PrioritizedSelector) },
            { "ParallelAll", typeof(ParallelAll) },
            { "Sequencer", typeof(Sequencer) },
            { "Selector", typeof(Selector) },

            // ---- 装饰器节点 ----
            { "Cooldown", typeof(Cooldown) },
            { "TargetObserver", typeof(TargetObserver) },

            // ---- 行为节点 ----
            { "MoveTo", typeof(MoveTo) },
            { "AttackTarget", typeof(AttackTarget) },
            { "CastSkill", typeof(CastSkill) },
            { "Flee", typeof(Flee) },
            { "Idle", typeof(Idle) },
            { "PatrolWaypoints", typeof(PatrolWaypoints) },
            { "PlayAnimation", typeof(PlayAnimation) },
            { "SensorScan", typeof(SensorScan) },

            // ---- 条件节点 ----
            { "HasTarget", typeof(HasTarget) },
            { "IsTargetInRange", typeof(IsTargetInRange) },
            { "IsHealthBelow", typeof(IsHealthBelow) },
            { "HasEnemyDetected", typeof(HasEnemyDetected) },
            { "BlackboardBool", typeof(BlackboardBool) },
            { "CooldownReady", typeof(CooldownReady) },
            { "CompareFloat", typeof(CompareFloat) },
        };

        /// <summary>反射 BindingFlags，统一用于字段/属性查找</summary>
        private const BindingFlags ReflectFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        #region 菜单入口

        [MenuItem("Tools/Skills/AI/Generate AI Trees from CSV", false, 102)]
        private static void GenerateFromMenu()
        {
            GenerateAll();
        }

        [MenuItem("Tools/Skills/AI/Generate AI Trees from CSV", true)]
        private static bool GenerateFromMenuValidate()
        {
            return File.Exists(CsvPath);
        }

        #endregion

        #region 公开 API

        /// <summary>从 CSV 生成全部 AI 行为树</summary>
        public static void GenerateAll()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"[AITreeGenerator] CSV 文件不存在: {CsvPath}");
                return;
            }

            var rows = ParseCsv(CsvPath);
            if (rows.Count == 0)
            {
                Debug.LogWarning("[AITreeGenerator] CSV 中没有有效数据行。");
                return;
            }

            var definitions = GroupIntoDefinitions(rows);
            if (definitions.Count == 0)
            {
                Debug.LogWarning("[AITreeGenerator] 未能解析出有效的行为树定义。");
                return;
            }

            EnsureFolder(OutputFolder);

            var generatedCount = 0;
            foreach (var def in definitions)
            {
                try
                {
                    GenerateTree(def);
                    generatedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AITreeGenerator] 生成行为树 '{def.TreeId}' 失败: {ex.Message}\n{ex.StackTrace}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AITreeGenerator] 完成！共生成 {generatedCount}/{definitions.Count} 棵 AI 行为树 → {OutputFolder}");
        }

        /// <summary>无声模式生成（供 AssetPostprocessor 调用）</summary>
        public static void GenerateAllSilently()
        {
            if (!File.Exists(CsvPath)) return;

            var rows = ParseCsv(CsvPath);
            if (rows.Count == 0) return;

            var definitions = GroupIntoDefinitions(rows);
            foreach (var def in definitions)
            {
                try { GenerateTree(def); }
                catch (Exception ex) { Debug.LogError($"[AITreeGenerator] {def.TreeId}: {ex.Message}"); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        #endregion

        #region CSV 解析

        private static List<AITreeRowConfig> ParseCsv(string filePath)
        {
            var rows = new List<AITreeRowConfig>();
            var csvText = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = csvText.Replace("\r", "").Split('\n');

            if (lines.Length < 2) return rows;

            var headers = SplitCsvLine(lines[0]);

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var cols = SplitCsvLine(line);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var c = 0; c < headers.Count; c++)
                    dict[headers[c]] = c < cols.Count ? cols[c] : "";

                var treeId = GetString(dict, "tree_id");
                if (string.IsNullOrWhiteSpace(treeId)) continue;

                var nodeClass = GetString(dict, "node_class");
                if (string.IsNullOrWhiteSpace(nodeClass)) continue;

                rows.Add(new AITreeRowConfig
                {
                    TreeId = treeId.Trim(),
                    TreeName = GetString(dict, "tree_name", treeId),
                    AIType = ParseEnum(GetString(dict, "ai_type"), AIType.Combat),
                    Priority = ParseInt(dict, "priority", 50),
                    UpdateInterval = ParseFloat(dict, "update_interval"),
                    ChainOrder = ParseInt(dict, "chain_order"),
                    NodeOrder = ParseInt(dict, "node_order"),
                    NodeClass = nodeClass.Trim(),
                    Params = GetString(dict, "params"),
                });
            }

            return rows;
        }

        private static List<AITreeDefinition> GroupIntoDefinitions(List<AITreeRowConfig> rows)
        {
            var result = new List<AITreeDefinition>();
            var treeGroups = rows.GroupBy(r => r.TreeId);

            foreach (var treeGroup in treeGroups)
            {
                var first = treeGroup.First();
                var def = new AITreeDefinition
                {
                    TreeId = first.TreeId,
                    TreeName = first.TreeName,
                    AIType = first.AIType,
                    Priority = first.Priority,
                    UpdateInterval = first.UpdateInterval,
                };

                var chainGroups = treeGroup
                    .OrderBy(r => r.ChainOrder)
                    .GroupBy(r => r.ChainOrder);

                foreach (var chainGroup in chainGroups)
                {
                    def.Chains.Add(new AITreeChain
                    {
                        ChainOrder = chainGroup.Key,
                        Nodes = chainGroup.OrderBy(r => r.NodeOrder).ToList(),
                    });
                }

                // 按 chain_order 排序（链优先级）
                def.Chains.Sort((a, b) => a.ChainOrder.CompareTo(b.ChainOrder));

                result.Add(def);
            }

            return result;
        }

        #endregion

        #region 图生成

        private static void GenerateTree(AITreeDefinition def)
        {
            var assetPath = $"{OutputFolder}/AI_{def.TreeId}.asset";

            // 删除旧资产重新生成
            AssetDatabase.DeleteAsset(assetPath);

            var graph = ScriptableObject.CreateInstance<AIGraph>();
            graph.TreeName = def.TreeName;
            graph.TreeDescription = $"自动生成自 AITree.csv (id={def.TreeId})";
            SetPrivateField(graph, "aiType", def.AIType);
            SetPrivateField(graph, "priority", Mathf.Clamp(def.Priority, 0, 100));

            AssetDatabase.CreateAsset(graph, assetPath);

            // 1. 创建根节点 PrioritizedSelector
            var rootSelector = graph.AddNode<PrioritizedSelector>();
            rootSelector.dynamic = true;
            rootSelector.position = new Vector2(400, 100);

            // 2. 为每个行为链创建 Sequencer + 子节点
            var chainY = 60f;
            var chainSequencers = new List<Node>();

            foreach (var chain in def.Chains)
            {
                var seq = graph.AddNode<Sequencer>();
                seq.position = new Vector2(400, chainY);
                graph.ConnectNodes(rootSelector, seq);
                chainSequencers.Add(seq);

                // 创建链内节点
                var nodeX = 150f;
                Node prevNode = null;

                foreach (var nodeRow in chain.Nodes)
                {
                    var nodeType = ResolveNodeType(nodeRow.NodeClass);
                    if (nodeType == null)
                    {
                        Debug.LogWarning($"[AITreeGenerator] 未知节点类型: '{nodeRow.NodeClass}' (树={def.TreeId}, 链={chain.ChainOrder})");
                        continue;
                    }

                    var node = graph.AddNode(nodeType);
                    node.position = new Vector2(nodeX, chainY);

                    // 设置节点参数
                    ApplyNodeParams(node, nodeRow.Params);

                    if (prevNode != null)
                    {
                        graph.ConnectNodes(prevNode, node);
                    }
                    else
                    {
                        graph.ConnectNodes(seq, node);
                    }

                    prevNode = node;
                    nodeX += 220f;
                }

                chainY += 100f;
            }

            // 调整根节点位置到中心
            if (chainSequencers.Count > 0)
            {
                var avgY = chainSequencers.Average(s => s.position.y);
                rootSelector.position = new Vector2(400, avgY);
            }

            EditorUtility.SetDirty(graph);
        }

        private static Type ResolveNodeType(string className)
        {
            return NodeTypeMap.TryGetValue(className, out var type) ? type : null;
        }

        /// <summary>解析参数字符串并设置到节点字段</summary>
        private static void ApplyNodeParams(Node node, string paramsStr)
        {
            if (string.IsNullOrWhiteSpace(paramsStr)) return;

            var pairs = paramsStr.Split(';');
            foreach (var pair in pairs)
            {
                var eqIdx = pair.IndexOf('=');
                if (eqIdx <= 0) continue;

                var key = pair.Substring(0, eqIdx).Trim();
                var value = pair.Substring(eqIdx + 1).Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;

                SetNodeField(node, key, value);
            }
        }

        /// <summary>通过反射设置节点字段值，支持 BBParameter 自动包装</summary>
        private static void SetNodeField(Node node, string fieldName, string rawValue)
        {
            var nodeType = node.GetType();

            // 尝试 Field
            var field = nodeType.GetField(fieldName, ReflectFlags);
            if (field != null)
            {
                SetFieldValue(node, field, rawValue);
                return;
            }

            // 尝试 Property（安全查找，避免 Ambiguous match found）
            var prop = FindPropertySafe(nodeType, fieldName);
            if (prop != null && prop.CanWrite)
            {
                SetPropertyValue(node, prop, rawValue);
                return;
            }

            Debug.LogWarning($"[AITreeGenerator] 节点 '{nodeType.Name}' 上找不到字段/属性: '{fieldName}'");
        }

        /// <summary>
        ///     安全查找属性。使用 GetProperties 遍历而非 GetProperty(name, flags)，
        ///     避免 BBParameter&lt;T&gt; 等泛型继承体系中 "Ambiguous match found" 异常。
        ///     返回第一个可写的匹配属性（派生类优先）。
        /// </summary>
        private static PropertyInfo FindPropertySafe(Type type, string name)
        {
            foreach (var p in type.GetProperties(ReflectFlags))
            {
                if (p.Name == name && p.CanWrite)
                    return p;
            }
            return null;
        }

        private static void SetFieldValue(object target, FieldInfo field, string rawValue)
        {
            var fieldType = field.FieldType;
            object converted;

            // BBParameter<T> 包装
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(BBParameter<>))
            {
                var innerType = fieldType.GetGenericArguments()[0];
                var innerValue = ConvertValue(rawValue, innerType);
                if (innerValue != null)
                {
                    var existing = field.GetValue(target);
                    if (existing != null)
                    {
                        SetBBParameterValue(fieldType, existing, innerValue);
                        return;
                    }

                    var bbParam = Activator.CreateInstance(fieldType);
                    SetBBParameterValue(fieldType, bbParam, innerValue);
                    field.SetValue(target, bbParam);
                }
                return;
            }

            // 枚举类型
            if (fieldType.IsEnum)
            {
                converted = ParseEnumValue(rawValue, fieldType);
                if (converted != null)
                    field.SetValue(target, converted);
                return;
            }

            // 普通类型
            converted = ConvertValue(rawValue, fieldType);
            if (converted != null)
                field.SetValue(target, converted);
        }

        private static void SetPropertyValue(object target, PropertyInfo prop, string rawValue)
        {
            var propType = prop.PropertyType;
            object converted;

            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(BBParameter<>))
            {
                var innerType = propType.GetGenericArguments()[0];
                var innerValue = ConvertValue(rawValue, innerType);
                if (innerValue != null)
                {
                    var existing = prop.GetValue(target);
                    if (existing != null)
                    {
                        SetBBParameterValue(propType, existing, innerValue);
                        return;
                    }

                    var bbParam = Activator.CreateInstance(propType);
                    SetBBParameterValue(propType, bbParam, innerValue);
                    prop.SetValue(target, bbParam);
                }
                return;
            }

            if (propType.IsEnum)
            {
                converted = ParseEnumValue(rawValue, propType);
                if (converted != null)
                    prop.SetValue(target, converted);
                return;
            }

            converted = ConvertValue(rawValue, propType);
            if (converted != null)
                prop.SetValue(target, converted);
        }

        /// <summary>
        ///     安全设置 BBParameter&lt;T&gt;.value，避免 "Ambiguous match found"。
        ///     使用 GetProperties 遍历查找可写且类型匹配的 value 属性。
        /// </summary>
        private static void SetBBParameterValue(Type bbParamType, object bbParamInstance, object innerValue)
        {
            // 遍历所有 public instance 属性，找到名称是 "value" 且类型匹配的
            foreach (var p in bbParamType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name == "value" && p.CanWrite && p.PropertyType.IsAssignableFrom(innerValue.GetType()))
                {
                    p.SetValue(bbParamInstance, innerValue);
                    return;
                }
            }

            Debug.LogWarning($"[AITreeGenerator] 无法在 {bbParamType.Name} 上设置 value 属性。");
        }

        /// <summary>将字符串转换为目标类型</summary>
        private static object ConvertValue(string raw, Type targetType)
        {
            try
            {
                if (targetType == typeof(string))
                    return raw;

                if (targetType == typeof(int))
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv) ? iv : (object)null;

                if (targetType == typeof(float))
                    return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv) ? fv : null;

                if (targetType == typeof(bool))
                    return bool.TryParse(raw, out var bv) ? bv : raw.ToLowerInvariant() switch
                    {
                        "1" or "yes" or "true" => true,
                        "0" or "no" or "false" => false,
                        _ => (object)null,
                    };

                if (targetType == typeof(Vector3))
                {
                    var parts = raw.Split(',');
                    if (parts.Length == 3 &&
                        float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                        return new Vector3(x, y, z);
                    return null;
                }

                if (targetType.IsEnum)
                    return ParseEnumValue(raw, targetType);

                Debug.LogWarning($"[AITreeGenerator] 不支持的字段类型: {targetType.Name}");
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static object ParseEnumValue(string raw, Type enumType)
        {
            try
            {
                if (Enum.TryParse(enumType, raw, true, out var result))
                    return result;

                if (int.TryParse(raw, out var intVal))
                    return Enum.ToObject(enumType, intVal);

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>通过反射设置私有字段</summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
                field.SetValue(target, value);
        }

        #endregion

        #region CSV 工具方法

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(current.Trim());
                    current = "";
                    continue;
                }

                current += ch;
            }

            result.Add(current.Trim());
            return result;
        }

        private static string GetString(Dictionary<string, string> row, string key, string defaultValue = "")
        {
            return row.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private static int ParseInt(Dictionary<string, string> row, string key, int defaultValue = 0)
        {
            if (!row.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return defaultValue;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
        }

        private static float ParseFloat(Dictionary<string, string> row, string key, float defaultValue = 0f)
        {
            if (!row.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return defaultValue;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
        }

        private static T ParseEnum<T>(string raw, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            return Enum.TryParse<T>(raw, true, out var result) ? result : defaultValue;
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

        #endregion
    }
}
