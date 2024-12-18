using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

public class BlackboardComponent : EntityComponent<BaseEntity>, IServerComponent
{
	[SerializeField]
	private float factDuration = 30f;

	private HashSet<string> addedFacts = new HashSet<string>();

	private Dictionary<string, float> factExpirationTimes = new Dictionary<string, float>();

	public override void InitShared()
	{
		base.InitShared();
		InvokeRepeating("CleanExpiredFacts", Random.value, 1f);
	}

	public void Add(string value)
	{
		if (addedFacts.Add(value))
		{
			factExpirationTimes[value] = Time.time + factDuration;
		}
	}

	public void Remove(string value)
	{
		if (addedFacts.Remove(value))
		{
			factExpirationTimes.Remove(value);
		}
	}

	public void Clear()
	{
		addedFacts.Clear();
		factExpirationTimes.Clear();
	}

	public bool Has(string value)
	{
		return addedFacts.Contains(value);
	}

	public void CleanExpiredFacts()
	{
		using (TimeWarning.New("BlackboardComponent.CleanExpiredFacts"))
		{
			float time = Time.time;
			using PooledList<string> pooledList = Pool.Get<PooledList<string>>();
			foreach (string addedFact in addedFacts)
			{
				if (factExpirationTimes[addedFact] < time)
				{
					pooledList.Add(addedFact);
				}
			}
			foreach (string item in pooledList)
			{
				Remove(item);
			}
		}
	}
}
