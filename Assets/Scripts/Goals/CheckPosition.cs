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
        // The loop below used to run on every physics step regardless. The flag is
        // off in the shipped scene, so it could never act on the result.
        if (!shouldMoveOnVisitedCell || cellManager == null || cellManager.numberOfCellsVisited >= 100)
        {
            return;
        }

        // Move on once every cell the goal currently sits in has been visited, to
        // push it towards unexplored parts of the city. An empty list means the
        // goal is not on a cell, in which case it stays put.
        if (currentCells.Count == 0)
        {
            return;
        }

        foreach (Cell cell in currentCells)
        {
            if (cell == null || !cell.hasVisited)
            {
                return;
            }
        }

        SetGoalPosition();
    }

    /// <summary>
    /// Drops the goal on a random road junction.
    ///
    /// This is now the only implementation. CarController had a second copy that
    /// it used for every placement after the first, with a different random range
    /// and the margin subtracted rather than added, so the goal a run started with
    /// sat a cell away from anywhere a later goal could appear. This keeps the
    /// version that did all the work and CarController calls into it.
    /// </summary>
    public void SetGoalPosition()
    {
        if (grid == null || grid.parameters == null)
        {
            return;
        }

        int gridHeight = grid.parameters.height;
        int gridWidth = grid.parameters.width;
        Vector3 gridMargin = grid.parameters.marginBetweenShapes;

        int randomX = Random.Range(-Mathf.FloorToInt(gridHeight / 2) + 1, gridHeight - Mathf.FloorToInt(gridHeight / 2));
        int randomZ = Random.Range(-Mathf.FloorToInt(gridHeight / 2), gridWidth - Mathf.FloorToInt(gridHeight / 2));

        // Offset by one margin so the goal lands between buildings rather than
        // inside one, which keeps it reachable whatever the roadblocks do.
        transform.localPosition = new Vector3(
            randomX * gridMargin.x * 2 - gridMargin.x,
            transform.localScale.y / 2,
            randomZ * gridMargin.z * 2 - gridMargin.z);
    }
}
