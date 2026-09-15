using System;
using System.Collections;
using System.Text.RegularExpressions;
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

    // Layout of the `sensors` array. Each of RayDirections rays writes into three
    // banks - wall, goal, breadcrumb - followed by six scalar inputs (velocity x/z,
    // distance to target, direction to target x/y/z). Named so the banks cannot
    // drift apart again the way the goal-visibility test below once did.
    private const int RayDirections = 4;
    private const int WallSensorOffset = 0;
    private const int GoalSensorOffset = RayDirections;
    private const int BreadcrumbSensorOffset = RayDirections * 2;
    private const int ScalarSensorOffset = RayDirections * 3;

    // An agent that has moved less than this has not meaningfully navigated
    // anywhere. Used only to stop the goal fitness dividing by zero.
    private const float MinimumTravelDistance = 1f;

    // Credit per goal reached, on top of the best the agent managed. Matches the
    // scale of the goal branch of the fitness function below.
    private const float GoalReward = 20f;

    // LayerMask.GetMask does a string lookup. It was being called twelve times per
    // agent per physics step; the masks never change, so resolve them once.
    //
    // Resolved on first use rather than in a field initializer: Unity forbids
    // NameToLayer there and throws a TypeInitializationException, which takes the
    // whole component with it.
    private static int solidMask = -1;
    private static int solidAndBreadcrumbMask = -1;

    private static int SolidMask =>
        solidMask >= 0 ? solidMask : (solidMask = LayerMask.GetMask("Goal", "Wall"));

    private static int SolidAndBreadcrumbMask =>
        solidAndBreadcrumbMask >= 0
            ? solidAndBreadcrumbMask
            : (solidAndBreadcrumbMask = LayerMask.GetMask("Goal", "Wall", "Breadcrumb"));

    // Reused rather than allocating a new List every physics step.
    private readonly Vector3[] rayDirections = new Vector3[RayDirections];

    [Header("Environment")]
    private GameObject target;

    /// <summary>Where the goal currently is, for the training recorder.</summary>
    public Vector3 TargetPosition => target != null ? target.transform.position : Vector3.zero;
    private CheckPosition targetPlacement;
    private Rigidbody rbd;
    private GridWithParams grid;

    private Vector3 lastPosition;
    private float totalDistanceTravelled = 0;
    private float bestEpisodeFitness = 0f;
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
        bestEpisodeFitness = 0f;
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
                targetPlacement = tr.GetComponent<CheckPosition>();
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

            // The target has moved, so this agent has not reached the new one.
            // Credit for the goal already banked in bestEpisodeFitness and in
            // numberOfGoalsReached above.
            if (randomiseGoalPosition)
            {
                reachedGoal = false;
            }

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
        // Placement lives on the goal itself now. This used to be a second copy of
        // the same logic that disagreed with the one in CheckPosition, so the goal
        // a run started with came from a different distribution to every goal after.
        if (targetPlacement != null)
        {
            targetPlacement.SetGoalPosition();
        }
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
        Vector3 forward = transform.forward;
        rayDirections[0] = Quaternion.Euler(0, -90f, 0) * forward;
        rayDirections[1] = forward;
        rayDirections[2] = Quaternion.Euler(0, 90f, 0) * forward;
        rayDirections[3] = Quaternion.Euler(0, -180f, 0) * forward;

        const float maxDistance = 50f;
        Vector3 origin = transform.position;

        for (int i = 0; i < RayDirections; i++)
        {
            Ray ray = new Ray(origin, rayDirections[i]);

            // One cast serves both the wall and goal banks. These were two separate
            // casts with byte-identical arguments, differing only in which tag they
            // tested on the result, so the second was pure waste.
            //
            // Note the masks are deliberate, not incidental: a bank reports only when
            // something of its own type is the *nearest* hit. That is what stops an
            // agent seeing the goal through a wall.
            float wall = 0f;
            float goal = 0f;
            if (Physics.Raycast(ray, out RaycastHit solidHit, maxDistance, SolidMask))
            {
                if (solidHit.transform.CompareTag("Wall"))
                {
                    wall = solidHit.distance;
                    DrawSensorRay(ray.origin, solidHit.point, Color.red);
                }
                else if (solidHit.transform.CompareTag("Goal"))
                {
                    goal = solidHit.distance;
                    DrawSensorRay(ray.origin, solidHit.point, Color.yellow);
                }
            }
            sensors[WallSensorOffset + i] = wall;
            sensors[GoalSensorOffset + i] = goal;

            // Breadcrumbs need their own cast, because walls and goals occlude them
            // but breadcrumbs occlude nothing: a marker should not hide a wall.
            float breadcrumb = 0f;
            if (Physics.Raycast(ray, out RaycastHit anyHit, maxDistance, SolidAndBreadcrumbMask)
                && anyHit.transform.CompareTag("Breadcrumb"))
            {
                breadcrumb = anyHit.distance;
                DrawSensorRay(ray.origin, anyHit.point, Color.blue);
            }
            sensors[BreadcrumbSensorOffset + i] = breadcrumb;
        }

        // velocity
        sensors[ScalarSensorOffset + 0] = rbd.linearVelocity.x;
        sensors[ScalarSensorOffset + 1] = rbd.linearVelocity.z;

        // distance to target
        sensors[ScalarSensorOffset + 2] = Vector3.Distance(transform.localPosition, target.transform.localPosition);

        // direction to target
        sensors[ScalarSensorOffset + 3] = (target.transform.position - transform.position).normalized.x;
        sensors[ScalarSensorOffset + 4] = (target.transform.position - transform.position).normalized.y;
        sensors[ScalarSensorOffset + 5] = (target.transform.position - transform.position).normalized.z;

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


    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void DrawSensorRay(Vector3 from, Vector3 to, Color colour)
    {
        Debug.DrawLine(from, to, colour);
    }

    /// <summary>
    /// Index of the largest network output, i.e. the chosen action.
    ///
    /// Replaces act.ToList().IndexOf(act.Max()), which allocated a List and walked
    /// the array twice, once per agent per physics step.
    /// </summary>
    private static int IndexOfLargest(float[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }
        return best;
    }

    public void MoveCar(float[] act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        switch (IndexOfLargest(act))
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

    /// <summary>
    /// True when any of the goal rays currently has a hit.
    ///
    /// This previously read sensors[5..8], which missed goal ray 0 and instead
    /// included the first breadcrumb ray - so the "can see the goal" term of the
    /// fitness function was partly wired to breadcrumbs. Left over from an earlier
    /// nine-ray layout (see the commented block in InputSensors).
    /// </summary>
    private bool CanSeeGoal()
    {
        for (int i = 0; i < RayDirections; i++)
        {
            if (sensors[GoalSensorOffset + i] != 0f)
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateFitness()
    {
        // Measure the step before advancing the reference point. The original
        // assigned lastPosition immediately before calling this, so the distance
        // below was always exactly zero - which made totalDistanceTravelled stay
        // at zero and the goal fitness divide by it, yielding Infinity.
        float stepDistance = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        totalDistanceTravelled += stepDistance;
        geneticManager.totalDistanceCovered += stepDistance;

        float distanceToTarget = Vector3.Distance(transform.localPosition, target.transform.localPosition);
        if (reachedGoal)
        {
            double travelled = Math.Max(totalDistanceTravelled, MinimumTravelDistance);
            fitness = (float)(20 + (100 / Math.Pow(travelled, 2)));
        }
        else
        {
            bool canSeeGoal = CanSeeGoal();
            fitness = (float)(1 / Math.Pow(distanceToTarget + (canSeeGoal ? 0 : 10) + 0.001 * numOfCollisions, 2)); // F7

            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)); //F1
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)(0.01 * numOfCollisions); // F2
            //fitness = (float)(10 / Math.Pow(distanceToTarget + numOfCollisions * 0.01, 2)); // F3
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)Math.Pow(numOfCollisions, 1 / distanceToTarget); // F4
            //fitness = (float)(10 / Math.Pow(distanceToTarget, 2)) - (float)Math.Pow(0.001 * numOfCollisions, distanceToTarget);  // F5
            //fitness = (float)(1 / Math.Pow(distanceToTarget + (canSeeGoal ? 0 : 10), 2)); // F6
        }
        // Score the whole episode, not the instant it ended. This used to assign
        // the current value every step, so an agent was judged purely on where it
        // happened to be when it last updated: one that navigated well then
        // drifted scored badly, and one that parked beside the goal scored well.
        // That is what taught agents to hover near the goal instead of entering it.
        if (fitness > bestEpisodeFitness)
        {
            bestEpisodeFitness = fitness;
        }

        network.fitness = bestEpisodeFitness + GoalReward * numberOfGoalsReached;

        if (geneticManager.maximumFitness < fitness)
        {
            geneticManager.maximumFitness = fitness;
        }
    }
}
