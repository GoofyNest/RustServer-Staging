using FIMSpace.FLook;
using FIMSpace.FSpine;
using Network;
using UnityEngine;

namespace Rust.Ai.Gen2;

public class NPCAnimController : EntityComponent<BaseEntity>, IClientComponent
{
	public enum AnimatorType
	{
		NoStrafe,
		Strafe
	}

	[SerializeField]
	private AnimatorType animatorType;

	[SerializeField]
	private Animator animator;

	[SerializeField]
	private FSpineAnimator spineAnimator;

	[SerializeField]
	private FLookAnimator lookAnimator;

	[SerializeField]
	private float maxWalkingSpeed;

	[SerializeField]
	private string[] animationBlacklist = new string[4] { "prowl", "walk", "trot", "run" };

	[SerializeField]
	private int animatorLayer;

	[SerializeField]
	private float maxPitchToConformToSlope = 30f;

	[SerializeField]
	private string animationsPrefix = "wolf_";

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("NPCAnimController.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}
}
