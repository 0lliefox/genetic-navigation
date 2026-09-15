using UnityEngine;

/// <summary>
/// Keeps the whole city in frame whatever the screen shape.
///
/// CameraScreenResolution, recovered from the original web build, only adjusts
/// orthographic size when the screen is at least 700px wide, so on a phone the
/// city ran off the edge. The reinforcement learning project worked around that
/// with a hardcoded orthographic size of 350, which only suits the city it was
/// written for. This measures the city instead, so it stays correct if the grid
/// size or spacing changes.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FitCameraToCity : MonoBehaviour
{
    [SerializeField] private GridWithParams grid;
    [SerializeField, Tooltip("Fraction of the viewport to leave as a border.")]
    private float margin = 0.06f;

    private Camera view;
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        view = GetComponent<Camera>();

        if (grid == null)
        {
            grid = FindFirstObjectByType<GridWithParams>();
        }
    }

    // The grid builds itself in Start, so wait a frame before measuring it.
    private void LateUpdate()
    {
        if (Screen.width == lastWidth && Screen.height == lastHeight)
        {
            return;
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;
        Fit();
    }

    private void Fit()
    {
        if (view == null || grid == null || !view.orthographic)
        {
            return;
        }

        Bounds bounds = grid.Bounds;
        if (bounds.size.x <= 0f || bounds.size.z <= 0f)
        {
            return; // City not generated yet.
        }

        // Orthographic size is half the viewport height in world units. Take
        // whichever of the two axes needs more room at this aspect ratio.
        float aspect = view.aspect <= 0f ? 1f : view.aspect;
        float forHeight = bounds.size.z * 0.5f;
        float forWidth = bounds.size.x * 0.5f / aspect;

        view.orthographicSize = Mathf.Max(forHeight, forWidth) * (1f + margin);

        Vector3 centre = bounds.center;
        view.transform.position = new Vector3(centre.x, view.transform.position.y, centre.z);
    }
}
