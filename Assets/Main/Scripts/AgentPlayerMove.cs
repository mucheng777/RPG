using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AgentPlayerMove : MonoBehaviour
{
    private NavMeshAgent playerAgent;
    void Start()
    {
        playerAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;
            bool isCollide = Physics.Raycast(ray,out hit);
            if(isCollide)
            {
                playerAgent.SetDestination(hit.point);
            }
        }
    }
}
