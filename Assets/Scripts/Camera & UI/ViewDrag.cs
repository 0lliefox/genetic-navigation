using UnityEngine;

/// <summary>
/// Click or drag to pan the camera, pinch or scroll to zoom.
///
/// Panning already worked on touch because Unity synthesises mouse events from a
/// single finger. The two finger branch read both touches, measured the distance
/// between them and then threw it away: pinch to zoom was started and never
/// finished, in this project or in reinforcement-navigation.
/// </summary>
public class ViewDrag : MonoBehaviour
{
    [SerializeField, Tooltip("World units of zoom per pixel of pinch.")]
    private float pinchSensitivity = 0.35f;
    [SerializeField, Tooltip("World units of zoom per scroll wheel notch.")]
    private float scrollSensitivity = 12f;
    [SerializeField, Tooltip("Closest zoom, as a fraction of the starting view.")]
    private float minZoomFraction = 0.15f;
    [SerializeField, Tooltip("Furthest zoom, as a fraction of the starting view.")]
    private float maxZoomFraction = 1.25f;

    private Vector3 hit_position = Vector3.zero;
    private Vector3 current_position = Vector3.zero;
    private Vector3 camera_position = Vector3.zero;

    private Camera view;
    private float previousPinchDistance = -1f;
    private float baselineSize = -1f;

    private void Awake()
    {
        view = GetComponent<Camera>();
        if (view == null)
        {
            view = Camera.main;
        }
    }

    private void Update()
    {
        if (view == null)
        {
            return;
        }

        // Two fingers means a pinch. Unity still reports mouse events while both
        // are down, so return early rather than letting panning fight the zoom.
        if (Input.touchCount >= 2)
        {
            Pinch();
            return;
        }
        previousPinchDistance = -1f;

        if (Input.GetMouseButtonDown(0))
        {
            hit_position = Input.mousePosition;
            camera_position = transform.position;
        }

        if (Input.GetMouseButton(0))
        {
            current_position = Input.mousePosition;
            LeftMouseDrag();
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Zoom(-scroll * scrollSensitivity);
        }
    }

    private void Pinch()
    {
        float distance = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);

        if (previousPinchDistance > 0f)
        {
            // Fingers apart means zoom in, which is a smaller orthographic size.
            Zoom((previousPinchDistance - distance) * pinchSensitivity);
        }

        previousPinchDistance = distance;
    }

    private void Zoom(float amount)
    {
        if (!view.orthographic)
        {
            return;
        }

        // Captured on first use rather than in Awake, because the camera is fitted
        // to the city after the grid generates, which happens later than Awake.
        if (baselineSize <= 0f)
        {
            baselineSize = view.orthographicSize;
        }

        view.orthographicSize = Mathf.Clamp(
            view.orthographicSize + amount,
            baselineSize * minZoomFraction,
            baselineSize * maxZoomFraction);
    }

    private void LeftMouseDrag()
    {
        // From the Unity3D docs: "The z position is in world units from the camera."  In my case I'm using the y-axis as height
        // with my camera facing back down the y-axis.  You can ignore this when the camera is orthograhic.
        current_position.z = hit_position.z = camera_position.y;

        // Get direction of movement.  (Note: Don't normalize, the magnitude of change is going to be Vector3.Distance(current_position-hit_position)
        // anyways.  
        Vector3 direction = view.ScreenToWorldPoint(current_position) - view.ScreenToWorldPoint(hit_position);

        // Invert direction to that terrain appears to move with the mouse.
        direction = direction * -1;

        transform.position = camera_position + direction;
    }
}
