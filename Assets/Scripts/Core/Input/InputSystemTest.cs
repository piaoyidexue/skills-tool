using UnityEngine;

/// <summary>
/// 输入系统测试脚本 - 用于验证输入模块功能
/// </summary>
public class InputSystemTest : MonoBehaviour
{
    private InputSystemManager _inputSystem;
    
    private void Awake()
    {
        _inputSystem = InputSystemManager.Instance;
        
        if (_inputSystem != null)
        {
            _inputSystem.OnInputCommand += OnInputCommandReceived;
        }
    }
    
    private void OnDestroy()
    {
        if (_inputSystem != null)
        {
            _inputSystem.OnInputCommand -= OnInputCommandReceived;
        }
    }
    
    private void OnInputCommandReceived(InputCommand command)
    {
        Debug.Log($"[InputSystemTest] Received input command: {command.Intent} at {Time.time:F3}s");
        
        // 测试移动向量
        if (command.Intent == InputIntent.Move && command.MoveVector.sqrMagnitude > 0.1f)
        {
            Debug.Log($"[InputSystemTest] Move vector: {command.MoveVector}");
        }
        
        // 测试技能指令
        if (command.Intent == InputIntent.Skill1 || command.Intent == InputIntent.Skill2)
        {
            Debug.Log($"[InputSystemTest] Skill command: {command.Intent}, ID: {command.SkillId}");
        }
    }
    
    private void Update()
    {
        // 测试获取当前移动向量
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_inputSystem != null)
            {
                var moveVector = _inputSystem.GetCurrentMoveVector();
                Debug.Log($"[InputSystemTest] Current move vector: {moveVector}");
            }
        }
    }
}