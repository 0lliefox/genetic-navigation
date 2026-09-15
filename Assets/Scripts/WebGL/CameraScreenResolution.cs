using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Positions the camera above all training environments depending on number of environments
public class CameraScreenResolution : MonoBehaviour
{
    [SerializeField]
    private Camera mainCamera;
    [SerializeField]
    private bool maintainWidth = true;
    [SerializeField, Range(-1, 1)]
    public int adaptPosition;

    float defaultWidth;
    float defaultHeight;

    Vector3 CameraPos;

    // Start is called before the first frame update
    void Start()
    {
        CameraPos = mainCamera.transform.position;

        defaultHeight = mainCamera.orthographicSize;
        defaultWidth = mainCamera.orthographicSize * mainCamera.aspect;
    }

    // Update is called once per frame
    void Update()
    {
        if (maintainWidth && Screen.width >= 700)
        {
            mainCamera.orthographicSize = defaultWidth / mainCamera.aspect;

            mainCamera.transform.position = new Vector3(CameraPos.x, adaptPosition * (defaultHeight - mainCamera.orthographicSize) + CameraPos.y, CameraPos.z);
        }
        else
        {

            mainCamera.transform.position = new Vector3(adaptPosition * (defaultWidth - mainCamera.orthographicSize) + CameraPos.x, CameraPos.y, CameraPos.z);

        }
    }

    public void SetOrthographicSize(float size)
    {
        maintainWidth = false;
        mainCamera.orthographicSize = size; 
    }
}
