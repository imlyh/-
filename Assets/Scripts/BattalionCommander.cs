using UnityEngine;
using UnityEngine.InputSystem;

public class BattalionCommander : MonoBehaviour
{
    private Battalion selectedBattalion;
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        HandleSelection();
        HandleCommand();
    }

    void HandleSelection()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 500f))
        {
            var battalion = hit.collider.GetComponentInParent<Battalion>();
            if (battalion != null && battalion.owner == BattalionOwner.Player)
                SelectBattalion(battalion);
            else
                SelectBattalion(null);
        }
        else
        {
            SelectBattalion(null);
        }
    }

    void SelectBattalion(Battalion b)
    {
        if (selectedBattalion != null)
            selectedBattalion.SetSelected(false);
        selectedBattalion = b;
        if (selectedBattalion != null)
            selectedBattalion.SetSelected(true);
    }

    void HandleCommand()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.rightButton.wasPressedThisFrame) return;
        if (selectedBattalion == null) return;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return;

        Vector3 hitPoint = ray.GetPoint(dist);
        Vector3 cellCenter = new Vector3(
            Mathf.Clamp(Mathf.Round(hitPoint.x), 0, 29),
            0,
            Mathf.Clamp(Mathf.Round(hitPoint.z), 0, 19)
        );

        CommandType type;
        if (Battalion.IsGoldMineAt(cellCenter))
            type = CommandType.Mine;
        else if (Battalion.IsEnemyAt(cellCenter, selectedBattalion.owner))
            type = CommandType.Attack;
        else
            type = CommandType.Move;

        selectedBattalion.Command(cellCenter, type);
    }
}
