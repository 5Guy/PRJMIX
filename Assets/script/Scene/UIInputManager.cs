using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIInputManager : MonoBehaviour
{
    private InputSystem_Actions inputActions;

    public event Action OnPausePressed;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.UI.Enable();
        inputActions.UI.ESC.performed += HandlePause;
    }

    // Awake가 돌기 전에 꺼지거나(비활성 상태로 만들어진 오브젝트), 씬을 내리는 중에 불릴 수 있다.
    // 그때 inputActions는 아직/이미 없으므로 그냥 넘어간다.
    private void OnDisable()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.UI.ESC.performed -= HandlePause;
        inputActions.UI.Disable();
    }

    private void HandlePause(InputAction.CallbackContext context)
    {
        if (StageFailPanel.IsOpen)
        {
            return;
        }

        // 조합창(Tab)이 열려 있으면 Esc는 일시정지가 아니라 Tab과 똑같이 조합창만 닫는다.
        if (CraftingPanelUI.IsOpen)
        {
            CraftingPanelUI.CloseIfOpen();
            return;
        }

        // 도감이 열려 있으면 Esc는 도감만 닫는다.
        if (EncyclopediaPanel.IsOpen)
        {
            EncyclopediaPanel.CloseIfOpen();
            return;
        }

        OnPausePressed?.Invoke();
    }
}
