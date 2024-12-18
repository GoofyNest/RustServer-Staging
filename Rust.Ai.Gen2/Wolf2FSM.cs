using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

public class Wolf2FSM : FSMComponent
{
	[Serializable]
	public class WolfFSMData
	{
		public State_PlayRandomAnimation randomIdle = new State_PlayRandomAnimation();

		public State_Roam roam = new State_Roam();

		public State_Howl howl = new State_Howl();

		public State_CircleDynamic approach = new State_CircleDynamic();

		public State_Bark bark = new State_Bark();

		public State_Growl growlFire = new State_Growl();

		public State_ApproachFire approachFire = new State_ApproachFire();

		public State_FleeFire fleeFire = new State_FleeFire();

		public State_MoveToTarget charge = new State_MoveToTarget();

		public State_Attack attack = new State_Attack();

		public State_PlayAnimationRM leapAway = new State_PlayAnimationRM();

		public State_Circle reacCircle = new State_Circle();

		public State_CircleDynamic fastApproach = new State_CircleDynamic();

		public State_Hurt hurt = new State_Hurt();

		public State_Intimidated intimidated = new State_Intimidated();

		public State_Flee flee = new State_Flee();

		public State_Flee fleeShort = new State_Flee();

		public State_Dead dead = new State_Dead();

		public State_ApproachFood approachFood = new State_ApproachFood();

		public State_EatFood eatFood = new State_EatFood();

		public State_PlayAnimationRM growlFood = new State_PlayAnimationRM();

		public State_PlayAnimLoop sleep = new State_PlayAnimLoop();

		public State_AttackUnreachable attackUnreachable = new State_AttackUnreachable();
	}

	[SerializeField]
	private WolfFSMData data = new WolfFSMData();

	private Trans_Triggerable_HitInfo HurtTrans;

	private Trans_Triggerable_HitInfo DeathTrans;

	private Trans_Triggerable<BaseEntity> AllyGotHurtNearby;

	private Trans_Triggerable<BaseEntity> HowlTrans;

	private Trans_Triggerable<BaseEntity> BarkTrans;

	private void Start()
	{
		BaseEntity.Query.Server.AddBrain(base.baseEntity);
	}

	private void OnDestroy()
	{
		if (!Application.isQuitting)
		{
			BaseEntity.Query.Server.RemoveBrain(base.baseEntity);
		}
	}

	public override void InitShared()
	{
		if (base.baseEntity.isServer)
		{
			State_Nothing state = new State_Nothing
			{
				Name = "WaitForNavMesh"
			};
			State_Circle obj = new State_Circle
			{
				radius = 2f,
				speed = LimitedTurnNavAgent.Speeds.Sprint,
				Name = "Circle short"
			};
			State_MoveToTarget state2 = new State_MoveToTarget
			{
				speed = LimitedTurnNavAgent.Speeds.Walk,
				decelerationOverride = 6f,
				Name = "Step forward"
			};
			State_MoveToLastReachablePointNearTarget state3 = new State_MoveToLastReachablePointNearTarget
			{
				speed = LimitedTurnNavAgent.Speeds.FullSprint,
				succeedWhenDestinationIsReached = true,
				Name = "Go to last destination"
			};
			FSMStateBase fSMStateBase = data.leapAway.Clone();
			fSMStateBase.Name = "Leap away unreachable";
			State_Flee state4 = new State_Flee
			{
				distance = 8f,
				desiredDistance = 16f,
				Name = "Flee fire after attack"
			};
			FSMStateBase fSMStateBase2 = obj.Clone();
			fSMStateBase2.Name = "Circle short fire";
			FSMStateBase fSMStateBase3 = data.charge.Clone();
			fSMStateBase3.Name = "Charge fire";
			FSMStateBase fSMStateBase4 = data.attack.Clone();
			fSMStateBase4.Name = "Attack fire";
			DeathTrans = new Trans_Triggerable_HitInfo();
			HurtTrans = new Trans_Triggerable_HitInfo();
			Trans_Triggerable PathFailedTrans = new Trans_Triggerable();
			base.baseEntity.GetComponent<LimitedTurnNavAgent>().onPathFailed.AddListener(delegate
			{
				PathFailedTrans.Trigger();
			});
			Trans_Triggerable FireMeleeTrans = new Trans_Triggerable();
			base.baseEntity.GetComponent<SenseComponent>().onFireMelee.AddListener(delegate
			{
				FireMeleeTrans.Trigger();
			});
			Trans_Triggerable EncounterEndTrans = new Trans_Triggerable();
			base.baseEntity.GetComponent<NPCEncounterTimer>().onShouldGiveUp.AddListener(delegate
			{
				EncounterEndTrans.Trigger();
			});
			BarkTrans = new Trans_Triggerable<BaseEntity>();
			AllyGotHurtNearby = new Trans_Triggerable<BaseEntity>();
			HowlTrans = new Trans_Triggerable<BaseEntity>();
			TreeNode treeNode = new TreeNode();
			TreeNode treeNode2 = new TreeNode();
			TreeNode treeNode3 = new TreeNode();
			TreeNode treeNode4 = new TreeNode();
			TreeNode treeNode5 = new TreeNode();
			TreeNode treeNode6 = new TreeNode();
			TreeNode treeNode7 = new TreeNode();
			TreeNode treeNode8 = new TreeNode(data.howl);
			TreeNode treeNode9 = new TreeNode(data.approach);
			TreeNode treeNode10 = new TreeNode(data.bark);
			TreeNode treeNode11 = new TreeNode(data.charge);
			TreeNode treeNode12 = new TreeNode(data.attack);
			TreeNode treeNode13 = new TreeNode(data.leapAway);
			TreeNode treeNode14 = new TreeNode(obj);
			TreeNode treeNode15 = new TreeNode(data.reacCircle);
			TreeNode treeNode16 = new TreeNode(data.fastApproach);
			TreeNode treeNode17 = new TreeNode(data.fleeShort);
			TreeNode treeNode18 = new TreeNode();
			TreeNode treeNode19 = new TreeNode(state3);
			TreeNode treeNode20 = new TreeNode(data.attackUnreachable);
			TreeNode treeNode21 = new TreeNode(fSMStateBase);
			TreeNode treeNode22 = new TreeNode();
			TreeNode treeNode23 = new TreeNode(data.growlFire);
			TreeNode treeNode24 = new TreeNode(data.approachFire);
			TreeNode treeNode25 = new TreeNode(fSMStateBase2);
			TreeNode treeNode26 = new TreeNode(fSMStateBase3);
			TreeNode treeNode27 = new TreeNode(fSMStateBase4);
			TreeNode treeNode28 = new TreeNode(data.fleeFire);
			TreeNode treeNode29 = new TreeNode(state4);
			TreeNode treeNode30 = new TreeNode(state2);
			TreeNode treeNode31 = new TreeNode(data.intimidated);
			TreeNode treeNode32 = new TreeNode(data.flee);
			TreeNode treeNode33 = new TreeNode();
			TreeNode treeNode34 = new TreeNode(data.approachFood);
			TreeNode treeNode35 = new TreeNode(data.eatFood);
			TreeNode treeNode36 = new TreeNode(data.growlFood);
			TreeNode treeNode37 = new TreeNode();
			TreeNode treeNode38 = new TreeNode(data.roam);
			TreeNode treeNode39 = new TreeNode(data.hurt);
			TreeNode treeNode40 = new TreeNode(data.dead);
			TreeNode treeNode41 = new TreeNode(state);
			TreeNode treeNode42 = new TreeNode();
			TreeNode treeNode43 = new TreeNode();
			TreeNode treeNode44 = new TreeNode(data.sleep);
			TreeNode treeNode45 = new TreeNode(data.randomIdle);
			TreeNode treeNode46 = new TreeNode(new State_Nothing
			{
				Name = "Fire entry"
			});
			TreeNode treeNode47 = new TreeNode(new State_Nothing
			{
				Name = "Combat entry"
			});
			TreeNode treeNode48 = new TreeNode(new State_Nothing
			{
				Name = "Random post idle wait"
			});
			treeNode.AddChildren(treeNode2.AddTickTransition(treeNode40, DeathTrans).AddChildren(treeNode41.AddTickTransition(treeNode3, new Trans_IsNavmeshReady()), treeNode3.AddTickTransition(treeNode41, new Trans_IsNavmeshReady
			{
				Inverted = true
			}).AddChildren(treeNode4.AddTickTransition(treeNode39, HurtTrans).AddChildren(treeNode37.AddTickTransition(treeNode40, PathFailedTrans).AddTickTransition(treeNode9, HowlTrans).AddTickTransition(treeNode5, new Trans_HasTarget())
				.AddTickTransition(treeNode33, new Trans_SeesFood())
				.AddChildren(treeNode38.AddEndTransition(treeNode44, new Trans_RandomChance
				{
					chance = 0.25f
				}).AddEndTransition(treeNode45), treeNode44.AddEndTransition(treeNode38), treeNode45.AddEndTransition(treeNode48), treeNode48.AddTickTransition(treeNode38, new Trans_ElapsedTimeRandomized
				{
					MinDuration = 0.0,
					MaxDuration = 3.0
				})), treeNode5.AddTickTransition(treeNode37, new Trans_HasTarget
			{
				Inverted = true
			}).AddTickTransition(treeNode32, EncounterEndTrans).AddTickTransition(treeNode32, new Trans_TargetIsInSafeZone())
				.AddChildren(treeNode7.AddTickTransition(treeNode32, new Trans_TargetOrSelfInWater()).AddTickTransition(treeNode18, PathFailedTrans).AddChildren(treeNode6.AddTickTransition(treeNode22, new Trans_TargetIsNearFire
				{
					onlySeeFireWhenClose = true
				}).AddChildren(treeNode47.AddTickTransition(treeNode8, new Trans_HasBlackboardBool
				{
					Key = "WolfNearbyAlreadyHowled",
					Inverted = true
				}).AddTickTransition(treeNode9, new Trans_AlwaysValid()), treeNode43.AddTickTransition(treeNode32, new Trans_And
				{
					AllyGotHurtNearby,
					new Trans_TargetIsNearFire()
				}).AddTickTransition(treeNode16, AllyGotHurtNearby).AddTickTransition(treeNode11, BarkTrans)
					.AddChildren(treeNode8.AddTickTransition(treeNode9, new Trans_TargetInRange
					{
						Range = 12f
					}).AddEndTransition(treeNode9), treeNode9.AddTickBranchingTrans(treeNode11, new Trans_TargetInRange
					{
						Range = 12f
					}, treeNode10, new Trans_HasBlackboardBool
					{
						Key = "WolfNearbyAlreadyBarked",
						Inverted = true
					}).AddTickTransition(treeNode33, new Trans_And
					{
						new Trans_SeesFood(),
						new Trans_HasBlackboardBool
						{
							Key = "TriedToApproachUnreachableFood",
							Inverted = true
						}
					})), treeNode10.AddTickTransition(treeNode11, new Trans_TargetInRange
				{
					Range = 2f
				}).AddEndTransition(treeNode11), treeNode11.AddTickTransition(treeNode16, AllyGotHurtNearby).AddTickTransition(treeNode12, new Trans_TargetInRange
				{
					Range = 2f
				}).AddTickTransition(treeNode9, new Trans_ElapsedTime
				{
					Duration = 5.0
				})
					.AddFailureTransition(treeNode18), treeNode12.AddEndTransition(treeNode13, new Trans_TargetInFront
				{
					Angle = 120f,
					Inverted = true
				}).AddEndTransition(treeNode14), treeNode13.AddEndTransition(treeNode14), treeNode14.AddTickTransition(treeNode11, new Trans_ElapsedTimeRandomized
				{
					MinDuration = 0.75,
					MaxDuration = 1.5
				}).AddEndTransition(treeNode11), treeNode15.AddTickTransition(treeNode15, AllyGotHurtNearby).AddTickTransition(treeNode11, new Trans_ElapsedTimeRandomized
				{
					MinDuration = 2.0,
					MaxDuration = 4.0
				}).AddEndTransition(treeNode11), treeNode16.AddTickTransition(treeNode15, new Trans_TargetInRange
				{
					Range = data.reacCircle.radius + 5f
				}).AddTickTransition(treeNode11, BarkTrans), treeNode17.AddEndTransition(treeNode8)), treeNode22.AddTickTransition(treeNode32, PathFailedTrans).AddTickTransition(treeNode32, AllyGotHurtNearby).AddChildren(treeNode46.AddTickTransition(treeNode31, new Trans_TargetInRange
				{
					Range = 12f
				}).AddTickTransition(treeNode23, new Trans_HasBlackboardBool
				{
					Key = "AlreadyGrowled",
					Inverted = true
				}).AddTickTransition(treeNode24, new Trans_AlwaysValid()), treeNode42.AddTickBranchingTrans(treeNode31, FireMeleeTrans, treeNode23, new Trans_RandomChance
				{
					chance = 0.75f
				}).AddChildren(treeNode24.AddTickTransition(treeNode25, new Trans_TargetInRange
				{
					Range = 5f
				}).AddTickTransition(treeNode16, new Trans_TargetIsNearFire
				{
					Inverted = true
				}).AddTickTransition(treeNode16, new Trans_TargetInRange
				{
					Range = 21f,
					Inverted = true
				}), treeNode30.AddTickTransition(treeNode25, new Trans_TargetInRange
				{
					Range = 5f
				}).AddTickTransition(treeNode24, new Trans_ElapsedTimeRandomized
				{
					MinDuration = 1.0,
					MaxDuration = 3.0
				})), treeNode23.AddTickTransition(treeNode31, FireMeleeTrans).AddTickTransition(treeNode25, new Trans_TargetInRange
				{
					Range = 5f
				}).AddEndTransition(treeNode24), treeNode25.AddTickTransition(treeNode26, new Trans_ElapsedTimeRandomized
				{
					MinDuration = 0.5,
					MaxDuration = 1.25
				}).AddEndTransition(treeNode26), treeNode26.AddTickTransition(treeNode27, new Trans_TargetInRange
				{
					Range = 2f
				}), treeNode27.AddEndTransition(treeNode29), treeNode31.AddEndTransition(treeNode28), treeNode28.AddEndTransition(treeNode30), treeNode29.AddEndTransition(treeNode30))), treeNode18.AddChildren(treeNode19.AddFailureTransition(treeNode32).AddTickTransition(treeNode32, FireMeleeTrans).AddTickTransition(treeNode11, new Trans_CanReachTarget_Slow())
					.AddEndTransition(treeNode11, new Trans_CanReachTarget_Slow())
					.AddEndTransition(treeNode20)
					.AddEndTransition(treeNode32), treeNode21.AddEndTransition(treeNode19)), treeNode32.AddTickTransition(treeNode40, PathFailedTrans).AddEndTransition(treeNode16, new Trans_TargetInRange
				{
					Range = data.flee.desiredDistance
				}).AddEndTransition(treeNode38)), treeNode33.AddTickTransition(treeNode22, new Trans_TargetIsNearFire
			{
				onlySeeFireWhenClose = true
			}).AddTickTransition(treeNode9, HowlTrans).AddTickTransition(treeNode16, AllyGotHurtNearby)
				.AddTickTransition(treeNode11, BarkTrans)
				.AddTickTransition(treeNode, new Trans_SeesFood
				{
					Inverted = true
				})
				.AddChildren(treeNode34.AddTickTransition(treeNode36, new Trans_TargetInRange
				{
					Range = 12f
				}).AddFailureTransition(treeNode).AddEndTransition(treeNode35), treeNode35.AddTickTransition(treeNode36, new Trans_TargetInRange
				{
					Range = 12f
				}).AddFailureTransition(treeNode).AddEndTransition(treeNode), treeNode36.AddTickTransition(treeNode10, new Trans_TargetInRange
				{
					Range = 5f
				}).AddEndTransition(treeNode10, new Trans_TargetInRange
				{
					Range = 12f
				}).AddEndTransition(treeNode34))), treeNode39.AddEndTransition(treeNode32, new Trans_IsHealthBelowPercentage()).AddEndTransition(treeNode32, new Trans_HasBlackboardBool
			{
				Key = "HitByFire"
			}).AddEndTransition(treeNode32, new Trans_TargetIsNearFire())
				.AddEndTransition(treeNode32, new Trans_TargetInRange
				{
					Range = 50f,
					Inverted = true
				})
				.AddEndTransition(treeNode17, new Trans_InitialAlliesNotFighting())
				.AddEndTransition(treeNode11, new Trans_And
				{
					new Trans_RandomChance
					{
						chance = 0.5f
					},
					new Trans_TargetInRange
					{
						Range = 12f
					}
				})
				.AddEndTransition(treeNode15, new Trans_TargetInRange
				{
					Range = data.reacCircle.radius + 5f
				})
				.AddEndTransition(treeNode16)), treeNode20.AddFailureTransition(treeNode32).AddEndTransition(treeNode32, new Trans_TargetIsNearFire()).AddEndTransition(treeNode21)), treeNode40);
			BuildFromTree(treeNode);
			SetState(treeNode.GetFirstChildLeaf().state);
			Run();
		}
	}

	public void Hurt(HitInfo hitInfo)
	{
		if (GetComponent<SenseComponent>().CanTarget(hitInfo.Initiator) && (hitInfo.Initiator.IsNonNpcPlayer() || !(UnityEngine.Random.value > 0.5f)))
		{
			HurtTrans.Trigger(hitInfo);
		}
	}

	public void Intimidate(BaseEntity target)
	{
		AllyGotHurtNearby.Trigger(target);
	}

	public void Howl(BaseEntity target)
	{
		HowlTrans.Trigger(target);
	}

	public void Bark(BaseEntity target)
	{
		BarkTrans.Trigger(target);
	}

	public void Die(HitInfo hitInfo)
	{
		DeathTrans.Trigger(hitInfo);
	}
}
