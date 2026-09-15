using UnityEngine;

public class Cell : MonoBehaviour
{
    public bool hasVisited;
    public int timesVisited = 0; // How many recurrent times this cell 

    [SerializeField] private Material visitedMat;
    [SerializeField] private Material unvisitedMat;
    [SerializeField] private CellManager cellManager;

    private Renderer cellRenderer;
    private bool appliedVisitedState;

    private void Awake()
    {
        cellRenderer = GetComponent<Renderer>();
        ApplyMaterial();
    }

    // This used to call GetComponent and reassign the material every frame for
    // every cell in the city, a hundred of each per frame for no benefit. Now it
    // only compares a bool, and still reacts if hasVisited is changed elsewhere.
    private void Update()
    {
        if (hasVisited != appliedVisitedState)
        {
            ApplyMaterial();
        }
    }

    private void ApplyMaterial()
    {
        appliedVisitedState = hasVisited;

        if (cellRenderer != null)
        {
            // sharedMaterial, not material: both states are shared assets and no
            // per-cell properties are ever set, so instancing them is wasted memory.
            cellRenderer.sharedMaterial = hasVisited ? visitedMat : unvisitedMat;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Agent"))
        {
            if (!hasVisited)
            {
                hasVisited = true;
                cellManager.numberOfCellsVisited++;
            }
            timesVisited++;
        }
    }
}
