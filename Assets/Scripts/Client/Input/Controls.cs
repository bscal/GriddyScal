// GENERATED AUTOMATICALLY FROM 'Assets/Scripts/Client/Input/Controls.inputactions'

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class @Controls : IInputActionCollection, IDisposable
{
    public InputActionAsset asset { get; }
    public @Controls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""Controls"",
    ""maps"": [
        {
            ""name"": ""New action map"",
            ""id"": ""a8477478-d581-472e-8312-50c10e3b861f"",
            ""actions"": [
                {
                    ""name"": ""CameraMove"",
                    ""type"": ""Value"",
                    ""id"": ""f75618bf-ca33-40d2-ab79-e319ef95aff1"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""CameraZoom"",
                    ""type"": ""Value"",
                    ""id"": ""b4817418-6d22-43b7-be3e-6dad5140c97d"",
                    ""expectedControlType"": ""Axis"",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""fc24215e-f89d-4689-974e-652b13fc70b5"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""CameraMove"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""1855e1d1-717c-49cb-afe9-c2774fcf2bd7"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""MainKeyboard"",
                    ""action"": ""CameraMove"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""1bb48f06-fc17-41c2-8d40-8c7ee4eb380f"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""MainKeyboard"",
                    ""action"": ""CameraMove"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""8652f1cf-4c5f-472e-a947-61ec03ec15ad"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""MainKeyboard"",
                    ""action"": ""CameraMove"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""ceb5cb12-6d43-44db-bec5-8ca8365fb5ea"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""MainKeyboard"",
                    ""action"": ""CameraMove"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""6cd53681-15a6-4301-9b12-f2f93a23c1c3"",
                    ""path"": ""<Mouse>/scroll/y"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""CameraZoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""MainKeyboard"",
            ""bindingGroup"": ""MainKeyboard"",
            ""devices"": [
                {
                    ""devicePath"": ""<Keyboard>"",
                    ""isOptional"": false,
                    ""isOR"": false
                },
                {
                    ""devicePath"": ""<Mouse>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        }
    ]
}");
        // New action map
        m_Newactionmap = asset.FindActionMap("New action map", throwIfNotFound: true);
        m_Newactionmap_CameraMove = m_Newactionmap.FindAction("CameraMove", throwIfNotFound: true);
        m_Newactionmap_CameraZoom = m_Newactionmap.FindAction("CameraZoom", throwIfNotFound: true);
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

    // New action map
    private readonly InputActionMap m_Newactionmap;
    private INewactionmapActions m_NewactionmapActionsCallbackInterface;
    private readonly InputAction m_Newactionmap_CameraMove;
    private readonly InputAction m_Newactionmap_CameraZoom;
    public struct NewactionmapActions
    {
        private @Controls m_Wrapper;
        public NewactionmapActions(@Controls wrapper) { m_Wrapper = wrapper; }
        public InputAction @CameraMove => m_Wrapper.m_Newactionmap_CameraMove;
        public InputAction @CameraZoom => m_Wrapper.m_Newactionmap_CameraZoom;
        public InputActionMap Get() { return m_Wrapper.m_Newactionmap; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(NewactionmapActions set) { return set.Get(); }
        public void SetCallbacks(INewactionmapActions instance)
        {
            if (m_Wrapper.m_NewactionmapActionsCallbackInterface != null)
            {
                @CameraMove.started -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraMove;
                @CameraMove.performed -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraMove;
                @CameraMove.canceled -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraMove;
                @CameraZoom.started -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraZoom;
                @CameraZoom.performed -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraZoom;
                @CameraZoom.canceled -= m_Wrapper.m_NewactionmapActionsCallbackInterface.OnCameraZoom;
            }
            m_Wrapper.m_NewactionmapActionsCallbackInterface = instance;
            if (instance != null)
            {
                @CameraMove.started += instance.OnCameraMove;
                @CameraMove.performed += instance.OnCameraMove;
                @CameraMove.canceled += instance.OnCameraMove;
                @CameraZoom.started += instance.OnCameraZoom;
                @CameraZoom.performed += instance.OnCameraZoom;
                @CameraZoom.canceled += instance.OnCameraZoom;
            }
        }
    }
    public NewactionmapActions @Newactionmap => new NewactionmapActions(this);
    private int m_MainKeyboardSchemeIndex = -1;
    public InputControlScheme MainKeyboardScheme
    {
        get
        {
            if (m_MainKeyboardSchemeIndex == -1) m_MainKeyboardSchemeIndex = asset.FindControlSchemeIndex("MainKeyboard");
            return asset.controlSchemes[m_MainKeyboardSchemeIndex];
        }
    }
    public interface INewactionmapActions
    {
        void OnCameraMove(InputAction.CallbackContext context);
        void OnCameraZoom(InputAction.CallbackContext context);
    }
}
