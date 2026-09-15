using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using Random = UnityEngine.Random;

public class CarController : MonoBehaviour
{
    [Header("Agent Controls")]
    [SerializeField, Range(0, 100)] public int agentNo = 0;
    [SerializeField] private GameObject breadcrumbPrefab;
    [SerializeField] private bool leaveBreadcrumbs = true;
    [SerializeField] public bool randomiseGoalPosition = true;
    [SerializeField] public bool isRandomGrid = true;
    [SerializeField] private float fitness = 0;
    private int numberOfGoalsReached = 0;

    private Vector3 startPosition, startRotation;
    public NeuralNetwork network = null;

    public float timeSinceStart = 0f; 

    [Header("Fitness")]
    public float overallFitness;

    [Header("Network Options")]
    [SerializeField] public static int LAYERS = 18;

    [Header("Environment")]
    private GameObject target;
    private Rigidbody rbd;
    private GridWithParams grid;

    private Vector3 lastPosition;
    private float totalDistanceTravelled = 0;
    private float avgSpeed;
    [SerializeField] private float numOfCollisions = 0;

    private float[] sensors = new float[LAYERS];

    private Manager geneticManager;
    private bool reachedGoal = false;
    private bool collided = false;
    public bool active = true;
    public bool reset = false;
    public Vector3 velocity;

    public float resetTimer = 0f;
    private void Awake() 
    {
        rbd = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.eulerAngles;
        lastPosition = startPosition;
        totalDistanceTravelled = 0f;
        numOfCollisions = 0f;
        reachedGoal = false;
        collided = false;
        active = true;
        numberOfGoalsReached = 0;

        foreach (Transform tr in transform.parent)
        {
            if (tr.tag == "Goal")
            {
                target = tr.gameObject;
            }
            if (tr.tag == "Grid")
            {
                grid = tr.GetComponent<GridWithParams>();
            }
            if (tr.tag == "Manager")
            {
                geneticManager = tr.GetComponent<Manager>();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Goal"))
        {
            reachedGoal = true;
            UpdateFitness();

            if (randomiseGoalPosition)
            {
                geneticManager.goalReached = true;
                RandomiseTargetPos();
            }

            geneticManager.numberOfGoals += 1;
            numberOfGoalsReached += 1;

        }
        if (other.CompareTag("Wall"))
        {
            geneticManager.numberOfCollisions++;
            numOfCollisions += 1;
            Stop();
        }
    }

    private void Stop()
    {
        rbd.linearVelocity = Vector3.zero;
        collided = true;
        active = false;
        UpdateFitness();
    }

    public void RandomiseTargetPos()
    {
        int gridHeight = grid.parameters.height;
        int gridWidth = grid.parameters.width;
        Vector3 gridMargin = grid.parameters.marginBetweenShapes;

        // Get random building in environment
        int randomX = Random.Range(-Mathf.FloorToInt(gridHeight / 2) + 1, gridHeight - Mathf.FloorToInt(gridHeight / 2));
        int randomZ = Random.Range(-Mathf.FloorToInt(gridHeight / 2), gridWidth - Mathf.FloorToInt(gridHeight / 2));

        // Position target in centre of a building so that it is always reachable despite roadblocks
        target.transform.localPosition = new Vector3(randomX * gridMargin.x * 2 - gridMargin.x, target.transform.localScale.y / 2, randomZ * gridMargin.z * 2 - gridMargin.z);
    }

    public void BuildRandomGrid(int randomSeedNo)
    {
        foreach (Transform tr in transform.parent) 
        {
            if (tr.tag == "Grid")
            {
                grid = tr.GetComponent<GridWithParams>();
                
                grid.parameters.randomSeed = randomSeedNo;

                grid.BuildGrid();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!collided)
        {
            if (other.CompareTag("Breadcrumb"))
            {
                // If breadcrumb has been active for more than 1 second, then penalise for colliding with it
                if (other.GetComponent<Breadcrumb>().newBreadcrumb == false && Regex.Match(other.name, @"\d+").Value == agentNo.ToString())
                {
                    numOfCollisions += 1;
                    geneticManager.numberOfCollisions++;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        velocity = rbd.linearVelocity;

        // attempt at resetting all agents when stationary or collided but did not work...
        //resetTimer++;
        //if (active && !reset && resetTimer > 500)
        //{
        //    if (rbd.velocity.x >= -0.1f && rbd.velocity.x <= 0.1f && 
        //        rbd.velocity.z >= -0.1f && rbd.velocity.z <= 0.1f)
        //    {
        //        active = false;
        //        reset = true;
        //        resetTimer = 0f;
        //        geneticManager.forceResetCounter++;
        //    } else if ((rbd.velocity.x == 0.8642743f || rbd.velocity.x == 0.8642745f || rbd.velocity.x == -0.8642743f || rbd.velocity.x == -0.8642745f && rbd.velocity.z >= -0.1f && rbd.velocity.z <= 0.1f) ||
        //        (rbd.velocity.z == 0.8642745f || rbd.velocity.z == 0.8642743f || rbd.velocity.z == -0.8642745f || rbd.velocity.z == -0.8642743f && rbd.velocity.x >= -0.1f && rbd.velocity.x <= 0.1f)) 
        //    {
        //        active = false;
        //        reset = true;
        //        resetTimer = 0f;
        //        geneticManager.forceResetCounter++;
        //    }
        //}
        //else if (!active && resetTimer > 500 && !reset)
        //{
        //    resetTimer = 0f;
        //    reset = true; 
        //    geneticManager.forceResetCounter++;
        //}

        if (!collided && network != null)
        {
            InputSensors();

            float[] ouput = network.FeedForward(sensors);
            MoveCar(ouput);

            lastPosition = transform.position;
            UpdateFitness();

            if (leaveBreadcrumbs)
            {
                if (placingBreadcrumb == false)
                {
                    StartCoroutine(PlaceBreadcrumb());
                }
            }
        }
    }

    private bool placingBreadcrumb = false;
    IEnumerator PlaceBreadcrumb()
    {
        placingBreadcrumb = true;
        GameObject breadcrumb = Instantiate(breadcrumbPrefab);
        breadcrumb.transform.parent = transform.parent;
        breadcrumb.name = $"Breadcrumb{agentNo}";
        breadcrumb.transform.localPosition = transform.localPosition;
        yield return new WaitForSeconds(0.5f);
        placingBreadcrumb = false;
    }

    public void RemoveAllBreadcrumbs()
    {
        foreach (Transform child in transform.parent)
        {
            if (child.tag == "Breadcrumb" && child.name == $"Breadcrumb{agentNo}")
            {
                Destroy(child.gameObject); 
            }
        }
    }

    private void InputSensors() 
    {
        // Same raycasts as RL (4 each direction up to 75 deg)
        //List<Vector3> raycasts = new List<Vector3>()
        //{
        //    (Quaternion.Euler(0, -75f, 0) * transform.forward),
        //    (Quaternion.Euler(0, -56.25f, 0) * transform.forward),
        //    (Quaternion.Euler(0, -37.5f, 0) * transform.forward),
        //    (Quaternion.Euler(0, -18.75f, 0) * transform.forward),
        //    (transform.forward),
        //    (Quaternion.Euler(0, 75f, 0) * transform.forward),
        //    (Quaternion.Euler(0, 56.25f, 0) * transform.forward),
        //    (Quaternion.Euler(0, 37.5f, 0) * transform.forward),
        //    (Quaternion.Euler(0, 18.75f, 0) * transform.forward)
        //};

        // NSWE directions
        List<Vector3> raycasts = new List<Vector3>()
        {
            (Quaternion.Euler(0, -90f, 0) * transform.forward),
            (transform.forward),
            (Quaternion.Euler(0, 90f, 0) * transform.forward),
            (Quaternion.Euler(0, -180f, 0) * transform.forward),
        };

        float maxDistance = 50;

        // WALL 
        for (int i = 0; i < raycasts.Count; i++) {
            Ray r = new Ray(transform.position, raycasts[i]);
            if (Physics.Raycast(r, out RaycastHit hit, maxDistance, LayerMask.GetMask("Goal", "Wall")) && hit.transform.CompareTag("Wall"))
            {
                //sensors[i] = (maxDistance - hit.distance) / maxDistance;
                sensors[i] = hit.distance;
                Debug.DrawLine(r.origin, hit.point, Color.red);
            }
            else
            {
                sensors[i] = 0;
            }
        }

        // GOAL
        for (int i = 0; i < raycasts.Count; i++)
        {
            Ray r = new Ray(transform.position, raycasts[i]);
            if (Physics.Raycast(r, out RaycastHit hit, maxDistance, LayerMask.GetMask("Goal", "Wall")) && hit.transform.CompareTag("Goal")) // Make sure it doesn't look through walls to see goal
            {
                sensors[i + raycasts.Count] = hit.distance; 
                Debug.DrawLine(r.origin, hit.point, Color.yellow);
            }
            else
            {
                sensors[i + raycasts.Count] = 0;
            }
        }

        // BREADCRUMBS
        for (int i = 0; i < raycasts.Count; i++)
        {
            Ray r = new Ray(transform.position, raycasts[i]);
            if (Physics.Raycast(r, out RaycastHit hit, maxDistance, LayerMask.GetMask("Goal", "Wall", "Breadcrumb")) && hit.transform.CompareTag("Breadcrumb"))
            {
                sensors[i + raycasts.Count * 2] = hit.distance;
                Debug.DrawLine(r.origin, hit.point, Color.blue);
            }
            else
            {
                sensors[i + raycasts.Count * 2] = 0;
            }
        }

        // velocity
        sensors[raycasts.Count * 3] = rbd.linearVelocity.x;
        sensors[raycasts.Count * 3 + 1] = rbd.linearVelocity.z;

        // distance to target
        sensors[raycasts.Count * 3 + 2] = Vector3.Distance(transform.localPosition, target.transform.localPosition);

        // direction to target
        sensors[raycasts.Count * 3 + 3] = (target.transform.position - transform.position).normalized.x;
        sensors[raycasts.Count * 3 + 4] = (target.transform.position - transform.position).normalized.y;
        sensors[raycasts.Count * 3 + 5] = (target.transform.position - transform.position).normalized.z;

        //sensors[raycasts.Count + 6] = transform.position.x;
        //sensors[raycasts.Count + 7] = transform.position.y;
        //sensors[raycasts.Count + 8] = transform.position.z;

        //sensors[raycasts.Count * 2 + 6] = numOfCollisions;

        // forward transform
        //sensors[raycasts.Count + 7] = transform.forward.x;
        //sensors[raycasts.Count + 8] = transform.forward.y;
        //sensors[raycasts.Count + 9] = transform.forward.z;

        //sensors[raycasts.Count + 10] = transform.right.x;
        //sensors[raycasts.Count + 11] = transform.right.y;
        //sensors[raycasts.Count + 12] = transform.right.z;
    }

    public void MoveCar(float[] act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        switch (act.ToList().IndexOf(act.Max()))
        {
            case 0:
                dirToGo = Vector3.zero;
                break;
            case 1:
                dirToGo = transform.forward * 1f;
                break;
            case 2:
                dirToGo = transform.forward * -1f;
                break;
            //case 3:
            //    rotateDir = transform.up * 1f;
            //    break;
            //case 4:
            //    rotateDir = transform.up * -1f;
            //    break;
            case 3:
                dirToGo = transform.right * 1f;
                break;
            case 4:
                dirToGo = transform.right * -1f;
                break;
        }
        transform.Rotate(rotateDir, Time.deltaTime * 200f);
        if (dirToGo == Vector3.zero)
        {
            rbd.linearVelocity = dirToGo;
        } 
        else
        {
            rbd.AddForce(dirToGo * 2f, ForceMode.VelocityChange);
        }
    }

    public void UpdateFitness()
    {
        totalDistanceTravelled += Vector3.Distance(transform.position, lastPosition);
        geneticManager.totalDistanceCovered += totalDistanceTravelled;

        float distanceToTarget = Vector3.Distance(transform.localPosition, target.transform.localPosition);
        if (reachedGoal)
        {
            fitness = (float)(20 + (100 / Math.Pow(totalDistanceTravelled, 2)));
        }
        else
        {
            bool canSeeGoal = sensors[5] != 0 || sensors[6] != 0 || sensors[7] != 0 || sensors[8] != 0;
            fitness = (float)(1 / Math.Pow(distanceToTarget + (canSeeGoal ? 0 : 10) + 0.001 * numOfCollisions, 2)); // F7

            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)); //F1
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)(0.01 * numOfCollisions); // F2
            //fitness = (float)(10 / Math.Pow(distanceToTarget + numOfCollisions * 0.01, 2)); // F3
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)Math.Pow(numOfCollisions, 1 / distanceToTarget); // F4
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)Math.Pow(0.001 * numOfCollisions, distanceToTarget);  // F5
            //fitness = (float)(1 / Math.Pow(distanceToTarget + (canSeeGoal ? 0 : 10), 2)); // F6
        }
        network.fitness = fitness;

        if (geneticManager.maximumFitness < fitness)
        {
            geneticManager.maximumFitness = fitness;
        }
    }
}
