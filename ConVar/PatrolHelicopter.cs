using UnityEngine;

namespace ConVar;

[Factory("heli")]
public class PatrolHelicopter : ConsoleSystem
{
	private const string path = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";

	[ServerVar]
	public static float lifetimeMinutes = 30f;

	[ServerVar]
	public static int guns = 1;

	[ServerVar]
	public static float bulletDamageScale = 1f;

	[ServerVar]
	public static float bulletAccuracy = 2f;

	[ServerVar]
	public static void drop(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			Debug.Log("heli called to : " + basePlayer.transform.position.ToString());
			BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
			if ((bool)baseEntity)
			{
				baseEntity.GetComponent<PatrolHelicopterAI>().SetInitialDestination(basePlayer.transform.position + new Vector3(0f, 10f, 0f), 0f);
				baseEntity.Spawn();
			}
		}
	}

	[ServerVar]
	public static void calltome(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			Debug.Log("heli called to : " + basePlayer.transform.position.ToString());
			BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
			if ((bool)baseEntity)
			{
				baseEntity.GetComponent<PatrolHelicopterAI>().SetInitialDestination(basePlayer.transform.position + new Vector3(0f, 10f, 0f));
				baseEntity.Spawn();
			}
		}
	}

	[ServerVar]
	public static void call(Arg arg)
	{
		if ((bool)arg.Player())
		{
			Debug.Log("Helicopter inbound");
			BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
			if ((bool)baseEntity)
			{
				baseEntity.Spawn();
			}
		}
	}

	[ServerVar]
	public static void strafe(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.strafe_target = basePlayer;
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Strafe_Enter(basePlayer);
		}
	}

	[ServerVar]
	public static void orbit(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Orbit_Enter(70f);
		}
	}

	[ServerVar]
	public static void orbitstrafe(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.strafe_target = basePlayer;
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_OrbitStrafe_Enter();
		}
	}

	[ServerVar]
	public static void move(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Move_Enter(basePlayer.transform.position);
		}
	}

	[ServerVar]
	public static void flee(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Flee_Enter(basePlayer.transform.position);
		}
	}

	[ServerVar]
	public static void patrol(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Patrol_Enter();
		}
	}

	[ServerVar]
	public static void death(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			PatrolHelicopterAI heliInstance = PatrolHelicopterAI.heliInstance;
			if (heliInstance == null)
			{
				Debug.Log("no heli instance");
				return;
			}
			heliInstance.interestZoneOrigin = basePlayer.transform.position;
			heliInstance.ExitCurrentState();
			heliInstance.State_Death_Enter();
		}
	}

	[ServerVar]
	public static void testpuzzle(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer)
		{
			_ = basePlayer.IsDeveloper;
		}
	}
}
