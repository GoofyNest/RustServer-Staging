using UnityEngine;

namespace ConVar;

[Factory("physics")]
public class Physics : ConsoleSystem
{
	[ServerVar(Help = "The collision detection mode that dropped items and corpses should use")]
	public static int droppedmode = 2;

	[ServerVar(Help = "Send effects to clients when physics objects collide")]
	public static bool sendeffects = true;

	[ServerVar]
	public static bool groundwatchdebug = false;

	[ServerVar]
	public static int groundwatchfails = 1;

	[ServerVar]
	public static float groundwatchdelay = 0.1f;

	[ServerVar(Help = "The collision detection mode that server-side ragdolls should use")]
	public static int serverragdollmode = 3;

	private const float baseGravity = -9.81f;

	private static bool _serversideragdolls = false;

	[ClientVar]
	[ServerVar]
	public static bool batchsynctransforms = true;

	private static bool _treecollision = true;

	[ServerVar]
	public static float bouncethreshold
	{
		get
		{
			return UnityEngine.Physics.bounceThreshold;
		}
		set
		{
			UnityEngine.Physics.bounceThreshold = value;
		}
	}

	[ServerVar]
	public static float sleepthreshold
	{
		get
		{
			return UnityEngine.Physics.sleepThreshold;
		}
		set
		{
			UnityEngine.Physics.sleepThreshold = value;
		}
	}

	[ServerVar(Help = "The default solver iteration count permitted for any rigid bodies (default 7). Must be positive")]
	public static int solveriterationcount
	{
		get
		{
			return UnityEngine.Physics.defaultSolverIterations;
		}
		set
		{
			UnityEngine.Physics.defaultSolverIterations = value;
		}
	}

	[ReplicatedVar(Help = "Gravity multiplier", Default = "1.0")]
	public static float gravity
	{
		get
		{
			return UnityEngine.Physics.gravity.y / -9.81f;
		}
		set
		{
			UnityEngine.Physics.gravity = new Vector3(0f, value * -9.81f, 0f);
		}
	}

	[ReplicatedVar(Help = "Do ragdoll physics calculations on the server, or use the old client-side system", Saved = true, ShowInAdminUI = true)]
	public static bool serversideragdolls
	{
		get
		{
			return _serversideragdolls;
		}
		set
		{
			_serversideragdolls = value;
			UnityEngine.Physics.IgnoreLayerCollision(9, 13, !_serversideragdolls);
			UnityEngine.Physics.IgnoreLayerCollision(9, 11, !_serversideragdolls);
			UnityEngine.Physics.IgnoreLayerCollision(9, 28, !_serversideragdolls);
		}
	}

	[ClientVar]
	[ServerVar]
	public static bool autosynctransforms
	{
		get
		{
			return UnityEngine.Physics.autoSyncTransforms;
		}
		set
		{
			UnityEngine.Physics.autoSyncTransforms = value;
		}
	}

	[ReplicatedVar(Help = "Do players and vehicles collide with trees?", Saved = true, ShowInAdminUI = true)]
	public static bool treecollision
	{
		get
		{
			return _treecollision;
		}
		set
		{
			_treecollision = value;
			UnityEngine.Physics.IgnoreLayerCollision(15, 30, !_treecollision);
			UnityEngine.Physics.IgnoreLayerCollision(12, 30, !_treecollision);
		}
	}

	internal static void ApplyDropped(Rigidbody rigidBody)
	{
		if (droppedmode <= 0)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
		}
		if (droppedmode == 1)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
		}
		if (droppedmode == 2)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		}
		if (droppedmode >= 3)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
		}
	}
}
