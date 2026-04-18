using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class SegmentGen : MonoBehaviour
{
    public GameObject[] segment;
    [SerializeField] int zPos = 50;
    [SerializeField] bool creatingSegment = false;
    [SerializeField] int segmentNum;


    void Update()
    {
        if(creatingSegment == false)
        {
            creatingSegment = true;
            StartCoroutine(SegmentGenerate());
        }
        
    }

    IEnumerator SegmentGenerate()
    {
        segmentNum = Random.Range(0, 2);
        Instantiate(segment[segmentNum], new Vector3(0, 0, zPos), Quaternion.identity);
        zPos += 50;
        yield return new WaitForSeconds(3);
        creatingSegment = false;
    }

}