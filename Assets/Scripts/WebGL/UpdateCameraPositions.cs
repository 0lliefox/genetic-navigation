using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Script for demo scene to change between first and third person
public class UpdateCameraPositions : MonoBehaviour
{
    [SerializeField]
    private Rect rect;
    [SerializeField]
    private Rect mobileRect;

    private Camera mainCamera;

    [SerializeField]
    private Vector3[] positions;

    [SerializeField]
    private Quaternion[] rotations;

    private int positionCounter = 0;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = GetComponent<Camera>();

        CameraScreenResolution _;
        bool isTopCamera = TryGetComponent<CameraScreenResolution>(out _);
        if (Screen.width < 700 && isTopCamera)
        {
            mainCamera.orthographicSize = 350;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Screen.width < 700)
        {
            mainCamera.rect = mobileRect;
        }
        else
        {
            mainCamera.rect = rect; 
        }
    }

    public void ToggleCameraPOV()
    {
        positionCounter++;

        if (positionCounter == positions.Length)
        {
            positionCounter = 0;
        }

        if (positionCounter < positions.Length)
        {
            mainCamera.transform.localPosition = positions[positionCounter];
            mainCamera.transform.localRotation = rotations[positionCounter];
        }
    }
}
