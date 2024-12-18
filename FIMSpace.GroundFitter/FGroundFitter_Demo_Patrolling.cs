using FIMSpace.Basics;
using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_Demo_Patrolling : MonoBehaviour
{
	public Vector4 MovementRandomPointRange = new Vector4(25f, -25f, 25f, -25f);

	public float speed = 1f;

	private Transform bodyTransform;

	private float bodyRotateSpeed = 5f;

	private Animator animator;

	private FGroundFitter fitter;

	private float timer;

	private Vector3 targetPoint;

	private bool onDestination;

	private FAnimationClips clips;

	private void Start()
	{
		fitter = GetComponent<FGroundFitter>();
		animator = GetComponentInChildren<Animator>();
		timer = Random.Range(1f, 5f);
		if (base.name.Contains("Fpider"))
		{
			bodyTransform = base.transform.GetChild(0).Find("BSkeleton").GetChild(0)
				.Find("Body_Shield");
		}
		base.transform.rotation = Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);
		fitter.UpAxisRotation = base.transform.rotation.eulerAngles.y;
		onDestination = true;
		base.transform.localScale = Vector3.one * Random.Range(0.5f, 1f);
		clips = new FAnimationClips(animator);
		clips.AddClip("Idle");
		clips.AddClip("Move");
	}

	private void Update()
	{
		if (onDestination)
		{
			timer -= Time.deltaTime;
			if (timer < 0f)
			{
				ChooseNewDestination();
			}
			bodyRotateSpeed = Mathf.Lerp(bodyRotateSpeed, 50f, Time.deltaTime * 2f);
		}
		else
		{
			if ((bool)fitter.LastRaycast.transform)
			{
				base.transform.position = fitter.LastRaycast.point;
			}
			base.transform.position += base.transform.forward * speed * Time.deltaTime;
			if (Vector3.Distance(base.transform.position, targetPoint) < 2f)
			{
				ReachDestination();
			}
			Quaternion quaternion = Quaternion.LookRotation(targetPoint - base.transform.position);
			fitter.UpAxisRotation = Mathf.LerpAngle(fitter.UpAxisRotation, quaternion.eulerAngles.y, Time.deltaTime * 7f);
			bodyRotateSpeed = Mathf.Lerp(bodyRotateSpeed, -250f, Time.deltaTime * 3f);
		}
		if ((bool)bodyTransform)
		{
			bodyTransform.Rotate(0f, 0f, Time.deltaTime * bodyRotateSpeed);
		}
	}

	private void ChooseNewDestination()
	{
		targetPoint = new Vector3(Random.Range(MovementRandomPointRange.x, MovementRandomPointRange.y), 0f, Random.Range(MovementRandomPointRange.z, MovementRandomPointRange.w));
		Physics.Raycast(targetPoint + Vector3.up * 1000f, Vector3.down, out var hitInfo, float.PositiveInfinity, fitter.GroundLayerMask, QueryTriggerInteraction.Ignore);
		if ((bool)hitInfo.transform)
		{
			targetPoint = hitInfo.point;
		}
		animator.CrossFadeInFixedTime(clips["Move"], 0.25f);
		onDestination = false;
	}

	private void ReachDestination()
	{
		timer = Random.Range(1f, 5f);
		onDestination = true;
		animator.CrossFadeInFixedTime(clips["Idle"], 0.15f);
	}
}
