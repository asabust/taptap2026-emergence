using Game.Runtime.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 鼠标点击管理器
/// </summary>
public class CursorManager : MonoBehaviour
{
    [SerializeField] protected LayerMask interactLayer;
    [SerializeField] private InputActionReference pointAction;
    [SerializeField] private InputActionReference clickAction;

    private Camera mainCam;
    private Collider2D currentHover;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void OnEnable()
    {
        pointAction.action.Enable();
        clickAction.action.Enable();
    }

    void OnDisable()
    {
        pointAction.action.Disable();
        clickAction.action.Disable();
    }

    void Update()
    {
        if (GameManager.Instance.CurrentPhase != GamePhase.Gameplay) return;
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screenPos = pointAction.action.ReadValue<Vector2>();
        Vector2 worldPos = mainCam.ScreenToWorldPoint(screenPos);

        currentHover = Physics2D.OverlapPoint(worldPos, interactLayer);

        if (clickAction.action.WasPressedThisFrame() && currentHover)
        {
            currentHover
                .GetComponent<Interactable>()
                ?.Interact();
        }
    }
}