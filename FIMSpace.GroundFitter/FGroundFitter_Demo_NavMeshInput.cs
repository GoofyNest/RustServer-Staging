using UnityEngine;
using UnityEngine.AI;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_Demo_NavMeshInput : MonoBehaviour
{
	public NavMeshAgent TargetAgent;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0) && (bool)TargetAgent)
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray.origin, ray.direction, out var hitInfo) && NavMesh.SamplePosition(hitInfo.point, out var hit, 1f, 1))
			{
				TargetAgent.SetDestination(hit.position);
			}
		}
	}
}
