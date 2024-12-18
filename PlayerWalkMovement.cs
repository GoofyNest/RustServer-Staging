using UnityEngine;

public class PlayerWalkMovement : BaseMovement
{
	public const float WaterLevelHead = 0.75f;

	public const float WaterLevelNeck = 0.65f;

	public PhysicMaterial zeroFrictionMaterial;

	public PhysicMaterial highFrictionMaterial;

	public float capsuleHeight = 1.8f;

	public float capsuleCenter = 0.9f;

	public float capsuleHeightDucked = 1.1f;

	public float capsuleCenterDucked = 0.55f;

	public float capsuleHeightCrawling = 0.5f;

	public float capsuleCenterCrawling = 0.5f;

	public float gravityTestRadius = 0.2f;

	public float gravityMultiplier = 2.5f;

	public float gravityMultiplierSwimming = 0.1f;

	public float maxAngleWalking = 50f;

	public float maxAngleClimbing = 60f;

	public float maxAngleSliding = 90f;

	public float maxStepHeight = 0.5f;
}
