using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Used to calculate the maximum amount of roadblocks for a given city size
public class GetMaxRoadblocks : MonoBehaviour
{
    [SerializeField]
    private GridWithParams grid;

    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Slider>().maxValue = (grid.parameters.width * (grid.parameters.width - 1)); 
    }
}
