










using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;


























































public partial class @GameInput: IInputActionCollection2, IDisposable
{
    
    
    
    public InputActionAsset asset { get; }

    
    
    
    public @GameInput()
    {
        asset = InputActionAsset.FromJson(@"{
    ""version"": 1,
    ""name"": ""GameInput"",
    ""maps"": [
        {
            ""name"": ""Player"",
            ""id"": ""2d2bf19c-b7f9-42cf-9f19-28999024d06a"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""1866cde4-bb17-44c1-8ac3-d449f31fc253"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true,
                    ""priority"": 0
                },
                {
                    ""name"": ""Brake"",
                    ""type"": ""Button"",
                    ""id"": ""2933be52-2c2f-4735-9a06-dac1db8c34f5"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false,
                    ""priority"": 0
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""75167d18-9571-48d9-94c8-12add9954f71"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""615719e5-eb7d-439b-b5d2-97198aa96fbf"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""fbdb4e89-b819-4c19-a302-f5140e31f7d0"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""4b46af50-c578-4103-9e29-9029ab8f199a"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Brake"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        
        m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
        m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
        m_Player_Brake = m_Player.FindAction("Brake", throwIfNotFound: true);
    }

    ~@GameInput()
    {
        UnityEngine.Debug.Assert(!m_Player.enabled, "This will cause a leak and performance issues, GameInput.Player.Disable() has not been called.");
    }

    
    
    
    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    
    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    
    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    
    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    
    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    
    public void Enable()
    {
        asset.Enable();
    }

    
    public void Disable()
    {
        asset.Disable();
    }

    
    public IEnumerable<InputBinding> bindings => asset.bindings;

    
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    
    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    
    private readonly InputActionMap m_Player;
    private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();
    private readonly InputAction m_Player_Move;
    private readonly InputAction m_Player_Brake;
    
    
    
    public struct PlayerActions
    {
        private @GameInput m_Wrapper;

        
        
        
        public PlayerActions(@GameInput wrapper) { m_Wrapper = wrapper; }
        
        
        
        public InputAction @Move => m_Wrapper.m_Player_Move;
        
        
        
        public InputAction @Brake => m_Wrapper.m_Player_Brake;
        
        
        
        public InputActionMap Get() { return m_Wrapper.m_Player; }
        
        public void Enable() { Get().Enable(); }
        
        public void Disable() { Get().Disable(); }
        
        public bool enabled => Get().enabled;
        
        
        
        public static implicit operator InputActionMap(PlayerActions set) { return set.Get(); }
        
        
        
        
        
        
        
        
        public void AddCallbacks(IPlayerActions instance)
        {
            if (instance == null || m_Wrapper.m_PlayerActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_PlayerActionsCallbackInterfaces.Add(instance);
            @Move.started += instance.OnMove;
            @Move.performed += instance.OnMove;
            @Move.canceled += instance.OnMove;
            @Brake.started += instance.OnBrake;
            @Brake.performed += instance.OnBrake;
            @Brake.canceled += instance.OnBrake;
        }

        
        
        
        
        
        
        
        private void UnregisterCallbacks(IPlayerActions instance)
        {
            @Move.started -= instance.OnMove;
            @Move.performed -= instance.OnMove;
            @Move.canceled -= instance.OnMove;
            @Brake.started -= instance.OnBrake;
            @Brake.performed -= instance.OnBrake;
            @Brake.canceled -= instance.OnBrake;
        }

        
        
        
        
        public void RemoveCallbacks(IPlayerActions instance)
        {
            if (m_Wrapper.m_PlayerActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        
        
        
        
        
        
        
        
        
        public void SetCallbacks(IPlayerActions instance)
        {
            foreach (var item in m_Wrapper.m_PlayerActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_PlayerActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    
    
    
    public PlayerActions @Player => new PlayerActions(this);
    
    
    
    
    
    public interface IPlayerActions
    {
        
        
        
        
        
        
        void OnMove(InputAction.CallbackContext context);
        
        
        
        
        
        
        void OnBrake(InputAction.CallbackContext context);
    }
}
