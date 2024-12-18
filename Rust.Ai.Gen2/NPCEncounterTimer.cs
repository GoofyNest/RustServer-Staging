using System;
using Facepunch;
using UnityEngine;
using UnityEngine.Events;

namespace Rust.Ai.Gen2;

[SoftRequireComponent(typeof(SenseComponent))]
public class NPCEncounterTimer : EntityComponent<BaseEntity>, IServerComponent
{
	[NonSerialized]
	public UnityEvent onShouldGiveUp = new UnityEvent();

	private const float giveUpDurationSeconds = 120f;

	private const float fireTimeMultiplier = 4f;

	private const float mountedTimeMultiplier = 12f;

	private float? encounterRemainingTimeSeconds;

	private double? _lastTickTime;

	private SenseComponent _senseComponent;

	private double LastTickTime
	{
		get
		{
			double valueOrDefault = _lastTickTime.GetValueOrDefault();
			if (!_lastTickTime.HasValue)
			{
				valueOrDefault = Time.timeAsDouble;
				_lastTickTime = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
		set
		{
			_lastTickTime = value;
		}
	}

	private SenseComponent Senses => _senseComponent ?? (_senseComponent = base.baseEntity.GetComponent<SenseComponent>());

	public void Tick()
	{
		float num = (float)(Time.timeAsDouble - LastTickTime);
		LastTickTime = Time.timeAsDouble;
		BaseEntity target;
		bool flag = Senses.FindTarget(out target);
		if (encounterRemainingTimeSeconds.HasValue && !flag)
		{
			encounterRemainingTimeSeconds = null;
		}
		else if (!encounterRemainingTimeSeconds.HasValue && flag)
		{
			StartTimer();
		}
		else
		{
			if (!encounterRemainingTimeSeconds.HasValue)
			{
				return;
			}
			if (base.baseEntity is BaseCombatEntity { SecondsSinceAttacked: <5f })
			{
				StartTimer();
				using PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>();
				Senses.GetInitialAllies(pooledList);
				foreach (BaseEntity item in pooledList)
				{
					item.GetComponent<NPCEncounterTimer>().StartTimer();
				}
			}
			float num2 = 1f;
			if (target.ToNonNpcPlayer(out var player) && player.isMounted)
			{
				num2 = 12f;
			}
			else if (Trans_TargetIsNearFire.Test(base.baseEntity, Senses))
			{
				num2 = 4f;
			}
			encounterRemainingTimeSeconds -= num * num2;
			if (!(encounterRemainingTimeSeconds <= 0f))
			{
				return;
			}
			GiveUp();
			using PooledList<BaseEntity> pooledList2 = Pool.Get<PooledList<BaseEntity>>();
			Senses.GetInitialAllies(pooledList2);
			foreach (BaseEntity item2 in pooledList2)
			{
				item2.GetComponent<NPCEncounterTimer>().GiveUp();
			}
		}
	}

	private void StartTimer()
	{
		encounterRemainingTimeSeconds = 120f;
	}

	private void GiveUp()
	{
		if (encounterRemainingTimeSeconds.HasValue)
		{
			encounterRemainingTimeSeconds = null;
			onShouldGiveUp.Invoke();
		}
	}
}
