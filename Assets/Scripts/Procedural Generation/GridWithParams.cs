using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GridWithParams : MonoBehaviour
{
    // Layer 6 is "Wall" in TagManager; the agent's raycast masks look it up by name.
    private const int WallLayer = 6;

    [SerializeField]
    public ProceduralParam parameters = null;

    private GameObject[,] grid = null;
    private GameObject[,] roadblocks = null;

    private Material[] proceduralMaterials = null;

    [SerializeField]
    private Bounds bounds;


    void OnEnable() 
    {
        if(parameters == null)
        {
            Debug.LogError("You must set procedural parameters in order to use this class");
            enabled = false;
        }
    }

    void Initialise()
    {
        ClearAll();

        Random.InitState(parameters.randomSeed);
        grid = new GameObject[parameters.height, parameters.width];               
        proceduralMaterials = new Material[parameters.proceduralMaterialsToGenerate];
        bounds = new Bounds (Vector3.zero, Vector3.zero);

        // generate materials if needed
        if(parameters.proceduralMaterialsToGenerate > 0 && parameters.defaultMaterials.Length == 0)
            proceduralMaterials = MeshRendererExtensions.GetRandomMaterials(parameters.shaderName, parameters.proceduralMaterialsToGenerate);
    }

    void Start() {
        BuildGrid();
    }

    public void BuildGrid()
    {
        Initialise();

        grid = new GameObject[parameters.height, parameters.width];

        DateTime started = DateTime.Now;        

        for(int row = 0; row < parameters.height; row++)
        {
            for(int col = 0; col < parameters.width; col++)
            {
                StartCoroutine(AddCell(row, col));
            }
        }

        // add random roadblocks 
        BuildRoadblocks();
    }

    public void BuildRoadblocks()
    {
        if (parameters.roadblockCount > 0)
        {
            roadblocks = new GameObject[parameters.height, parameters.width];

            for (int i = 0; i < parameters.roadblockCount; i++)
            {
                GameObject roadblock = new GameObject($"roadblock_{i}");
                roadblock.transform.parent = gameObject.transform;

                int randomX = Random.Range(0, parameters.height);
                int randomZ = Random.Range(0, parameters.width);

                for (int j = 0; j < 5; j++)
                {
                    if (roadblocks[randomX, randomZ] != null)
                    {
                        randomX = Random.Range(0, parameters.height);
                        randomZ = Random.Range(0, parameters.width);
                    }
                    else
                    {
                        break;
                    }
                }

                roadblocks[randomX, randomZ] = roadblock;

                float xOffset = grid[randomX, randomZ].GetComponent<BoxCollider>().size.x;
                float zOffset = grid[randomX, randomZ].GetComponent<BoxCollider>().size.z;

                roadblock.transform.localPosition = new Vector3(randomX * parameters.marginBetweenShapes.x * 2, 0, randomZ * parameters.marginBetweenShapes.z * 2);
                MeshFilter meshFilter = roadblock.AddComponent<MeshFilter>();
                MeshRenderer renderer = roadblock.AddComponent<MeshRenderer>();

                Shape shape = null;

                // only block x or z
                if (Random.Range(0, 2) == 0)
                {
                    shape = new Cube
                    {
                        Width = 5,
                        Height = 5,
                        Depth = parameters.marginBetweenShapes.z * 2 - zOffset
                    };

                    roadblock.transform.localPosition += new Vector3(0, 0, zOffset);
                }
                else
                { 
                    shape = new Cube
                    {
                        Width = parameters.marginBetweenShapes.x * 2 - xOffset,
                        Height = 5,
                        Depth = 5
                    };

                    roadblock.transform.localPosition += new Vector3(xOffset, 0, 0);
                }

                meshFilter.mesh = shape.Generate();
                roadblock.AddComponent<BoxCollider>();
                roadblock.GetComponent<BoxCollider>().isTrigger = false;
                roadblock.tag = "Wall";
                roadblock.layer = WallLayer;

                renderer.material = parameters.roadblockMaterial;
            }
        }
    }
    
    IEnumerator AddCell(int row, int col)
    {
        GameObject cell = null;
        if(grid[row, col] == null)
        {
            cell = new GameObject($"cell_{row}_{col}");
            cell.transform.parent = gameObject.transform;
            grid[row, col] = cell;
        }   
        else {
            DestroyImmediate(grid[row, col]);
            grid[row, col] = new GameObject($"cell_{row}_{col}");
            cell = grid[row, col];
            cell.transform.parent = gameObject.transform;
        }

        cell.isStatic = parameters.makeShapesStatic;
        cell.transform.localPosition =
            Vector3.Scale(new Vector3(
                        parameters.shapeWidth * row * Random.Range(1.0f, parameters.maxRandomWidthOffset), 0,
                        parameters.shapeDepth * col * Random.Range(1.0f, parameters.maxRandomDepthOffset)),
                        parameters.marginBetweenShapes);

        MeshFilter meshFilter = cell.AddComponent<MeshFilter>();
        MeshRenderer renderer = cell.AddComponent<MeshRenderer>();

        Shape shape = null;

        if(parameters.ShapeType == ShapeTypes.Cube){
            shape = new Cube
            {
                Width = parameters.shapeWidth * Random.Range(1.0f, parameters.maxRandomWidth),
                Height = parameters.shapeHeight * Random.Range(1.0f, parameters.maxRandomHeight),
                Depth = parameters.shapeDepth * Random.Range(1.0f, parameters.maxRandomDepth)
            };
        }
        else if(parameters.ShapeType == ShapeTypes.Quad){
            shape = new Quad {
                Width = parameters.shapeWidth * Random.Range(1.0f, parameters.maxRandomWidth),
                Depth = parameters.shapeDepth * Random.Range(1.0f, parameters.maxRandomDepth),
            };
        }

        meshFilter.mesh = shape.Generate();

        if (proceduralMaterials.Length > 0 && parameters.defaultMaterials.Length == 0)
            renderer.material = proceduralMaterials[Random.Range(0, proceduralMaterials.Length)];
        else if(proceduralMaterials.Length == 0 && parameters.defaultMaterials.Length > 0)
            renderer.material = parameters.defaultMaterials[Random.Range(0, parameters.defaultMaterials.Length)];

        if (parameters.shouldGenerateRigidBodies)
        {
            cell.AddComponent<Rigidbody>();
        }

        // One collider only. Previously the rigidbody branch above added its own,
        // so with rigidbodies enabled a cell ended up with two: GetComponent
        // returned the first, meaning the trigger flag was applied to that one and
        // the second kept its default. Inert in the shipped presets, which all
        // have shouldGenerateRigidBodies off, but wrong.
        BoxCollider collider = cell.AddComponent<BoxCollider>();
        collider.isTrigger = parameters.shouldGenerateTriggers;
        cell.tag = "Wall";
        cell.layer = WallLayer;
        
        // Calculate bounds
        bounds.Encapsulate(renderer.bounds);

        yield return null;
    }

    public Bounds Bounds
    {
        get 
        {
            return bounds;
        }
    }

    private void ClearAll()
    {
        if(grid?.Length > 0) 
        {
            for(int row = 0; row < grid.GetLength(0); row++)
            {
                for(int col = 0; col < grid.GetLength(1); col++)
                {
                    if(grid[row, col] != null)
                        Destroy(grid[row, col]);
                }
            }
        }

        if(transform.childCount > 0)
        {
            foreach(Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }
        grid = null;
    }

    public void ClearRoadblocks()
    {
        if (roadblocks?.Length > 0)
        {
            for (int row = 0; row < roadblocks.GetLength(0); row++)
            {
                for (int col = 0; col < roadblocks.GetLength(1); col++)
                {
                    if (roadblocks[row, col] != null)
                        Destroy(roadblocks[row, col]);
                }
            }
        }

        roadblocks = null;
    }

    public void SetRoadblockCount(float count)
    {
        int roadblockCount;
        bool success = int.TryParse(count.ToString(), out roadblockCount);
        if (success)
        {
            parameters.roadblockCount = roadblockCount;
        }
    }

    public void SetRoadblockText(Text text)
    {
        text.text = parameters.roadblockCount.ToString();
    }
}
