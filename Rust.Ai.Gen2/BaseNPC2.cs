using UnityEngine;

namespace Rust.Ai.Gen2;

public class BaseNPC2 : BaseCombatEntity
{
	[SerializeField]
	private float mass = 45f;

	public override bool IsNpc => true;

	public bool IsAnimal => true;

	public override float RealisticMass => mass;

	public override float MaxVelocity()
	{
		return 10f;
	}
}
