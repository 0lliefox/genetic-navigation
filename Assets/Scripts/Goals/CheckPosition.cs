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

        // Buildings sit on a lattice starting at the grid's own origin, spaced by
        // shapeWidth * margin. Placing the goal halfway between two of them puts it
        // in the middle of a road rather than inside a building.
        //
        // The original arithmetic worked in the goal's own local space and landed
        // it 10 units from a building centre. Buildings reach 8 units from their
        // centre and the goal reaches 10 from its own, so up to 8 units of the goal
        // ended up buried in a building, despite the comment claiming this made it
        // "always reachable".
        float spacingX = grid.parameters.shapeWidth * grid.parameters.marginBetweenShapes.x;
        float spacingZ = grid.parameters.shapeDepth * grid.parameters.marginBetweenShapes.z;

        // Gaps between buildings, so one fewer than the number of buildings.
        int gapsX = Mathf.Max(1, grid.parameters.width - 1);
        int gapsZ = Mathf.Max(1, grid.parameters.height - 1);

        int gapX = Random.Range(0, gapsX);
        int gapZ = Random.Range(0, gapsZ);

        Vector3 origin = grid.transform.position;
        transform.position = new Vector3(
            origin.x + (gapX + 0.5f) * spacingX,
            transform.position.y,
            origin.z + (gapZ + 0.5f) * spacingZ);
    }

}
