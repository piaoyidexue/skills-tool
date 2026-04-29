using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace SkillAI
{
    /// <summary>
    ///     传感器扫描 —— 驱动 AI 传感器执行环境检测，更新黑板中的目标信息。
    ///     完全依赖空间哈希网格（无 Physics.OverlapSphere 回退）。
    /// </summary>
    [Name("★ Sensor Scan")]
    [Category("Composites/AI/Actions")]
    [Description("驱动AI传感器扫描环境，检测并更新目标。")]
    [Color("AB47BC")]
    [ParadoxNotion.Design.Icon("Action")]
    public class SensorScan : AIActionNode
    {
        [Tooltip("扫描间隔（秒）")]
        public BBParameter<float> scanInterval = 0.5f;

        private AISensor _sensor;
        private float _lastScanTime = float.MinValue;

        protected override void OnActionInit(Component agent, IBlackboard blackboard)
        {
            _sensor = agent.GetComponent<AISensor>();
            if (_sensor == null)
                _sensor = agent.GetComponentInChildren<AISensor>();
        }

        protected override Status OnExecuteOnce(Component agent, IBlackboard blackboard)
        {
            if (Time.time - _lastScanTime < scanInterval.value)
                return Status.Success;

            _lastScanTime = Time.time;

            if (_sensor != null)
            {
                _sensor.Scan();
            }

            return Status.Success;
        }
    }
}
