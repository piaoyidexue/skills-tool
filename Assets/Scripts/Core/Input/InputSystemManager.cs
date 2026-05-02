using System;
using UnityEngine;


/// <summary>
/// 输入系统管理器 - 处理语义化映射、状态拦截和缓冲区管理
/// </summary>
public class InputSystemManager : MonoBehaviour
{
    private static InputSystemManager _instance;
    public static InputSystemManager Instance => _instance;
    
    [Header("Input Settings")]
    [Tooltip("是否启用输入系统")]
    [SerializeField] private bool _isEnabled = true;
    
    [Tooltip("移动轴名称（默认Horizontal/Vertical）")]
    [SerializeField] private string _horizontalAxis = "Horizontal";
    [SerializeField] private string _verticalAxis = "Vertical";
    
    [Tooltip("技能1按键（默认1）")]
    [SerializeField] private KeyCode _skill1Key = KeyCode.Alpha1;
    [Tooltip("技能2按键（默认2）")]
    [SerializeField] private KeyCode _skill2Key = KeyCode.Alpha2;
    [Tooltip("攻击按键（默认J）")]
    [SerializeField] private KeyCode _attackKey = KeyCode.J;
    [Tooltip("交互按键（默认E）")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;
    [Tooltip("菜单按键（默认Escape）")]
    [SerializeField] private KeyCode _menuKey = KeyCode.Escape;
    
    [Header("State Interception")]
    [Tooltip("眩晕状态标签")]
    [SerializeField] private string _stunTag = "tag.status.stun";
    [Tooltip("沉默状态标签")]
    [SerializeField] private string _silenceTag = "tag.status.silence";
    
    private InputBufferManager _bufferManager;
    private GEHost _geHost;
    private SkillCaster _skillCaster;
    
    // 事件系统
    public event Action<InputCommand> OnInputCommand;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        _bufferManager = InputBufferManager.Instance;
        _geHost = GetComponent<GEHost>();
        _skillCaster = GetComponent<SkillCaster>();
        
        if (_bufferManager == null)
        {
            Debug.LogError("[InputSystemManager] InputBufferManager not found!");
        }
        
        if (_geHost == null)
        {
            Debug.LogWarning("[InputSystemManager] GEHost not found on this GameObject");
        }
        
        if (_skillCaster == null)
        {
            Debug.LogWarning("[InputSystemManager] SkillCaster not found on this GameObject");
        }
    }
    
    private void Update()
    {
        if (!_isEnabled || !_bufferManager) return;
        
        // 读取物理输入并映射到语义意图
        ProcessInput();
        
        // 处理缓冲区中的指令
        ProcessBufferedCommands();
    }
    
    /// <summary>
    /// 处理物理输入并映射到语义意图
    /// </summary>
    private void ProcessInput()
    {
        // 移动输入
        Vector2 moveVector = new Vector2(
            Input.GetAxis(_horizontalAxis),
            Input.GetAxis(_verticalAxis)
        );
        
        if (moveVector.sqrMagnitude > 0.1f)
        {
            var moveCommand = new InputCommand(InputIntent.Move, moveVector);
            if (_bufferManager.TryAddCommand(moveCommand))
            {
                OnInputCommand?.Invoke(moveCommand);
            }
        }
        
        // 技能1输入
        if (Input.GetKeyDown(_skill1Key))
        {
            var skill1Command = new InputCommand(InputIntent.Skill1, 1);
            if (_bufferManager.TryAddCommand(skill1Command))
            {
                OnInputCommand?.Invoke(skill1Command);
            }
        }
        
        // 技能2输入
        if (Input.GetKeyDown(_skill2Key))
        {
            var skill2Command = new InputCommand(InputIntent.Skill2, 2);
            if (_bufferManager.TryAddCommand(skill2Command))
            {
                OnInputCommand?.Invoke(skill2Command);
            }
        }
        
        // 攻击输入
        if (Input.GetKeyDown(_attackKey))
        {
            var attackCommand = new InputCommand(InputIntent.Attack);
            if (_bufferManager.TryAddCommand(attackCommand))
            {
                OnInputCommand?.Invoke(attackCommand);
            }
        }
        
        // 交互输入
        if (Input.GetKeyDown(_interactKey))
        {
            var interactCommand = new InputCommand(InputIntent.Interact);
            if (_bufferManager.TryAddCommand(interactCommand))
            {
                OnInputCommand?.Invoke(interactCommand);
            }
        }
        
        // 菜单输入
        if (Input.GetKeyDown(_menuKey))
        {
            var menuCommand = new InputCommand(InputIntent.OpenMenu);
            if (_bufferManager.TryAddCommand(menuCommand))
            {
                OnInputCommand?.Invoke(menuCommand);
            }
        }
    }
    
    /// <summary>
    /// 处理缓冲区中的指令，应用状态拦截
    /// </summary>
    private void ProcessBufferedCommands()
    {
        if (!_bufferManager) return;
        
        while (_bufferManager.TryGetValidCommand(out var command))
        {
            // 状态拦截：检查当前状态是否允许执行该指令
            if (!CanExecuteCommand(command))
            {
                continue; // 指令被拦截，跳过执行
            }
            
            // 执行指令
            ExecuteCommand(command);
        }
    }
    
    /// <summary>
    /// 检查指令是否可以执行（状态拦截）
    /// </summary>
    private bool CanExecuteCommand(InputCommand command)
    {
        if (_geHost == null) return true;
        
        switch (command.Intent)
        {
            case InputIntent.Move:
                // 被眩晕时不能移动
                if (_geHost.HasTag(_stunTag))
                {
                    Debug.Log("[InputSystemManager] Move command blocked by stun tag");
                    return false;
                }
                break;
                
            case InputIntent.Skill1:
            case InputIntent.Skill2:
                // 被沉默时不能施法
                if (_geHost.HasTag(_silenceTag))
                {
                    Debug.Log("[InputSystemManager] Skill command blocked by silence tag");
                    return false;
                }
                break;
                
            case InputIntent.Attack:
                // 被眩晕时不能攻击
                if (_geHost.HasTag(_stunTag))
                {
                    Debug.Log("[InputSystemManager] Attack command blocked by stun tag");
                    return false;
                }
                break;
        }
        
        return true;
    }
    
    /// <summary>
    /// 执行输入指令
    /// </summary>
    private void ExecuteCommand(InputCommand command)
    {
        switch (command.Intent)
        {
            case InputIntent.Move:
                // 将移动向量传递给移动组件
                HandleMoveCommand(command.MoveVector);
                break;
                
            case InputIntent.Skill1:
            case InputIntent.Skill2:
                // 将技能指令传递给SkillCaster
                HandleSkillCommand(command);
                break;
                
            case InputIntent.Attack:
                // 触发普通攻击
                HandleAttackCommand();
                break;
                
            case InputIntent.Interact:
                // 触发交互
                HandleInteractCommand();
                break;
                
            case InputIntent.OpenMenu:
                // 触发菜单打开
                HandleOpenMenuCommand();
                break;
        }
    }
    
    /// <summary>
    /// 处理移动指令
    /// </summary>
    private void HandleMoveCommand(Vector2 moveVector)
    {
        // 这里应该传递给角色的移动组件
        // 可以通过EventBus或直接调用移动组件的方法
        Debug.Log($"[InputSystemManager] Moving with vector: {moveVector}");
        
        // TODO: 实际的移动逻辑应该在这里实现
        // 例如：GetComponent<CharacterMovement>().SetMoveDirection(moveVector);
    }
    
    /// <summary>
    /// 处理技能指令
    /// </summary>
    private void HandleSkillCommand(InputCommand command)
    {
        if (_skillCaster == null)
        {
            Debug.LogWarning("[InputSystemManager] SkillCaster not found on this GameObject");
            return;
        }
        
        // 使用SkillCaster.TryCastFromInput方法
        bool success = _skillCaster.TryCastFromInput(command);
        
        if (success)
        {
            Debug.Log($"[InputSystemManager] Successfully cast skill {command.SkillId} with intent {command.Intent}");
        }
        else
        {
            Debug.Log($"[InputSystemManager] Failed to cast skill {command.SkillId} with intent {command.Intent}");
        }
    }
    
    /// <summary>
    /// 处理攻击指令
    /// </summary>
    private void HandleAttackCommand()
    {
        // TODO: 实现普通攻击逻辑
        Debug.Log("[InputSystemManager] Attack command executed");
    }
    
    /// <summary>
    /// 处理交互指令
    /// </summary>
    private void HandleInteractCommand()
    {
        // TODO: 实现交互逻辑
        Debug.Log("[InputSystemManager] Interact command executed");
    }
    
    /// <summary>
    /// 处理打开菜单指令
    /// </summary>
    private void HandleOpenMenuCommand()
    {
        // TODO: 实现菜单打开逻辑
        Debug.Log("[InputSystemManager] Open menu command executed");
    }
    
    /// <summary>
    /// 获取当前移动向量（用于外部查询）
    /// </summary>
    public Vector2 GetCurrentMoveVector()
    {
        return new Vector2(
            Input.GetAxis(_horizontalAxis),
            Input.GetAxis(_verticalAxis)
        );
    }
}