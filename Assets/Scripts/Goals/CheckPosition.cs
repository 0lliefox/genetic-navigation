using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPosition : MonoBehaviour
{
    [SerializeField] public GridWithParams grid;
    [SerializeField] private CellManager cellManager;
    [SerializeField] private bool shouldMoveOnVisitedCell = false;

    private List<Cell> currentCells = new List<Cell>();

    void Start()
    {
        SetGoalPosition();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Goal"))
        {
            SetGoalPosition();
        }
        if (other.CompareTag("Cell"))
        {
            currentCells.Add(other.GetComponent<Cell>());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Cell"))
        {
            currentCells.Remove(other.GetComponent<Cell>());
        }
    }

    private void FixedUpdate()
    {
        bool updatePosition = false;
        foreach (Cell cell in currentCells)
        {
            if (!cell.hasVisited)
            {
                updatePosition = false;
                break;
            }
            else
            {
                updatePosition = true;
            }
        }

        if (shouldMoveOnVisitedCell && updatePosition && cellManager.numberOfCellsVisited < 100)
        {
            SetGoalPosition();
        }
    }

    private void SetGoalPosition()
    {
        if (grid != null)
        {
            int gridHeight = grid.parameters.height;
            Vector3 gridMargin = grid.parameters.marginBetweenShapes;

            int randomX = Random.Range(-Mathf.FloorToInt(gridHeight / 2) - 1, Mathf.FloorToInt(gridHeight / 2) + 1);
            int randomZ = Random.Range(-Mathf.FloorToInt(gridHeight / 2) - 1, Mathf.FloorToInt(gridHeight / 2) + 1);

            transform.localPosition = new Vector3(randomX * gridMargin.x * 2 + gridMargin.x, transform.localScale.y / 2, randomZ * gridMargin.z * 2 + gridMargin.z);
        }
    }
}
